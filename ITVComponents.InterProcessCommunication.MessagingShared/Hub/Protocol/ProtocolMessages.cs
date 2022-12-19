using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol
{
    [Serializable]
    public class ServiceDiscoverMessage: IServiceMessage
    {
        public string TargetService { get; set; }
    }

    [Serializable]
    public class ServiceDiscoverResponseMessage: IServiceMessage
    {
        public string TargetService { get; set; }
        public bool Ok { get; set; }
        public string Reason { get; set; }
    }

    [Serializable]
    public class ServerOperationMessage: IServiceMessage
    {
        public string TargetService { get; set; }
        public string OperationId { get; set; }
        public string OperationPayload { get; set; }
        public string HubUser { get; set; }
        public bool TickBack { get; set; }
    }

    [Serializable]
    public class ServiceOperationResponseMessage: IServiceMessage, IResponderMessage
    {
        public string TargetService { get; set; }
        public string OperationId { get; set; }
        public string ResponsePayload { get; set; }
        public bool Ok { get; set; }
        public string ResponderFor { get; set; }
    }

    [Serializable]
    public class RegisterServiceMessage: IServerMessage
    {
        public string ServiceName { get; set; }
        public int Ttl { get; set; }
        public string ResponderFor { get; set; }
    }

    [Serializable]
    public class RegisterServiceResponseMessage
    {
        public bool Ok { get; set; }
        public string SessionTicket { get; set; }
        public string Reason { get; set; }
    }

    [Serializable]
    public class ServiceSessionOperationMessage: IServerMessage
    {
        public string ServiceName { get; set; }
        public string SessionTicket { get; set; }
        public int Ttl { get; set; }
        public string ResponderFor { get; set; }
        public bool Tick { get; set; }
    }

    [Serializable]
    public class ServiceTickResponseMessage
    {
        public bool Ok { get; set; }
        public string Reason { get; set; }
        public int PendingOperationsCount { get; set; }
    }

    public interface IServiceMessage
    {
        string TargetService { get; set; }
    }

    public interface IServerMessage:IResponderMessage
    {
        string ServiceName { get; set; }

    }

    public interface IResponderMessage
    {
        string ResponderFor { get; set; }
    }
}
