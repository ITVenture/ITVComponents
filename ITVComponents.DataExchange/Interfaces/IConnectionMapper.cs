using ITVComponents.DataAccess;
using ITVComponents.Plugins;
using ITVComponents.Threading;

namespace ITVComponents.DataExchange.Interfaces
{
    public interface IConnectionMapper
    {
        /// <summary>
        /// Gets a Database connection and opens a transaction is requested
        /// </summary>
        /// <returns>a connector that opens the configured database</returns>
        IDbWrapper AcquireDatabase();
    }
}
