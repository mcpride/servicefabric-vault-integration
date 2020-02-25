using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Fabric.Description;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VaultService.Core;
using VaultService.S3.Model;
using VaultService.S3.Storage;

namespace VaultService.Vault
{
    public class VaultCommunicationListener : IServiceCommunicationListener
    {
        private readonly ILogger _logger;
        private readonly ILogger _vaultLogger;
        private readonly string _fileName;
        private readonly string _workingDirectory;
        private readonly int _storagePort;
        private readonly ServiceContext _serviceContext;
        private readonly IS3Storage _s3Storage;
        private Process _process;
        private ManualResetEventSlim _processWaitHandle;

        public string EndpointName { get; }

        public VaultCommunicationListener(ServiceContext serviceContext, IS3Storage s3Storage, ILoggerFactory loggerFactory, string endpointName, string fileName, string workingDirectory, int storagePort)
        {
            _serviceContext = serviceContext ?? throw new ArgumentNullException(nameof(serviceContext));
            _s3Storage = s3Storage ?? throw new ArgumentNullException(nameof(s3Storage));

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (string.IsNullOrEmpty(endpointName))
            {
                throw new ArgumentException($"Argument {nameof(endpointName)} must not be null or empty!", nameof(endpointName));
            }

            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException($"Argument {nameof(fileName)} must not be null or empty!", nameof(fileName));
            }

            if (string.IsNullOrEmpty(workingDirectory))
            {
                throw new ArgumentException($"Argument {nameof(workingDirectory)} must not be null or empty!", nameof(workingDirectory));
            }

            if (storagePort < 1)
            {
                throw new ArgumentException($"Argument {nameof(storagePort)} must be higher than 0!", nameof(storagePort));
            }

            _logger = loggerFactory.CreateLogger<VaultCommunicationListener>();
            _vaultLogger = loggerFactory.CreateLogger("vault");
            EndpointName = endpointName;
            _fileName = fileName;
            _workingDirectory = workingDirectory;
            _storagePort = storagePort;
            _processWaitHandle = new ManualResetEventSlim(false);
        }

        public void Abort()
        {
            StopProcess(CancellationToken.None).Wait();
        }

        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            await StopProcess(cancellationToken);
        }

        public async Task<string> OpenAsync(CancellationToken cancellationToken)
        {
            var endpoint = _serviceContext.CodePackageActivationContext.GetEndpoint(EndpointName);
            var configFile = Path.Combine(_workingDirectory, "vault.hcl");
            await WriteConfigFile(endpoint, configFile);
            return $"{endpoint.Protocol}://{endpoint.IpAddressOrFqdn}:{endpoint.Port}/";
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            await _s3Storage.AddBucketAsync(new Bucket { Id = "vaultbucket", CreationDate = DateTime.UtcNow }, cancellationToken);

            if (! await StartProcess(cancellationToken))
            {
                var process = _process;
                if (process != null)
                {
                    _logger.LogError("Start of {FileName} has been failed! (Exit code: {ExitCode})", _fileName, process.ExitCode);
                }
                else
                {
                    _logger.LogError("Start of {FileName} has been failed!", _fileName);
                }
                await StopProcess(cancellationToken);
                throw new InvalidProgramException($"Start of {_fileName} has been failed!");
            }
        }

        private void DataReceived(object sender, DataReceivedEventArgs e)
        {
            //TODO: Parse, log vault's console output
            _vaultLogger.LogInformation(e.Data);
        }

        private void Exited(object sender, EventArgs e)
        {
            _processWaitHandle.Set();
        }

        private async Task<bool> StartProcess(CancellationToken cancellationToken)
        {
            return await Task.Run(() => 
            {
                var vaultProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _fileName,
                        WorkingDirectory = _workingDirectory,
                        //Arguments = "server -config=vault.hcl -log-level=debug",
                        Arguments = "server -config=vault.hcl",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    },
                    EnableRaisingEvents = true,
                };
                vaultProcess.ErrorDataReceived += DataReceived;
                vaultProcess.OutputDataReceived += DataReceived;
                vaultProcess.Exited += Exited;
                _processWaitHandle = new ManualResetEventSlim(false);
                _process = vaultProcess;
                _logger.LogInformation("Starting process {FileName} with Working directory {WorkingDirectory} ...", _fileName, _workingDirectory);
                _process.Start();
                _process.BeginErrorReadLine();
                _process.BeginOutputReadLine();

                // ReSharper disable once EmptyGeneralCatchClause
                try { _processWaitHandle.Wait(5000, cancellationToken); } catch { }

                return (_process.HasExited || cancellationToken.IsCancellationRequested) ? false : true;
            }, cancellationToken);
        }

        private async Task StopProcess(CancellationToken cancellationToken)
        {
            await Task.Run(() => 
            {
                var process = _process;
                _process = null;
                if ((process != null))
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                    process.Dispose();
                }
            }, cancellationToken);
        }

        private async Task WriteConfigFile(EndpointResourceDescription endpoint, string configFile)
        {
            var config = new List<string>
            {
                "storage \"s3\" {",
                "    access_key = \"abcd1234\"",
                "    secret_key = \"defg5678\"",
                "    bucket     = \"vaultbucket\"",
                "    disable_ssl = \"true\"",
                "    s3_force_path_style = \"true\"",
                $"    endpoint = \"http://127.0.0.1:{_storagePort}/\"",
                "}",
                "",
                "listener \"tcp\" {",
                $"  address = \"{endpoint.IpAddressOrFqdn}:{endpoint.Port}\"",
                "  tls_disable = 1",
                "}",
                "",
                "ui = true",
                "disable_mlock = true"
            };
            await File.WriteAllLinesAsync(configFile, config);
        }
    }
}
