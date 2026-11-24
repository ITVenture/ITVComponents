namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    public class SetPropertyRequestMessage:InvokeMethodRequestMessage
    {
        /// <summary>
        /// The new value as the runtime serializer writes it, without its type: a server receives a JsonElement. Kept for
        /// servers that do not know <see cref="PackedValue"/>.
        /// </summary>
        public object Value { get;set; }

        /// <summary>
        /// The new value packed like the arguments of a method (an argument list with one element), so it keeps its type -
        /// also a proxy. A server prefers it over <see cref="Value"/> when it is present.
        /// </summary>
        public string PackedValue { get; set; }
    }
}
