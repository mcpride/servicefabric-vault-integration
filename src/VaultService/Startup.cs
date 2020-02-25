using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core.Enrichers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.IO;
using VaultService.Core;
using VaultService.S3.Model;
using VaultService.S3.Responses;
using VaultService.S3.Responses.Serializers;
using VaultService.S3.Storage;
using VaultService.Vault;

namespace VaultService
{
    public class Startup
    {
        private readonly ServiceContext _serviceContext;

        public Startup(IConfiguration configuration, ServiceContext serviceContext)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _serviceContext = serviceContext ?? throw new ArgumentNullException(nameof(serviceContext));
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            ConfigureLogging(services, _serviceContext);

            services.AddControllers();

            services
                .AddTransient<IServiceCommunicationListenerFactory, VaultCommunicationListenerFactory>()
                .AddSingleton<IS3Storage, ServiceFabricStorage>()
                .AddSingleton(BuildResponder());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private static IS3Responder BuildResponder()
        {
            var s3Serializers = new Dictionary<Type, IS3Serializer>
            {
                {typeof (IEnumerable<Bucket>), new BucketListSerializer()},
                {typeof (S3ObjectSearchResponse), new S3ObjectSearchSerializer()},
                {typeof (BucketNotFound), new BucketNotFoundSerializer()},
                {typeof (ACLRequest), new ACLSerializer()},
                {typeof (DeleteRequest), new DeleteResultSerializer()}
            };

            IS3Responder s3Responder = new S3XmlResponder(s3Serializers);
            return s3Responder;
        }

        private static void ConfigureLogging(IServiceCollection services, ServiceContext serviceContext)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.File(Path.Combine(serviceContext.CodePackageActivationContext.LogDirectory, "vaultservice.log"),
                    fileSizeLimitBytes: 1_000_000,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: 10,
                    shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}",
                    flushToDiskInterval: TimeSpan.FromSeconds(1))
                .CreateLogger();

            var properties = new[]
            {
                new PropertyEnricher("ServiceTypeName", serviceContext.ServiceTypeName),
                new PropertyEnricher("ServiceName", serviceContext.ServiceName),
                new PropertyEnricher("PartitionId", serviceContext.PartitionId),
                new PropertyEnricher("InstanceId", serviceContext.ReplicaOrInstanceId),
                new PropertyEnricher("ProcessId", Process.GetCurrentProcess().Id)
            };

            services.AddLogging(loggingBuilder
                => loggingBuilder
                    .AddSerilog(Log.Logger.ForContext(properties), true));
        }
    }
}
