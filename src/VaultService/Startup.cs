using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Fabric;
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
            services.AddControllers();

            services
                .AddTransient<IServiceCommunicationListenerFactory, VaultCommunicationListenerFactory>()
                .AddSingleton<IS3Storage, ServiceFabricStorage>()
                .AddTransient(provider => new Lazy<IS3Storage>(provider.GetRequiredService<IS3Storage>()))
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
    }
}
