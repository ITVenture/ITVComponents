using System.Security.Principal;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Security
{
    /// <summary>
    /// A group of a pipe client.
    /// </summary>
    public sealed class PipeClientGroup
    {
        public PipeClientGroup(SecurityIdentifier sid, string name)
        {
            Sid = sid;
            Name = name;
        }

        /// <summary>
        /// Gets the SID of the group.
        /// </summary>
        public SecurityIdentifier Sid { get; }

        /// <summary>
        /// Gets the name of the group, or null when it has none (logon session, capability).
        /// </summary>
        public string Name { get; }
    }
}
