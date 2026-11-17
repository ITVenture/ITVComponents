using System;
using System.Runtime.Versioning;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Factory;
using ITVComponents.InterProcessCommunication.Shared.Security;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Client
{
    /// <summary>
    /// Creates the pipe connections of a <see cref="NamedPipeClient"/> or of a <see cref="Server.NamedPipeServer"/>
    /// outside the hub's process.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class NamedPipeHubConnectionFactory : IHubConnectionFactory
    {
        private readonly NamedPipeHubClientOptions options;
        private readonly string serviceName;
        private readonly bool useEvents;
        private readonly ICustomServerSecurity security;
        private readonly bool clientMode;

        /// <summary>
        /// Initializes a factory for the connections of a client.
        /// </summary>
        /// <param name="options">how to reach the hub</param>
        /// <param name="targetService">the service the client calls</param>
        /// <param name="useEvents">whether the client registers a back channel to receive events</param>
        public NamedPipeHubConnectionFactory(NamedPipeHubClientOptions options, string targetService, bool useEvents)
        {
            this.options = options;
            serviceName = targetService;
            this.useEvents = useEvents;
            clientMode = true;
        }

        /// <summary>
        /// Initializes a factory for the connections of a service.
        /// </summary>
        /// <param name="options">how to reach the hub</param>
        /// <param name="serviceName">the name the service registers</param>
        /// <param name="security">transforms the identity of the service's callers</param>
        public NamedPipeHubConnectionFactory(NamedPipeHubClientOptions options, string serviceName, ICustomServerSecurity security)
        {
            this.options = options;
            this.serviceName = serviceName;
            this.security = security;
            clientMode = false;
        }

        /// <inheritdoc />
        public IHubConnection CreateConnection()
        {
            if (!clientMode)
            {
                return new NamedPipeServiceHubConsumer(options, serviceName, null, security);
            }

            // The back channel is named by a GUID only: the pipe hub refuses any other name for it.
            return useEvents
                ? new NamedPipeServiceHubConsumer(options, Guid.NewGuid().ToString(), serviceName, null)
                : new NamedPipeServiceHubConsumer(options, null, serviceName, null);
        }
    }
}
