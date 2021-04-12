using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.Internal
{
    internal class ServiceStatus
    {
        public string ServiceName { get; set; }

        public DateTime LastPing { get; set; }

        public int Ttl { get; set; }

        public string RegistrationTicket { get; set; }

        public ServiceType ServiceKind{get;set;}

        public ILocalServiceClient LocalClient { get; set; }

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
            Grpc,
            Local
        }
    }
}
