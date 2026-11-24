namespace ITVComponents.Json.Contracts
{
    /// <summary>
    /// One named entry of an <see cref="IManualSerializer"/>. Written by the envelope bridge as a property of its own,
    /// the value carrying its type in its envelope.
    /// </summary>
    public class ManualSerializationData
    {
        /// <summary>The name of the entry (the property name in the written object).</summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Die Typkennung des Werts: der ueber <see cref="ManualTypeRegistry"/> registrierte <b>Kurzname</b>, sonst der
        /// Typname (siehe <see cref="FromValue"/>). Nur zur Information - geschrieben wird der Typ in der Huelle des
        /// Werts.
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// The value. After reading it has its exact type, or is a <c>JsonNode</c> when its type could not be resolved
        /// (not registered under the restriction, renamed or removed - the reason is in the log).
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// Baut einen Eintrag aus einem Wert. Die Typkennung ist der registrierte Kurzname des
        /// Laufzeittyps, ersatzweise sein Typname.
        /// </summary>
        public static ManualSerializationData FromValue(string propertyName, object value)
        {
            return new ManualSerializationData
            {
                PropertyName = propertyName,
                TypeName = ManualTypeRegistry.NameOf(value?.GetType()),
                Data = value
            };
        }
    }
}
