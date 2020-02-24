using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Runtime;

namespace VaultService
{
    [EventSource(Name = "MyCompany-Vault-VaultService")]
    internal sealed class ServiceEventSource : EventSource
    {
        public static readonly ServiceEventSource Current = new ServiceEventSource();

        static ServiceEventSource()
        {
            // Eine Problemumgehung für den Fall, dass ETW-Aktivitäten erst nachverfolgt werden, nachdem die Tasksinfrastruktur initialisiert wurde.
            // Dieses Problem wird in .NET Framework 4.6.2 behoben.
            Task.Run(() => { });
        }

        // Der Instanzkonstruktor ist privat, um Singletonsemantik zu erzwingen.
        private ServiceEventSource() : base() { }

        #region Schlüsselwörter
        // Ereignisschlüsselwörter können zum Kategorisieren von Ereignissen verwendet werden. 
        // Jedes Schlüsselwort ist eine Bitkennzeichnung. Ein einzelnes Ereignis kann mehreren Schlüsselwörtern (über die Eigenschaft "EventAttribute.Keywords") zugeordnet werden.
        // Schlüsselwörter müssen als eine öffentliche Klasse namens "Keywords" in der "EventSource" definiert werden, die sie verwendet.
        public static class Keywords
        {
            public const EventKeywords Requests = (EventKeywords)0x1L;
            public const EventKeywords ServiceInitialization = (EventKeywords)0x2L;
        }
        #endregion

        #region Ereignisse
        // Definiert eine Instanzmethode für jedes Ereignis, das Sie aufzeichnen möchten, und wendet ein Attribut [Event] darauf an.
        // Der Methodenname ist der Name des Ereignisses.
        // Übergeben Sie alle Parameter, die Sie mit dem Ereignis aufzeichnen möchten (nur primitive Integertypen, "DateTime", GUID und Zeichenfolgen sind zulässig).
        // Jede Ereignismethodenimplementierung sollte überprüfen, ob die Ereignisquelle aktiviert ist. Wenn dies der Fall ist, sollte die Methode "WriteEvent()" zum Auslösen des Ereignisses aufgerufen werden.
        // Die Anzahl und die Typen der an jede Ereignismethode übergebenen Argumente müssen genau mit den Elementen übereinstimmen, die an "WriteEvent()" übergeben werden.
        // Versehen Sie alle Methoden, die kein Ereignis definieren, mit dem Attribut [NonEvent].
        // Weitere Informationen finden Sie unter https://msdn.microsoft.com/de-de/library/system.diagnostics.tracing.eventsource.aspx.

        [NonEvent]
        public void Message(string message, params object[] args)
        {
            if (this.IsEnabled())
            {
                string finalMessage = string.Format(message, args);
                Message(finalMessage);
            }
        }

        private const int MessageEventId = 1;
        [Event(MessageEventId, Level = EventLevel.Informational, Message = "{0}")]
        public void Message(string message)
        {
            if (this.IsEnabled())
            {
                WriteEvent(MessageEventId, message);
            }
        }

        [NonEvent]
        public void ServiceMessage(ServiceContext serviceContext, string message, params object[] args)
        {
            if (this.IsEnabled())
            {

                string finalMessage = string.Format(message, args);
                ServiceMessage(
                    serviceContext.ServiceName.ToString(),
                    serviceContext.ServiceTypeName,
                    GetReplicaOrInstanceId(serviceContext),
                    serviceContext.PartitionId,
                    serviceContext.CodePackageActivationContext.ApplicationName,
                    serviceContext.CodePackageActivationContext.ApplicationTypeName,
                    serviceContext.NodeContext.NodeName,
                    finalMessage);
            }
        }

        // Für sehr häufig auftretende Ereignisse kann es vorteilhaft sein, Ereignisse mithilfe der WriteEventCore-API auszulösen.
        // Dies führt zu einer effizienteren Parameterverarbeitung, erfordert aber die explizite Zuweisung der EventData-Struktur und unsicheren Code.
        // Definieren Sie zum Aktivieren dieses Codepfads das bedingte Kompilierungssymbol UNSAFE, und aktivieren Sie die Unterstützung von unsicherem Code in den Projekteigenschaften.
        private const int ServiceMessageEventId = 2;
        [Event(ServiceMessageEventId, Level = EventLevel.Informational, Message = "{7}")]
        private
#if UNSAFE
        unsafe
#endif
        void ServiceMessage(
            string serviceName,
            string serviceTypeName,
            long replicaOrInstanceId,
            Guid partitionId,
            string applicationName,
            string applicationTypeName,
            string nodeName,
            string message)
        {
#if !UNSAFE
            WriteEvent(ServiceMessageEventId, serviceName, serviceTypeName, replicaOrInstanceId, partitionId, applicationName, applicationTypeName, nodeName, message);
#else
            const int numArgs = 8;
            fixed (char* pServiceName = serviceName, pServiceTypeName = serviceTypeName, pApplicationName = applicationName, pApplicationTypeName = applicationTypeName, pNodeName = nodeName, pMessage = message)
            {
                EventData* eventData = stackalloc EventData[numArgs];
                eventData[0] = new EventData { DataPointer = (IntPtr) pServiceName, Size = SizeInBytes(serviceName) };
                eventData[1] = new EventData { DataPointer = (IntPtr) pServiceTypeName, Size = SizeInBytes(serviceTypeName) };
                eventData[2] = new EventData { DataPointer = (IntPtr) (&replicaOrInstanceId), Size = sizeof(long) };
                eventData[3] = new EventData { DataPointer = (IntPtr) (&partitionId), Size = sizeof(Guid) };
                eventData[4] = new EventData { DataPointer = (IntPtr) pApplicationName, Size = SizeInBytes(applicationName) };
                eventData[5] = new EventData { DataPointer = (IntPtr) pApplicationTypeName, Size = SizeInBytes(applicationTypeName) };
                eventData[6] = new EventData { DataPointer = (IntPtr) pNodeName, Size = SizeInBytes(nodeName) };
                eventData[7] = new EventData { DataPointer = (IntPtr) pMessage, Size = SizeInBytes(message) };

                WriteEventCore(ServiceMessageEventId, numArgs, eventData);
            }
#endif
        }

        private const int ServiceTypeRegisteredEventId = 3;
        [Event(ServiceTypeRegisteredEventId, Level = EventLevel.Informational, Message = "Service host process {0} registered service type {1}", Keywords = Keywords.ServiceInitialization)]
        public void ServiceTypeRegistered(int hostProcessId, string serviceType)
        {
            WriteEvent(ServiceTypeRegisteredEventId, hostProcessId, serviceType);
        }

        private const int ServiceHostInitializationFailedEventId = 4;
        [Event(ServiceHostInitializationFailedEventId, Level = EventLevel.Error, Message = "Service host initialization failed", Keywords = Keywords.ServiceInitialization)]
        public void ServiceHostInitializationFailed(string exception)
        {
            WriteEvent(ServiceHostInitializationFailedEventId, exception);
        }

        // Ein Ereignispaar, das das gleiche Namenpräfix mit einem Suffix "Start"/"Stop" gemeinsam verwendet, markiert implizit Begrenzungen einer Ereignisnachverfolgungsaktivität.
        // Diese Aktivitäten können automatisch von Debug- und Profilerstellungstools abgerufen werden, um die Ausführungszeit, untergeordnete Aktivitäten,
        // und andere Statistiken zu berechnen.
        private const int ServiceRequestStartEventId = 5;
        [Event(ServiceRequestStartEventId, Level = EventLevel.Informational, Message = "Service request '{0}' started", Keywords = Keywords.Requests)]
        public void ServiceRequestStart(string requestTypeName)
        {
            WriteEvent(ServiceRequestStartEventId, requestTypeName);
        }

        private const int ServiceRequestStopEventId = 6;
        [Event(ServiceRequestStopEventId, Level = EventLevel.Informational, Message = "Service request '{0}' finished", Keywords = Keywords.Requests)]
        public void ServiceRequestStop(string requestTypeName, string exception = "")
        {
            WriteEvent(ServiceRequestStopEventId, requestTypeName, exception);
        }
        #endregion

        #region Private Methoden
        private static long GetReplicaOrInstanceId(ServiceContext context)
        {
            StatelessServiceContext stateless = context as StatelessServiceContext;
            if (stateless != null)
            {
                return stateless.InstanceId;
            }

            StatefulServiceContext stateful = context as StatefulServiceContext;
            if (stateful != null)
            {
                return stateful.ReplicaId;
            }

            throw new NotSupportedException("Context type not supported.");
        }
#if UNSAFE
        private int SizeInBytes(string s)
        {
            if (s == null)
            {
                return 0;
            }
            else
            {
                return (s.Length + 1) * sizeof(char);
            }
        }
#endif
        #endregion
    }
}
