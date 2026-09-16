using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub.Internal
{
    internal class ServiceStatus
    {
        private Dictionary<string, string> tags = new Dictionary<string, string>();

        public string ServiceName { get; set; }

        /// <summary>
        /// Der Mandant, in dem dieser Dienst registriert wurde - oder <c>null</c>, wenn er zu keinem
        /// gehoert.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Wird NICHT erzwungen.</b> Die Auswahl eines Dienstes laeuft weiter allein ueber den Namen;
        /// dieser Wert wird erfasst, nicht angewendet. Das ist Absicht: es gibt Dienste, die bewusst an
        /// keinen Mandanten gebunden sind und von mehreren in Anspruch genommen werden.
        /// </para>
        /// <para>
        /// <b>Wozu er dann da ist:</b> bevor sich eine Trennung durchsetzen laesst, muss man wissen, welche
        /// Dienste tatsaechlich mandantenuebergreifend benutzt werden - und welche bloss zufaellig
        /// erreichbar sind. Der Wert wird bei der Registrierung protokolliert und macht genau das sichtbar.
        /// </para>
        /// <para>
        /// <b>Er stammt aus der IDENTITAET des Registrierenden</b>, nie aus der Registrierungs-Nachricht -
        /// die kommt vom Client und koennte etwas anderes behaupten. Ein Dienst, der ueber einen Unter-Hub
        /// hereinkommt, traegt ihn heute noch nicht: der Weg ueber die Broker-Stellvertreter fuehrt ihn
        /// nicht mit.
        /// </para>
        /// </remarks>
        public string OwnerScope { get; set; }

        public DateTime LastPing { get; set; }

        public int Ttl { get; set; }

        public string RegistrationTicket { get; set; }

        public ServiceType ServiceKind{get;set;}

        public ILocalServiceClient LocalClient { get; set; }

        public TaskCompletionSource<OperationWaitHandle> OpenTaskWait { get; set; }

        public bool IsAlive
        {
            get
            {   
                var duration = DateTime.Now.Subtract(LastPing).TotalSeconds;
                return (ServiceKind == ServiceType.Local) || (duration < Ttl);
            }
        }

        public enum ServiceType
        {
            InterProcess,
            Local
        }

        public void SetTag(string tagName, string value)
        {
            if (tags.ContainsKey(tagName))
            {
                LogEnvironment.LogDebugEvent($"Replacing existing Tag-Value of {tagName}", LogSeverity.Warning);
            }

            tags[tagName] = value;
        }

        public string GetTag(string tagName)
        {
            string retVal = string.Empty;
            if (tags.ContainsKey(tagName))
            {
                retVal = tags[tagName];
            }

            return retVal;
        }
    }
}
