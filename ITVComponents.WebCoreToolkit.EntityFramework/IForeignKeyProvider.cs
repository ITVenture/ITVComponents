using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.ForeignKeys;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.EntityFramework
{
    public interface IForeignKeyProvider
    {
        /// <summary>
        /// Gets the filter Linq-Query for the given table-name. If you implement this interface, form a query that uses the db-context as [db] and the search-string as [filter]
        /// </summary>
        /// <param name="tableName">the table-name for which to get the foreign-key data</param>
        /// <returns>the query that will be executed go get the foreignkey-data</returns>
        IEnumerable GetForeignKeyFilterQuery(string tableName);

        /// <summary>
        /// Gets the filter Linq-Query for the given table-name. If you implement this interface, form a query that uses the db-context as [db] and the search-string as [filter]
        /// </summary>
        /// <param name="tableName">the table-name for which to get the foreign-key data</param>
        /// <param name="postedFilter">a filter that was posted when a Foreignkey was queried with POST</param>
        /// <returns>the query that will be executed go get the foreignkey-data</returns>
        IEnumerable GetForeignKeyFilterQuery(string tableName, Dictionary<string, object> postedFilter);

        /// <summary>
        /// Gets the filter Linq-Query for the given table-name. If you implement this interface, form a query that uses the db-context as [db] and the search-string as [filter]
        /// </summary>
        /// <param name="tableName">the table-name for which to get the foreign-key data</param>
        /// <param name="id">the primary-key value of the resovled record</param>
        /// <returns>the query that will be executed go get the foreignkey-data</returns>
        IEnumerable GetForeignKeyResolveQuery(string tableName, object id);
    }
}
