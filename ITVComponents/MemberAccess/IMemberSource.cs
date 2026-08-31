namespace ITVComponents.MemberAccess
{
    /// <summary>
    /// Implemented by carriers that serve named values themselves instead of through properties,
    /// fields or a dictionary. Checked before the built-in carriers, so an implementation stays
    /// in control of its own names.
    /// </summary>
    public interface IMemberSource
    {
        /// <summary>
        /// Gets a value indicating whether this source carries the given name
        /// </summary>
        /// <param name="name">the name for which to check</param>
        /// <returns>a value indicating whether the name is present</returns>
        bool ContainsMember(string name);

        /// <summary>
        /// Gets the value that is bound to the given name
        /// </summary>
        /// <param name="name">the name of the requested value</param>
        /// <returns>the value bound to the given name</returns>
        object GetMember(string name);

        /// <summary>
        /// Binds a value to the given name
        /// </summary>
        /// <param name="name">the name to which to bind the value</param>
        /// <param name="value">the value to bind</param>
        void SetMember(string name, object value);
    }
}
