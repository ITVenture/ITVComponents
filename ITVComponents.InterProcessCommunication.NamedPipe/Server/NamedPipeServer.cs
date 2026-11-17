using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Security.Claims;
using System.Security.Principal;
using ITVComponents.InterProcessCommunication.NamedPipe.Security;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Server;
using ITVComponents.InterProcessCommunication.NamedPipe.Client;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Server
{
    /// <summary>
    /// Offers objects to the clients of a pipe hub (<see cref="Hub.NamedPipeHubEndPoint"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Usually the service lives in the hub's process and registers with the hub directly (the constructors taking
    /// an <see cref="IServiceHubProvider"/>). A service in another process connects through the pipe; the hub must
    /// allow that (<see cref="Hub.NamedPipeHubOptions.AllowRemoteServices"/>).
    /// </para>
    /// <para>
    /// Unlike the other message servers it is strict by default:
    /// <list type="bullet">
    /// <item><c>TrustClientIdentity = false</c>: a call runs under the identity the hub took from the pipe; what a
    /// client claims in its request is ignored.</item>
    /// <item><c>RestrictArgumentTypes = true</c>: arguments only of registered types (the basic types are).</item>
    /// </list>
    /// Expose objects whose public surface is exactly what a client may do: no settable properties and no other public
    /// methods - every public member can be called. Do not use extended proxying for them.
    /// </para>
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public class NamedPipeServer : MessageServer
    {
        /// <summary>
        /// Offers the given objects as a service in the hub's process.
        /// </summary>
        /// <param name="serviceHub">the hub</param>
        /// <param name="exposedObjects">the objects, by name</param>
        /// <param name="serviceName">the name of the service</param>
        public NamedPipeServer(IServiceHubProvider serviceHub, IDictionary<string, object> exposedObjects, string serviceName)
            : this(serviceHub, exposedObjects, serviceName, false, false, null)
        {
        }

        /// <summary>
        /// Offers the given objects as a service in the hub's process.
        /// </summary>
        /// <param name="serviceHub">the hub</param>
        /// <param name="exposedObjects">the objects, by name</param>
        /// <param name="serviceName">the name of the service</param>
        /// <param name="useExtendedProxying">whether returned interfaces become remote objects themselves</param>
        /// <param name="useSecurity">whether objects requiring security may be used</param>
        /// <param name="security">checks every call</param>
        public NamedPipeServer(IServiceHubProvider serviceHub, IDictionary<string, object> exposedObjects, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security)
            : base(serviceHub, exposedObjects, serviceName, useExtendedProxying, useSecurity, security)
        {
            Harden();
        }

        /// <summary>
        /// Offers the plugins of a factory as a service in the hub's process.
        /// </summary>
        /// <param name="serviceHub">the hub</param>
        /// <param name="factory">the plugins</param>
        /// <param name="serviceName">the name of the service</param>
        /// <param name="useExtendedProxying">whether returned interfaces become remote objects themselves</param>
        /// <param name="useSecurity">whether objects requiring security may be used</param>
        /// <param name="security">checks every call</param>
        public NamedPipeServer(IServiceHubProvider serviceHub, PluginFactory factory, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security)
            : base(serviceHub, factory, serviceName, useExtendedProxying, useSecurity, security)
        {
            Harden();
        }

        /// <summary>
        /// Offers the given objects as a service in another process, connected through the hub's pipe.
        /// </summary>
        /// <param name="options">how to reach the hub</param>
        /// <param name="exposedObjects">the objects, by name</param>
        /// <param name="serviceName">the name of the service</param>
        /// <param name="useExtendedProxying">whether returned interfaces become remote objects themselves</param>
        /// <param name="useSecurity">whether objects requiring security may be used</param>
        /// <param name="security">checks every call</param>
        public NamedPipeServer(NamedPipeHubClientOptions options, IDictionary<string, object> exposedObjects, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security)
            : base(new NamedPipeHubConnectionFactory(options, serviceName, security), exposedObjects, useExtendedProxying, useSecurity, security)
        {
            Harden();
        }

        /// <summary>
        /// Offers the given objects as a service in another process, connected through the hub's pipe.
        /// </summary>
        /// <param name="pipeName">the name of the hub's pipe</param>
        /// <param name="exposedObjects">the objects, by name</param>
        /// <param name="serviceName">the name of the service</param>
        public NamedPipeServer(string pipeName, IDictionary<string, object> exposedObjects, string serviceName)
            : this(new NamedPipeHubClientOptions { PipeName = pipeName }, exposedObjects, serviceName, false, false, null)
        {
        }

        /// <summary>
        /// A caller that came through the pipe may only subscribe or cancel events for a back channel it registered over
        /// its own connection.
        /// </summary>
        /// <remarks>
        /// A caller of another transport on the same hub (in-process, in-memory, gRPC) carries no pipe identity; its own hub
        /// is in charge of it, as for every other message server.
        /// </remarks>
        protected override bool MayUseEventChannel(string respondChannel, IIdentity caller)
        {
            if (caller is not ClaimsIdentity claims || claims.AuthenticationType != PipeClaimTypes.AuthenticationType)
            {
                return true;
            }

            return !string.IsNullOrEmpty(respondChannel) && claims.HasClaim(PipeClaimTypes.BackChannel, respondChannel);
        }

        private void Harden()
        {
            TrustClientIdentity = false;
            RestrictArgumentTypes = true;
        }
    }
}
