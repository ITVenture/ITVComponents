using System.Runtime.Versioning;
using System.Security.Principal;
using ITVComponents.InterProcessCommunication.MessagingShared.Client;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Client
{
    /// <summary>
    /// Calls the objects of a service behind a hub that listens on a named pipe
    /// (<see cref="Hub.NamedPipeHubEndPoint"/>).
    /// </summary>
    /// <remarks>
    /// The client sends no identity: the hub learns from the operating system who is calling.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public class NamedPipeClient : MessageClient
    {
        /// <summary>
        /// Initializes a new client.
        /// </summary>
        /// <param name="pipeName">the name of the hub's pipe</param>
        /// <param name="targetService">the service to call</param>
        /// <param name="useEvents">whether to receive the service's events</param>
        public NamedPipeClient(string pipeName, string targetService, bool useEvents)
            : this(new NamedPipeHubClientOptions { PipeName = pipeName }, targetService, useEvents)
        {
        }

        /// <summary>
        /// Initializes a new client that checks who owns the pipe.
        /// </summary>
        /// <param name="pipeName">the name of the hub's pipe</param>
        /// <param name="targetService">the service to call</param>
        /// <param name="useEvents">whether to receive the service's events</param>
        /// <param name="expectedServerOwnerSid">the SID that must own the pipe, e.g. <c>S-1-5-18</c> for a hub running as LocalSystem</param>
        public NamedPipeClient(string pipeName, string targetService, bool useEvents, string expectedServerOwnerSid)
            : this(new NamedPipeHubClientOptions
            {
                PipeName = pipeName,
                ExpectedServerOwner = string.IsNullOrWhiteSpace(expectedServerOwnerSid) ? null : new SecurityIdentifier(expectedServerOwnerSid)
            }, targetService, useEvents)
        {
        }

        /// <summary>
        /// Initializes a new client.
        /// </summary>
        /// <param name="options">how to reach the hub</param>
        /// <param name="targetService">the service to call</param>
        /// <param name="useEvents">whether to receive the service's events</param>
        public NamedPipeClient(NamedPipeHubClientOptions options, string targetService, bool useEvents)
            : base(new NamedPipeHubConnectionFactory(options, targetService, useEvents), targetService, null, useEvents)
        {
        }
    }
}
