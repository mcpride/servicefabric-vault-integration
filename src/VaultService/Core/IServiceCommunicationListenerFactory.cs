namespace VaultService.Core
{
    public interface IServiceCommunicationListenerFactory
    {
        IServiceCommunicationListener Create();
    }
}