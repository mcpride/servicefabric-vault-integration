using System;
using System.Threading;

namespace VaultService.Core
{
    public class CancellableEventArgs : EventArgs
    {
        public CancellationToken CancellationToken { get; }

        public CancellableEventArgs(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }
    }
}