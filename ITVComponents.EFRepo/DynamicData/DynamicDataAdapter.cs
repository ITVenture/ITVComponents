using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ITVComponents.EFRepo.DynamicData
{
    public abstract class DynamicDataAdapter: IDisposable
    {
        private readonly DbContext parentContext;

        /// <summary>
        /// /Initializes a new instance of the DynamicDataAdapter class
        /// </summary>
        /// <param name="parentContext">the parent context of this Adapter</param>
        protected DynamicDataAdapter(DbContext parentContext)
        {
            this.parentContext = parentContext;
        }

        /// <summary>
        /// Gets a list of eligible types that are available for table-design
        /// </summary>
        public DynamicDataColumnType[] EligibleTypes => SyntaxProvider.EligibleTypes;
        
        /// <summary>
        /// Gets basic Syntax-information for the underlaying database-system
        /// </summary>
        public IQuerySyntaxProvider SyntaxProvider { get; protected set;}

        /// <summary>
        /// Gets an entity that can be filled with data.
        /// </summary>
        /// <returns>an object that acts in the way an application would expect it to, based on the underlaying database</returns>
        public abstract IDictionary<string, object> GetEntity();

        /// <summary>
        /// Gets the direct Database interface of the parent context
        /// </summary>
        protected DatabaseFacade Facade => parentContext.Database;

        /// <summary>
        /// Gets the data for a specific Table which can or can not be part of the parentContext
        /// </summary>
        /// <param name="tableName">the table-name to query</param>
        /// <param name="filter">the filter to apply</param>
        /// <param name="sorts">the selected columns for sorting</param>
        /// <param name="totalCount">the total number of hits that has resulted form the given query</param>
        /// <param name="tableAlias">the alias of the table if the query is extended by the query-callbacks</param>
        /// <param name="queryCallbacks">a callback-collection that is used to extend the query and column selection</param>
        /// <param name="hitsPerPage">the number of rows to select</param>
        /// <param name="page">the page to read if the hitsPerPage is selected</param>
        /// <returns>a list of hits that was read from the requested table</returns>
        public abstract List<IDictionary<string, object>> QueryDynamicTable(string tableName, DynamicTableFilter filter, ICollection<DynamicTableSort> sorts, out int totalCount, string tableAlias = null, DynamicQueryCallbackProvider queryCallbacks = null, int? hitsPerPage = null, int? page = null);

        /// <summary>
        /// Describes a Table
        /// </summary>
        /// <param name="tableName">the name of the Table that is described</param>
        /// <param name="ignoreUnknownTypes">indicates whether to ignore errors of db-types that could not be matched to managed types</param>
        /// <param name="definitionEditable">indicates whether the dynamicData adapter supports editing the given table-Definition</param>
        /// <returns>a list of columns of the requested table</returns>
        public abstract List<TableColumnDefinition> DescribeTable(string tableName, bool ignoreUnknownTypes, out bool definitionEditable);

        /// <summary>
        /// Copies the data of the specified src table to the specified destination table. if ignoreMissiongColumns is true, no exception is thrown, when a source-column can not be copied
        /// </summary>
        /// <param name="src">the source table</param>
        /// <param name="dst">the destination table</param>
        /// <param name="ignoreMissingColumn">when true, all source-columns must be copied to the destination (the destination can have more columns), otherwise an exception is thrown. when false, no exception is thrown, when a source-column can not be copied to destination.</param>
        /// <param name="whatIf">if set to true, no action is performed. Instead, the method will log the generated sql-commands to the LoggingEnvironment.</param>
        public abstract void CopyTableData(string src, string dst, bool ignoreMissingColumn = false, bool whatIf = false);

        /// <summary>
        /// Applies the given Column-configuration to an existing or new table
        /// </summary>
        /// <param name="tableName">the name of the target table</param>
        /// <param name="columns">the column-configuration to apply</param>
        /// <param name="forceDeleteColumn">indicates whether it is legit to drop a column</param>
        /// <param name="useTransaction">indicates whether to put the entire task into a transaction</param>
        /// <param name="whatIf">if set to true, no action is performed. Instead, the method will log the generated sql-commands to the LoggingEnvironment.</param>
        public abstract void AlterOrCreateTable(string tableName, TableColumnDefinition[] columns, bool forceDeleteColumn = false, bool useTransaction = true, bool whatIf = false);

        /// <summary>
        /// Checks for a specific table if it exists
        /// </summary>
        /// <param name="tableName">the target table-name</param>
        /// <returns>a value indicating whether the table exists in the database that is under the parentContext</returns>
        public abstract bool TableExists(string tableName);

        /// <summary>
        /// Executes a custom query
        /// </summary>
        /// <typeparam name="T">the expected return type</typeparam>
        /// <param name="query">the query to execute</param>
        /// <param name="arguments">the arguments for the query</param>
        /// <returns>a list of selected rows</returns>
        public abstract List<T> SqlQuery<T>(string query, params object[] arguments) where T : new();

        /// <summary>
        /// Executes a custom query
        /// </summary>
        /// <param name="query">the query to execute</param>
        /// <param name="targetType">the expected model-type that results out of the selection</param>
        /// <param name="arguments">the arguments for the query</param>
        /// <returns>a list of selected rows</returns>
        public abstract List<object> SqlQuery(string query, Type targetType, params object[] arguments);

        /// <summary>
        /// Executes a custom query
        /// </summary>
        /// <param name="query">the query to execute</param>
        /// <param name="arguments">the arguments for the query</param>
        /// <returns>an enumerable of selected rows</returns>
        public abstract IEnumerable<IDictionary<string,object>> SqlQuery(string query, IDictionary<string, object> arguments);

        /// <summary>
        /// Updates a specific record of the database
        /// </summary>
        /// <param name="tableName">the name of the table</param>
        /// <param name="values">the values to write into the existing record</param>
        /// <returns>the values that were written to the database</returns>
        public abstract IDictionary<string, object> Update(string tableName, IDictionary<string, object> values);

        /// <summary>
        /// Creates a new record in the database
        /// </summary>
        /// <param name="tableName">the target-table to which the record is written</param>
        /// <param name="values">the record to write</param>
        /// <returns>the record that was written to the database</returns>
        public abstract IDictionary<string, object> Create(string tableName, IDictionary<string, object> values);

        /// <summary>
        /// Deletes an existing record from the database
        /// </summary>
        /// <param name="tableName">the tablename from which to delete the record</param>
        /// <param name="record">the record to delete</param>
        /// <returns>a value indicating whether the delete was successful</returns>
        public abstract bool Delete(string tableName, IDictionary<string, object> record);

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public virtual void Dispose()
        {
        }
    }
}
