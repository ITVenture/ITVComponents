namespace ITVComponents.MemberAccess
{
    /// <summary>
    /// The outcome of a read- or write-attempt on a <see cref="MemberSlot"/>.
    /// </summary>
    /// <remarks>
    /// The reason is reported instead of thrown, because the callers do not agree on the error
    /// form: the script-interpreter raises its own exception-types, the generic callers use
    /// <see cref="MemberAccessFailedException"/>.
    /// </remarks>
    public enum MemberAccessResult
    {
        /// <summary>
        /// The access succeeded.
        /// </summary>
        Ok,

        /// <summary>
        /// Neither a member nor a carrier serves the requested name.
        /// </summary>
        NotFound,

        /// <summary>
        /// The access is refused by the <see cref="IMemberAccessGuard"/>.
        /// </summary>
        Denied,

        /// <summary>
        /// The member exists but can not be read (a property without a getter).
        /// </summary>
        NotReadable,

        /// <summary>
        /// The member exists but can not be written (a property without a setter, a constant,
        /// a read-only carrier).
        /// </summary>
        NotWritable,

        /// <summary>
        /// The member exists but is of a kind this accessor does not handle.
        /// </summary>
        Unsupported
    }
}
