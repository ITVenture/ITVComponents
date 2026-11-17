using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Security
{
    /// <summary>
    /// The access control list of a hub pipe.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class PipeAccessControl
    {
        /// <summary>
        /// The rights of a client: read and write - but never <see cref="PipeAccessRights.CreateNewInstance"/>,
        /// otherwise a client could open an instance of the pipe itself and pose as the hub.
        /// </summary>
        public const PipeAccessRights ClientRights = PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize;

        /// <summary>
        /// The clients allowed by default: every authenticated account - users at the console or over remote desktop as
        /// well as services, scheduled tasks and web applications under their own accounts.
        /// </summary>
        /// <remarks>
        /// The pipe only keeps out what must be kept out by the kernel (anonymous and network access, new instances).
        /// Who may do what is decided by the hub's authorization (<see cref="Hub.NamedPipeHubOptions.Authorizer"/>).
        /// </remarks>
        public static IReadOnlyList<SecurityIdentifier> DefaultClients { get; } = new[]
        {
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null)
        };

        /// <summary>
        /// Builds the ACL of a hub pipe.
        /// </summary>
        /// <param name="allowedClients">the accounts or groups that may connect; null or empty means <see cref="DefaultClients"/></param>
        /// <returns>the security of the pipe</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item>The account running the hub owns the pipe and has full control. A client can check the owner
        /// (<see cref="Client.NamedPipeHubClientOptions.ExpectedServerOwner"/>) and so make sure that it talks to the
        /// hub and not to someone who created the pipe before it.</item>
        /// <item>SYSTEM and the administrators have full control.</item>
        /// <item>The allowed clients may read and write (<see cref="ClientRights"/>).</item>
        /// <item>Access over the network is denied, whatever else is allowed. This also catches a local process whose
        /// thread impersonates a network logon (a web application acting for its user) - which is why
        /// <see cref="Client.NamedPipeServiceHubConsumer"/> always opens the pipe under its process' own account.</item>
        /// </list>
        /// </remarks>
        public static PipeSecurity Build(IEnumerable<SecurityIdentifier> allowedClients)
        {
            var owner = WindowsIdentity.GetCurrent().User
                        ?? throw new InvalidOperationException("The account running the hub has no SID.");
            var security = new PipeSecurity();
            security.SetOwner(owner);
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
            security.AddAccessRule(new PipeAccessRule(owner, PipeAccessRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
            var clients = allowedClients?.ToArray();
            foreach (var client in clients is { Length: > 0 } ? clients : DefaultClients)
            {
                security.AddAccessRule(new PipeAccessRule(client, ClientRights, AccessControlType.Allow));
            }

            return security;
        }

        /// <summary>
        /// Parses a list of accounts or groups, separated by semicolons or commas - each as a SID (<c>S-1-5-6</c>) or as
        /// a name (<c>NT AUTHORITY\SERVICE</c>, <c>MYDOMAIN\Kassen</c>).
        /// </summary>
        /// <param name="accounts">the list; null or empty gives an empty list</param>
        /// <returns>the SIDs</returns>
        /// <remarks>
        /// Prefer SIDs for the built-in groups: their names depend on the language of Windows
        /// (<c>BUILTIN\Users</c> is <c>VORDEFINIERT\Benutzer</c> on a German system).
        /// </remarks>
        /// <exception cref="IdentityNotMappedException">when a name does not exist on this machine</exception>
        public static IReadOnlyList<SecurityIdentifier> ParseSids(string accounts)
        {
            if (string.IsNullOrWhiteSpace(accounts))
            {
                return Array.Empty<SecurityIdentifier>();
            }

            return accounts.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ToSid)
                .ToArray();
        }

        private static SecurityIdentifier ToSid(string account)
        {
            if (account.StartsWith("S-", StringComparison.OrdinalIgnoreCase))
            {
                return new SecurityIdentifier(account);
            }

            return (SecurityIdentifier)new NTAccount(account).Translate(typeof(SecurityIdentifier));
        }
    }
}
