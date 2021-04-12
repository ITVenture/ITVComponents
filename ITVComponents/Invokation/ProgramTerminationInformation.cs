namespace ITVComponents.Invokation
{
    /// <summary>
    /// Execution Summary of an external command-line application
    /// </summary>
    public class ProgramTerminationInformation
    {
        /// <summary>
        /// Gets or sets the ExitCode that was received from the executed program
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Gets or sets the entire console output from the executed program
        /// </summary>
        public string ConsoleOutput { get; set; }

        /// <summary>
        /// Gets or sets the entire error output from the executed program
        /// </summary>
        public string ErrorOutput { get; set; }
    }
}
