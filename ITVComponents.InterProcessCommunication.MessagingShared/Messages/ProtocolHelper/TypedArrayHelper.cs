using ITVComponents.Json;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages.ProtocolHelper
{
    /// <summary>
    /// Packs method arguments and results for the wire. Every element is an open place
    /// (<see cref="SerializationTypingMode.EnvelopePolymorphism"/>): it carries its runtime type, on every level, and
    /// primitives keep their exact type (an int parameter does not arrive as long).
    /// </summary>
    internal static class TypedArrayHelper
    {
        public static string PackArguments(this object[] arguments)
        {
            // Missing arguments travel as an empty list, as with the former container: the receiver never gets null.
            return JsonHelper.ToJson(arguments ?? System.Array.Empty<object>(), SerializationTypingMode.EnvelopePolymorphism);
        }

        public static object[] UnpackArguments(this string arguments)
        {
            return JsonHelper.FromJsonString<object[]>(arguments, SerializationTypingMode.EnvelopePolymorphism);
        }
    }
}
