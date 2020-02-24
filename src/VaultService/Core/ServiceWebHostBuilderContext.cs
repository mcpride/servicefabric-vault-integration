using System;
using System.Fabric;
using System.Fabric.Description;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;

namespace VaultService.Core
{
    public class ServiceWebHostBuilderContext : WebHostBuilderContext
    {
        private readonly ServiceContext _serviceContext;

        public ServiceWebHostBuilderContext(ServiceContext serviceContext)
        {
            _serviceContext = serviceContext ?? throw new ArgumentNullException(nameof(serviceContext));
        }

        public string GetUrlOfEndpoint(string webEndpointName)
        {
            var resourceDescription = GetEndpointResourceDescription(webEndpointName);
            return string.Format(CultureInfo.InvariantCulture, resourceDescription.EndpointType == EndpointType.Internal
                ? "{0}://127.0.0.1:{1}"
                : "{0}://+:{1}", resourceDescription.Protocol.ToString().ToLower(), resourceDescription.Port);
        }

        private EndpointResourceDescription GetEndpointResourceDescription(string endpointName)
        {
            if (endpointName == null)
            {
                throw new ArgumentNullException(nameof(endpointName));
            }
            if (!_serviceContext.CodePackageActivationContext.GetEndpoints().Contains(endpointName))
            {
                throw new InvalidOperationException($"Endpoint definition for endpoint '{endpointName}' not found in 'CodePackageActivationContext'!");
            }
            return _serviceContext.CodePackageActivationContext.GetEndpoint(endpointName);
        }

    }
}
