namespace ITVComponents.MemberAccess
{
    /// <summary>
    /// Describes what a <see cref="MemberSlot"/> has actually found for the requested name.
    /// </summary>
    public enum MemberSlotKind
    {
        /// <summary>
        /// Neither a member nor a carrier that could serve the name.
        /// </summary>
        None,

        /// <summary>
        /// A property of the target type.
        /// </summary>
        Property,

        /// <summary>
        /// A field of the target type.
        /// </summary>
        Field,

        /// <summary>
        /// An event of the target type. Reading yields null; subscribing is not part of the
        /// generic access - that decision belongs to the caller.
        /// </summary>
        Event,

        /// <summary>
        /// A named value of an enum-type.
        /// </summary>
        EnumValue,

        /// <summary>
        /// The target carries the name in a string-keyed dictionary.
        /// </summary>
        Dictionary,

        /// <summary>
        /// The target carries the name in a read-only <see cref="ExtendedFormatting.IBasicKeyValueProvider"/>.
        /// </summary>
        KeyValueProvider,

        /// <summary>
        /// The target serves the name itself through <see cref="IMemberSource"/>.
        /// </summary>
        MemberSource,

        /// <summary>
        /// A member was found, but it is of a kind this accessor can not read or write
        /// (a method, a constructor, a nested type).
        /// </summary>
        Unsupported
    }
}
