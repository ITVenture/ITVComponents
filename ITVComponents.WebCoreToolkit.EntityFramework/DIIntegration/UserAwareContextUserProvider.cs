using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.Plugins;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration
{
    public class UserAwareContextUserProvider<T>:DbContextUserProvider<T>, IPlugin
    where T: DbContext, IUserAwareContext
    {
        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets the current user-name that is using the given Db-Context
        /// </summary>
        /// <param name="dbContext">the db-context that is currently being used to save some data</param>
        /// <returns>the user-name that will be protocolled as creator or modifier for changed entities</returns>
        public override string GetUserName(T dbContext)
        {
            return dbContext.CurrentUserName;
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
