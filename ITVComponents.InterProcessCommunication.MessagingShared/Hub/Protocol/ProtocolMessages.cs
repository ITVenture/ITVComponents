using ITVComponents.InterProcessCommunication.MessagingShared.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol
{
    public class ServiceDiscoverMessage: IServiceMessage
    {
        public string TargetService { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public TimeSpan? Timeout { get; set; }
    }

    public class ServiceDiscoverResponseMessage: IServiceMessage
    {
        public string TargetService { get; set; }
        public bool Ok { get; set; }
        public string Reason { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public TimeSpan? Timeout { get; set; }
    }

    public class ServerOperationMessage: IServiceMessage
    {
        public string TargetService { get; set; }
        public string OperationId { get; set; }
        public string OperationPayload { get; set; }
        public string HubUser { get; set; }
        public bool TickBack { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public TimeSpan? Timeout { get; set; }
    }

    public class ServiceOperationResponseMessage: IServiceMessage, IResponderMessage
    {
        public string TargetService { get; set; }
        public string OperationId { get; set; }
        public string ResponsePayload { get; set; }
        public bool Ok { get; set; }
        public string ResponderFor { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public TimeSpan? Timeout { get; set; }
    }

    public class RegisterServiceMessage: IServerMessage
    {
        public string ServiceName { get; set; }
        public int Ttl { get; set; }
        public string ResponderFor { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public TimeSpan? Timeout { get; set; }
    }

    public class RegisterServiceResponseMessage:IProtocolMessage
    {
        public bool Ok { get; set; }
        public string SessionTicket { get; set; }
        public string Reason { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public TimeSpan? Timeout { get; set; }
    }

    public class ServiceSessionOperationMessage: IServerMessage
    {
        public string ServiceName { get; set; }
        public string SessionTicket { get; set; }
        public int Ttl { get; set; }
        public string ResponderFor { get; set; }
        public bool Tick { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public TimeSpan? Timeout { get; set; }
    }

    public class ServiceTickResponseMessage:IProtocolMessage
    {
        public bool Ok { get; set; }
        public string Reason { get; set; }
        public int PendingOperationsCount { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public TimeSpan? Timeout { get; set; }
    }

    [JsonPolymorphic]
    [JsonDerivedType(typeof(ServiceOperationResponseMessage), "ServiceOperationResponse")]
    [JsonDerivedType(typeof(ServerOperationMessage), "ServerOperationRequest")]
    [JsonDerivedType(typeof(ServiceDiscoverResponseMessage), "ServiceDiscoveryResponse")]
    [JsonDerivedType(typeof(ServiceDiscoverMessage), "ServiceDiscoveryRequest")]
    public interface IServiceMessage:IProtocolMessage
    {
        string TargetService { get; set; }
    }

    [JsonPolymorphic]
    [JsonDerivedType(typeof(ServiceSessionOperationMessage), "ServiceSessionOperationRequest")]
    [JsonDerivedType(typeof(RegisterServiceMessage), "RegisterServiceRequest")]
    public interface IServerMessage:IResponderMessage
    {
        string ServiceName { get; set; }

    }

    [JsonPolymorphic]
    [JsonDerivedType(typeof(ServiceSessionOperationMessage), "ServiceSessionOperationRequest")]
    [JsonDerivedType(typeof(RegisterServiceMessage), "RegisterServiceRequest")]
    [JsonDerivedType(typeof(ServiceOperationResponseMessage), "ServiceOperationResponse")]
    public interface IResponderMessage: IProtocolMessage
    {
        string ResponderFor { get; set; }
    }

    [JsonPolymorphic]
    [JsonDerivedType(typeof(ServiceSessionOperationMessage), "ServiceSessionOperationRequest")]
    [JsonDerivedType(typeof(RegisterServiceMessage), "RegisterServiceRequest")]
    [JsonDerivedType(typeof(ServiceOperationResponseMessage), "ServiceOperationResponse")]
    [JsonDerivedType(typeof(ServerOperationMessage), "ServerOperationRequest")]
    [JsonDerivedType(typeof(ServiceDiscoverResponseMessage), "ServiceDiscoveryResponse")]
    [JsonDerivedType(typeof(ServiceDiscoverMessage), "ServiceDiscoveryRequest")]

    public interface IProtocolMessage
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        TimeSpan? Timeout { get; set; }
    }
}
