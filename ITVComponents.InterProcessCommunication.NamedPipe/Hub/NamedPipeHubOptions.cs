using System.Collections.Generic;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Hub
{
    /// <summary>
    /// The settings of a <see cref="NamedPipeHubEndPoint"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class NamedPipeHubOptions
    {
        /// <summary>
        /// Gets or sets the name of the pipe (without <c>\\.\pipe\</c>).
        /// </summary>
        public string PipeName { get; set; }

        /// <summary>
        /// Gets or sets the accounts or groups the pipe's ACL lets in. Empty means every authenticated account
        /// (see <see cref="Security.PipeAccessControl.DefaultClients"/>); what each of them may do is up to the
        /// <see cref="Authorizer"/>.
        /// </summary>
        public IList<SecurityIdentifier> AllowedClients { get; set; } = new List<SecurityIdentifier>();

        /// <summary>
        /// Gets or sets a value indicating whether a pipe client may register a service of its own that others can
        /// call. Default: false - a pipe client only consumes the services that are registered in the hub's process,
        /// and it may register nothing but the back channel for its events.
        /// </summary>
        public bool AllowRemoteServices { get; set; }

        /// <summary>
        /// Gets or sets who may do what at the hub. Null means open: whoever the ACL lets in may call every service.
        /// </summary>
        public Security.IPipeHubAuthorizer Authorizer { get; set; }

        /// <summary>
        /// Gets or sets the largest message accepted, in bytes. Default: 4 MB.
        /// </summary>
        public int MaxMessageSize { get; set; } = 4 * 1024 * 1024;

        /// <summary>
        /// Gets or sets the number of simultaneous connections. Default: <see cref="NamedPipeServerStream.MaxAllowedServerInstances"/>.
        /// </summary>
        public int MaxConnections { get; set; } = NamedPipeServerStream.MaxAllowedServerInstances;
    }
}
