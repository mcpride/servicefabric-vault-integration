using System;
using System.Fabric;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace VaultService.Core
{
    public class KestrelBasedServiceWebHostBuilder
    {
        private readonly ServiceContext _serviceContext;
        private readonly string _webEndpointName;
        private readonly Action<IServiceCollection> _configureAdditionalServices;
        private readonly Action<ServiceWebHostBuilderContext, KestrelServerOptions> _configureKestrel;

        public KestrelBasedServiceWebHostBuilder(
            ServiceContext serviceContext,
            string webEndpointName)
        {
            _serviceContext = serviceContext ?? throw new ArgumentNullException(nameof(serviceContext));
            _webEndpointName = webEndpointName ?? throw new ArgumentNullException(nameof(webEndpointName));
        }

        public KestrelBasedServiceWebHostBuilder(
            ServiceContext serviceContext,
            string webEndpointName,
            Action<IServiceCollection> configureAdditionalServices)
        {
            _serviceContext = serviceContext ?? throw new ArgumentNullException(nameof(serviceContext));
            _webEndpointName = webEndpointName ?? throw new ArgumentNullException(nameof(webEndpointName));
            _configureAdditionalServices = configureAdditionalServices;
        }

        public KestrelBasedServiceWebHostBuilder(
            ServiceContext serviceContext, 
            string webEndpointName, 
            Action<IServiceCollection> configureAdditionalServices, 
            Action<ServiceWebHostBuilderContext, KestrelServerOptions> configureKestrel)
        {
            _serviceContext = serviceContext ?? throw new ArgumentNullException(nameof(serviceContext));
            _webEndpointName = webEndpointName ?? throw new ArgumentNullException(nameof(webEndpointName));
            _configureAdditionalServices = configureAdditionalServices;
            _configureKestrel = configureKestrel;
        }

        public IServiceWebHost Build()
        {
            var builder = new WebHostBuilder();

            if (_configureKestrel != null)
            {
                var configureKestrelAction = new Action<WebHostBuilderContext, KestrelServerOptions>((c, o) =>
                {
                    var sc = new ServiceWebHostBuilderContext(_serviceContext)
                    {
                        Configuration = c.Configuration,
                        HostingEnvironment = c.HostingEnvironment,
                    };
                    _configureKestrel(sc, o);
                    c.Configuration = sc.Configuration;
                    c.HostingEnvironment = sc.HostingEnvironment;
                });
                builder.UseKestrel(configureKestrelAction);
            }
            else
            {
                builder.UseKestrel();
            }

            builder
                .ConfigureServices(
                    services =>
                    {
                        services
                            .AddSingleton(_serviceContext);
                        _configureAdditionalServices?.Invoke(services);
                    })
                .UseStartup<Startup>()
                .UseUrls(new ServiceWebHostBuilderContext(_serviceContext).GetUrlOfEndpoint(_webEndpointName));
            var webHost = builder.Build();
            var serviceWebHost = new KestrelBasedServiceWebHost(_serviceContext, webHost, _webEndpointName);
            return serviceWebHost;
        }
    }
}