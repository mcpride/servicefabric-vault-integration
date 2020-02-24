using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace VaultService
{
    internal static class Program
    {
        /// <summary>
        /// Dies ist der Einstiegspunkt des Diensthostprozesses.
        /// </summary>
        private static void Main()
        {
            try
            {
                // Die Datei "ServiceManifest.XML" definiert mindestens einen Diensttypnamen.
                // Durch die Registrierung eines Diensts wird ein Diensttypname einem .NET-Typ zugeordnet.
                // Wenn Service Fabric eine Instanz dieses Diensttyps erstellt,
                // wird eine Instanz der Klasse in diesem Hostprozess erstellt.

                ServiceRuntime.RegisterServiceAsync("VaultServiceType",
                    context => new VaultService(context)).GetAwaiter().GetResult();

                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(VaultService).Name);

                // Verhindert, dass dieser Hostprozess beendet wird, damit die Dienste weiterhin ausgeführt werden. 
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                throw;
            }
        }
    }
}
