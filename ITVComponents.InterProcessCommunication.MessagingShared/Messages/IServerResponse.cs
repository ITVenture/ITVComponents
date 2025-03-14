using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    [JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
    [JsonDerivedType(typeof(EventNotificationMessage), "EventNotificationResponse")]
    [JsonDerivedType(typeof(ErrorResponse), "ErrorResponse")]
    [JsonDerivedType(typeof(AbandonExtendedProxyResponseMessage), "AbandonExtendedProxyResponse")]
    [JsonDerivedType(typeof(ObjectAvailabilityResponseMessage), "ObjectAvailabilityResponse")]
    [JsonDerivedType(typeof(SetPropertyResponseMessage), "SetPropertyResponse")]
    [JsonDerivedType(typeof(GetPropertyResponseMessage), "GetPropertyResponse")]
    [JsonDerivedType(typeof(InvokeMethodResponseMessage), "InvokeMethodResponse")]
    [JsonDerivedType(typeof(UnRegisterEventResponseMessage), "UnRegisterEventResponse")]
    [JsonDerivedType(typeof(RegisterEventResponseMessage), "RegisterEventResponse")]
    public interface IServerResponse
    {
    }
}
