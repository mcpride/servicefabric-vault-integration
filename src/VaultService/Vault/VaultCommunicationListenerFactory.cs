using Microsoft.Extensions.Logging;
using System;
using System.Fabric;
using System.IO;
using VaultService.Core;
using VaultService.S3.Storage;

namespace VaultService.Vault
{
    public class VaultCommunicationListenerFactory : IServiceCommunicationListenerFactory
    {
        private readonly ServiceContext _serviceContext;
        private readonly IS3Storage _s3Storage;
        private readonly ILoggerFactory _loggerFactory;

        public VaultCommunicationListenerFactory(ServiceContext serviceContext, IS3Storage s3Storage, ILoggerFactory loggerFactory)
        {
            _serviceContext = serviceContext ?? throw new ArgumentNullException(nameof(serviceContext));
            _s3Storage = s3Storage ?? throw new ArgumentNullException(nameof(s3Storage));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public IServiceCommunicationListener Create()
        {
            var endpointName = "VaultEndpoint";
            var currentDir = Directory.GetCurrentDirectory();
            var fileName = Path.Combine(currentDir, "Vault.exe");
            var workingDirectory = _serviceContext.CodePackageActivationContext.WorkDirectory;
            var storagePort = _serviceContext.CodePackageActivationContext.GetEndpoint("StorageEndpoint").Port;

            return new VaultCommunicationListener(_serviceContext, _s3Storage, _loggerFactory, endpointName, fileName, workingDirectory, storagePort);
        }
    }
}
