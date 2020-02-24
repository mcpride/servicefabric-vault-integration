using Microsoft.ServiceFabric.Services.Communication.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace VaultService.Core
{
    public interface IServiceCommunicationListener : ICommunicationListener
    {
        string EndpointName { get; }
        Task RunAsync(CancellationToken cancellationToken);
    }
}