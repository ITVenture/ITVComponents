using System;
using System.Security.Principal;
using ITVComponents.Plugins;

namespace ITVComponents.WebCoreToolkit.Security.UserMappers
{
    /// <summary>
    /// Implements a default UserNameMapper that only returns the name of the underlaying identity user
    /// </summary>
    public class SimpleUserNameMapper:IUserNameMapper, IPlugin
    {
        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets all labels for the given principaluser
        /// </summary>
        /// <param name="user">the user for which to get all labels</param>
        /// <returns>a list of labels that are assigned to the given user</returns>
        public string[] GetUserLabels(IIdentity user)
        {
            return new[] {user?.Name};
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
