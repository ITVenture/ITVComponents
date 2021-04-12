namespace ITVComponents.Logging.DefaultLoggers
{
    public interface ILogProxy
    {
        /// <summary>
        /// Gets or sets a value indicating whether logging is currently enabled
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the minimal Severity of the logger
        /// </summary>
        int MinSeverity { get; set; }

        /// <summary>
        /// Gets or sets the maximum severity of the logger
        /// </summary>
        int MaxSeverity { get; set; }
    }
}
