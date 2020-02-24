using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;

namespace VaultService.Core
{
    public class KestrelBasedServiceWebHost : IServiceWebHost
    {
        private readonly ServiceContext _serviceContext;
        private IWebHost _internalWebHost;
        private ICommunicationListener _listener;

        public IFeatureCollection ServerFeatures => _internalWebHost.ServerFeatures;

        public IServiceProvider Services => _internalWebHost.Services;

        public string EndpointName { get; }

        public ICommunicationListener Listener
        {
            get
            {
                return _listener ?? (_listener = new KestrelCommunicationListener(_serviceContext, (url, listener) => _internalWebHost));
            }
        }

        internal KestrelBasedServiceWebHost(ServiceContext serviceContext, IWebHost internalWebHost, string endpointName)
        {
            _serviceContext = serviceContext ?? throw new ArgumentNullException(nameof(serviceContext));
            _internalWebHost = internalWebHost ?? throw new ArgumentNullException(nameof(internalWebHost));
            EndpointName = endpointName;
        }

        public void Dispose()
        {
            _internalWebHost.Dispose();
        }

        public void Start()
        {
            _internalWebHost.Start();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await _internalWebHost.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await _internalWebHost.StopAsync(cancellationToken);
        }
    }
}