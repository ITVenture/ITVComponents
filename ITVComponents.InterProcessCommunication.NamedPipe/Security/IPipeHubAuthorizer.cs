using System.Security.Claims;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Security
{
    /// <summary>
    /// Decides what a pipe client may do at the hub. Without one, the hub is open: everyone the pipe's ACL lets in may
    /// call every service.
    /// </summary>
    /// <remarks>
    /// The permissions are the same as at the other authenticated hubs (in-memory, gRPC):
    /// <list type="bullet">
    /// <item><see cref="PipeHubPermissions.ConnectAnyService"/> or a permission named like the target service: call a service, look it up,
    /// register a back channel for its events;</item>
    /// <item><see cref="PipeHubPermissions.ActAsService"/>: register a service of its own (only if the hub allows services from pipe clients
    /// at all).</item>
    /// </list>
    /// </remarks>
    public interface IPipeHubAuthorizer
    {
        /// <summary>
        /// Indicates whether the caller is known at all. An unknown caller is disconnected right away.
        /// </summary>
        /// <param name="caller">the identity the hub read from the operating system</param>
        bool IsAuthenticated(ClaimsIdentity caller);

        /// <summary>
        /// Indicates whether the caller has at least one of the given permissions.
        /// </summary>
        /// <param name="caller">the identity the hub read from the operating system</param>
        /// <param name="permissions">the permissions, any of which suffices</param>
        bool HasAnyPermission(ClaimsIdentity caller, params string[] permissions);
    }

    /// <summary>
    /// The permissions a pipe hub checks.
    /// </summary>
    public static class PipeHubPermissions
    {
        /// <summary>
        /// Call any service, look it up and register a back channel for its events.
        /// </summary>
        public const string ConnectAnyService = "ConnectAnyService";

        /// <summary>
        /// Register a service of one's own.
        /// </summary>
        public const string ActAsService = "ActAsService";
    }
}
