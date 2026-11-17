using System;
using ITVComponents.InterProcessCommunication.NamedPipe.Security;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Hub
{
    /// <summary>
    /// A client that connected to or disconnected from the hub.
    /// </summary>
    public sealed class PipeClientEventArgs : EventArgs
    {
        public PipeClientEventArgs(PipeClientIdentity client)
        {
            Client = client;
        }

        /// <summary>
        /// Gets who the client is, as the operating system reports it.
        /// </summary>
        public PipeClientIdentity Client { get; }
    }
}
