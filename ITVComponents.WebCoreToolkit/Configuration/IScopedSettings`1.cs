namespace ITVComponents.WebCoreToolkit.Configuration
{
    /// <summary>
    /// Injects Scope-driven settings with a specific settings-type
    /// </summary>
    /// <typeparam name="TSettings">the demanded settings-type</typeparam>
    public interface IScopedSettings<TSettings> where TSettings : class, new()
    {
        /// <summary>
        /// Gets the deserialized Settings-value. If it is not configured, an object is constructed, using the Default-Constructor.
        /// </summary>
        TSettings Value { get; }

        /// <summary>
        /// Gets the deserialized Settings-value. If it is not configured, null is returned (-> default(TSettings)).
        /// </summary>
        TSettings ValueOrDefault { get; }

        /// <summary>
        /// Sets the value of this settings item
        /// </summary>
        /// <param name="newValue">the value to write for the setting represented by this object</param>
        void Update(TSettings newValue);

        /// <summary>
        /// Sets the value of this settings item and encrypts string values that are prefixed with "encrypt:"
        /// </summary>
        /// <param name="newValue">the value to write for the setting represented by this object</param>
        /// <param name="useTenantEncryption">indicates whether to use tenant-driven encryption for writing the settings</param>
        void Update(TSettings newValue, bool useTenantEncryption);
    }
}
