namespace ITVComponents.WebCoreToolkit.Configuration
{
    /// <summary>
    /// Injects Scope-driven settings with a specific settings-type
    /// </summary>
    /// <typeparam name="TSettings">the demanded settings-type</typeparam>
    public interface IScopedSettings<TSettings> where TSettings : class, new()
    {
        /// <summary>
        /// Gets the deserialized Settings-value
        /// </summary>
        TSettings Value { get; }
    }
}
