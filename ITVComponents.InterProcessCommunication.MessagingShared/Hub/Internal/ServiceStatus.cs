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
