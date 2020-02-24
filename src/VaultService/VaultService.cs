using System.Collections.Generic;
using System.Fabric;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using VaultService.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace VaultService
{
    internal sealed class VaultService : StatefulService
    {
        private readonly IServiceWebHost _host;
        private readonly IList<IServiceCommunicationListener> _serviceCommunicationListeners = new List<IServiceCommunicationListener>();

        public VaultService(StatefulServiceContext context)
            : base(context)
        {
            var builder = new KestrelBasedServiceWebHostBuilder(Context, "StorageEndpoint",
                services =>
                {
                    services.AddSingleton(StateManager);
                });
            _host = builder.Build();
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            _serviceCommunicationListeners.Clear();
            var listeners = new List<ServiceReplicaListener>();

            listeners.Add(new ServiceReplicaListener(context => _host.Listener, _host.EndpointName));

            var listenerFactories = _host.Services.GetServices<IServiceCommunicationListenerFactory>();
            foreach (var listenerFactory in listenerFactories)
            {
                var listener = listenerFactory.Create();
                _serviceCommunicationListeners.Add(listener);
                listeners.Add(new ServiceReplicaListener(context => listener, listener.EndpointName));
            }

            return listeners;
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            await base.RunAsync(cancellationToken);

            foreach (var listener in _serviceCommunicationListeners)
            {
                await listener.RunAsync(cancellationToken);
            }
        }
    }
}