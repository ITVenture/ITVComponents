using System;
using System.Text.Json.Serialization;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Channels
{
    /// <summary>
    /// A message that expects an answer (<see cref="PipeResponse"/> with the same id).
    /// </summary>
    /// <remarks>
    /// Unlike its in-memory counterpart it carries no identity: the hub takes the identity from the pipe.
    /// </remarks>
    public class PipeRequest : IProtocolMessage
    {
        public string RequestId { get; set; }

        public IProtocolMessage Payload { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public TimeSpan? Timeout { get; set; }
    }

    /// <summary>
    /// The answer to a <see cref="PipeRequest"/>.
    /// </summary>
    public class PipeResponse : IProtocolMessage
    {
        public string RequestId { get; set; }

        public IProtocolMessage Payload { get; set; }

        /// <summary>
        /// Set when the other side could not process the request; the payload is empty then.
        /// </summary>
        public string Error { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public TimeSpan? Timeout { get; set; }
    }

    /// <summary>
    /// Announces that the sender closes the connection.
    /// </summary>
    public class PipeDisconnect : IProtocolMessage
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public TimeSpan? Timeout { get; set; }
    }
}
