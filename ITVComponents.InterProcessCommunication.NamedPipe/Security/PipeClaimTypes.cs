namespace ITVComponents.InterProcessCommunication.NamedPipe.Security
{
    /// <summary>
    /// The claims of a caller identified through a named pipe. Every value comes from the operating system,
    /// none from the caller.
    /// </summary>
    /// <remarks>
    /// Next to these, the identity carries <see cref="System.Security.Claims.ClaimTypes.Name"/> (DOMAIN\user),
    /// <see cref="System.Security.Claims.ClaimTypes.PrimarySid"/>, one <see cref="System.Security.Claims.ClaimTypes.GroupSid"/>
    /// per group of the caller's token and one <see cref="GroupName"/> per group whose name could be resolved.
    /// </remarks>
    public static class PipeClaimTypes
    {
        /// <summary>
        /// The authentication type of a caller identified through a named pipe.
        /// </summary>
        public const string AuthenticationType = "NamedPipe";

        /// <summary>
        /// The Windows session the calling process runs in.
        /// </summary>
        public const string SessionId = "itv:namedpipe:sessionid";

        /// <summary>
        /// The id of the calling process.
        /// </summary>
        public const string ProcessId = "itv:namedpipe:processid";

        /// <summary>
        /// The full path of the calling process' image. Missing when it could not be determined.
        /// </summary>
        public const string ProcessPath = "itv:namedpipe:processpath";

        /// <summary>
        /// The name of a group of the caller (<c>NT AUTHORITY\SERVICE</c>, <c>MYDOMAIN\Kassen</c>).
        /// </summary>
        public const string GroupName = "itv:namedpipe:groupname";

        /// <summary>
        /// The id of the pipe connection, unique within the hub's lifetime.
        /// </summary>
        public const string ConnectionId = "itv:namedpipe:connectionid";

        /// <summary>
        /// A back channel the caller registered for its events over this connection (one claim per channel).
        /// </summary>
        public const string BackChannel = "itv:namedpipe:backchannel";
    }
}
