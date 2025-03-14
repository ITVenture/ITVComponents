using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    [JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
    [JsonDerivedType(typeof(ObjectAvailabilityRequestMessage),"ObjectAvailabilityRequest")]
    [JsonDerivedType(typeof(AbandonExtendedProxyRequestMessage), "AbandonExtProxyRequest")]
    [JsonDerivedType(typeof(GetPropertyRequestMessage), "GetPropertyRequest")]
    [JsonDerivedType(typeof(SetPropertyRequestMessage), "SetPropertyRequest")]
    [JsonDerivedType(typeof(InvokeMethodRequestMessage), "InvokeMethodRequest")]
    [JsonDerivedType(typeof(UnRegisterEventRequestMessage), "UnregisterEventRequest")]
    [JsonDerivedType(typeof(RegisterEventRequestMessage), "RegisterEventRequest")]
    [JsonDerivedType(typeof(EventNotificationMessage), "EventRaiseCommit")]
    public interface IRequestMessage
    {
    }
}
