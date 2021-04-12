using ITVComponents.DataAccess;
using ITVComponents.Plugins;
using ITVComponents.Threading;

namespace ITVComponents.DataExchange.Interfaces
{
    public interface IConnectionMapper:IPlugin
    {
        /// <summary>
        /// Gets a Database connection and opens a transaction is requested
        /// </summary>
        /// <param name="useTransaction">indicates whether to open a new transaction on the returned connection</param>
        /// <param name="database">the database connection</param>
        /// <returns>a ResourceLock - Object that enables the caller to free the Database implicitly after using</returns>
        IResourceLock AcquireDatabase(bool useTransaction, out IDbWrapper database);
    }
}
