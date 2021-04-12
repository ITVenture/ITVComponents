using System;
using ITVComponents.Plugins;
using ITVComponents.Threading;

namespace ITVComponents.DataAccess.Parallel
{
    public interface IConnectionBuffer : IPlugin
    {
        /// <summary>
        /// Gets an available Database Connection from the pool of available connections
        /// </summary>
        /// <param name="useTransaction">indicates whether to begin a transaction for the returned database connection</param>
        /// <param name="database">the database that is available for usage</param>
        /// <returns>a connection that is unused at the moment</returns>
        IResourceLock AcquireConnection(bool useTransaction, out IDbWrapper database);

        /// <summary>
        /// Enables a client object to configure the acquired connection before it is returned
        /// </summary>
        event Action<IDbWrapper> ConnectionAcquiring;
    }
}