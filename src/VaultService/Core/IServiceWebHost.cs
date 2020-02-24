using Microsoft.AspNetCore.Hosting;
using Microsoft.ServiceFabric.Services.Communication.Runtime;

namespace VaultService.Core
{
    public interface IServiceWebHost : IWebHost
    {
        string EndpointName { get; }
        ICommunicationListener Listener { get; }
    }
}