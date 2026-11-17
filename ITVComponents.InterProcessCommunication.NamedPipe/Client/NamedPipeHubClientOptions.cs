using System;
using System.Security.Principal;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Client
{
    /// <summary>
    /// How a client reaches a hub over a named pipe.
    /// </summary>
    public class NamedPipeHubClientOptions
    {
        /// <summary>
        /// Gets or sets the name of the hub's pipe (without <c>\\.\pipe\</c>).
        /// </summary>
        public string PipeName { get; set; }

        /// <summary>
        /// Gets or sets the account that must own the pipe, e.g. <c>S-1-5-18</c> (LocalSystem) for a hub in a Windows
        /// service. Null skips the check.
        /// </summary>
        /// <remarks>
        /// The hub owns its pipe. Whoever creates a pipe of the same name before the hub runs owns that one - so the
        /// check tells the client whether it talks to the hub or to a process posing as it.
        /// </remarks>
        public SecurityIdentifier ExpectedServerOwner { get; set; }

        /// <summary>
        /// Gets or sets how long to wait for the pipe. Default: 2 seconds.
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Gets or sets how long to wait for the hub to answer a registration, a discovery or a tick. Default: 10 seconds.
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets how long to wait for a service to answer a call. Default: 5 minutes.
        /// </summary>
        public TimeSpan CallTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the largest message accepted, in bytes. Default: 4 MB.
        /// </summary>
        public int MaxMessageSize { get; set; } = 4 * 1024 * 1024;
    }
}
