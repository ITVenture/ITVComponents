using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.Claims;
using System.Security.Principal;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.NamedPipe.Native;
using ITVComponents.Logging;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Security
{
    /// <summary>
    /// Who is at the client end of a pipe connection - as the kernel reports it.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class PipeClientIdentity
    {
        private PipeClientIdentity(string connectionId, string userName, SecurityIdentifier userSid, int sessionId, int processId, string processPath, IReadOnlyList<PipeClientGroup> groups)
        {
            Groups = groups;
            ConnectionId = connectionId;
            UserName = userName;
            UserSid = userSid;
            SessionId = sessionId;
            ProcessId = processId;
            ProcessPath = processPath;
        }

        /// <summary>
        /// Gets the groups of the caller's token (enabled groups only, as Windows evaluates them for access).
        /// </summary>
        public IReadOnlyList<PipeClientGroup> Groups { get; }

        /// <summary>
        /// Gets the id of the pipe connection.
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// Gets the account of the caller (DOMAIN\user).
        /// </summary>
        public string UserName { get; }

        /// <summary>
        /// Gets the SID of the caller's account.
        /// </summary>
        public SecurityIdentifier UserSid { get; }

        /// <summary>
        /// Gets the Windows session the calling process runs in.
        /// </summary>
        public int SessionId { get; }

        /// <summary>
        /// Gets the id of the calling process.
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// Gets the full path of the calling process' image, or null when it could not be determined.
        /// </summary>
        public string ProcessPath { get; }

        /// <summary>
        /// Builds the claims identity the hub hands to the services.
        /// </summary>
        public ClaimsIdentity ToClaimsIdentity()
        {
            return ToClaimsIdentity(Array.Empty<string>());
        }

        /// <summary>
        /// Builds the claims identity the hub hands to the services, naming the back channels of the connection.
        /// </summary>
        /// <param name="backChannels">the back channels the client registered over this connection</param>
        public ClaimsIdentity ToClaimsIdentity(System.Collections.Generic.IEnumerable<string> backChannels)
        {
            var retVal = new ClaimsIdentity(PipeClaimTypes.AuthenticationType, ClaimTypes.Name, ClaimTypes.Role);
            retVal.AddClaim(new Claim(ClaimTypes.Name, UserName));
            retVal.AddClaim(new Claim(ClaimTypes.PrimarySid, UserSid.Value));
            retVal.AddClaim(new Claim(PipeClaimTypes.SessionId, SessionId.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer32));
            retVal.AddClaim(new Claim(PipeClaimTypes.ProcessId, ProcessId.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer32));
            retVal.AddClaim(new Claim(PipeClaimTypes.ConnectionId, ConnectionId));
            foreach (var group in Groups)
            {
                retVal.AddClaim(new Claim(ClaimTypes.GroupSid, group.Sid.Value));
                if (!string.IsNullOrEmpty(group.Name))
                {
                    retVal.AddClaim(new Claim(PipeClaimTypes.GroupName, group.Name));
                }
            }
            if (!string.IsNullOrEmpty(ProcessPath))
            {
                retVal.AddClaim(new Claim(PipeClaimTypes.ProcessPath, ProcessPath));
            }

            foreach (var backChannel in backChannels)
            {
                retVal.AddClaim(new Claim(PipeClaimTypes.BackChannel, backChannel));
            }

            return retVal;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{UserName} (session {SessionId}, process {ProcessId}, connection {ConnectionId})";
        }

        /// <summary>
        /// Reads the identity of the client that just connected to a server pipe.
        /// </summary>
        /// <param name="pipe">the connected server end</param>
        /// <param name="connectionId">the id the hub gives this connection</param>
        /// <returns>the identity of the client</returns>
        /// <exception cref="UnauthorizedAccessException">when the client cannot be identified (e.g. it connected anonymously)</exception>
        internal static PipeClientIdentity Read(NamedPipeServerStream pipe, string connectionId)
        {
            // The user comes from the token of the client PROCESS, not from impersonating the client: no impersonation
            // (the client may connect anonymously, the hub can never act as it), and nothing of the hub runs under a
            // foreign token - with an identification-level token, loading an assembly fails, and the runtime remembers
            // that failure. The process id is safe to use: the kernel reports it, and while the process holds the pipe
            // open, its id cannot be given to another process.
            var handle = pipe.SafePipeHandle;
            var processId = (int)PipeNativeMethods.ClientProcessId(handle);
            var sessionId = (int)PipeNativeMethods.ClientSessionId(handle);
            string userName;
            SecurityIdentifier userSid;
            IReadOnlyList<PipeClientGroup> groups;
            try
            {
                using var user = PipeNativeMethods.ProcessUser((uint)processId);
                userName = user.Name;
                userSid = user.User;
                groups = GroupsOf(user, processId);
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException($"The account of pipe client process {processId} could not be read.", ex);
            }

            if (userSid == null)
            {
                throw new UnauthorizedAccessException($"Pipe client process {processId} has no user SID.");
            }
            string processPath = null;
            try
            {
                processPath = PipeNativeMethods.ProcessImagePath((uint)processId);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The image path of pipe client process {processId} ({userName}) could not be read; the identity goes without it: {ex.OutlineException()}", LogSeverity.Warning);
            }

            return new PipeClientIdentity(connectionId, userName, userSid, sessionId, processId, processPath, groups);
        }

        private static IReadOnlyList<PipeClientGroup> GroupsOf(WindowsIdentity user, int processId)
        {
            var retVal = new List<PipeClientGroup>();
            foreach (var sid in user.Groups?.OfType<SecurityIdentifier>() ?? Enumerable.Empty<SecurityIdentifier>())
            {
                string name = null;
                try
                {
                    name = ((NTAccount)sid.Translate(typeof(NTAccount))).Value;
                }
                catch (IdentityNotMappedException)
                {
                    // Logon-session SIDs and capabilities have no name - the group still counts by its SID.
                    LogEnvironment.LogDebugEvent($"Group {sid.Value} of pipe client process {processId} has no name; it is used by its SID.", LogSeverity.Report);
                }

                retVal.Add(new PipeClientGroup(sid, name));
            }

            return retVal;
        }
    }
}
