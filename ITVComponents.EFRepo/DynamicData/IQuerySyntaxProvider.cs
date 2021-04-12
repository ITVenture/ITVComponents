using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.DynamicData
{
    public interface IQuerySyntaxProvider
    {
        /// <summary>
        /// /Gets a list of eligible types that can be used to design a table
        /// </summary>
        DynamicDataColumnType[] EligibleTypes{get;}
        
        /// <summary>
        /// Builds a logic Operand chain for a specific boolean operator (AND/OR)
        /// </summary>
        /// <param name="type">the used operator for the boolean operation</param>
        /// <param name="filterParts">the filter parts that need to be chained</param>
        /// <param name="tableColumnNameCallback">a callback returning the full-qualified column for a specified alias</param>
        /// <param name="addQueryParam">a callback that will add a parameter to the query-command that needs to be executed</param>
        /// <param name="invertEntireFilter">indicates whether to invert this query-part</param>
        /// <returns>a string representing the boolean filter chain represented by the provided params</returns>
        string BooleanLogicFilter(DynamicCompositeFilterType type, ICollection<DynamicTableFilter> filterParts, TableColumnResolveCallback tableColumnNameCallback, Func<object, string> addQueryParam, bool invertEntireFilter);

        /// <summary>
        /// Builds a binary compare operation for the given column name operator and values
        /// </summary>
        /// <param name="columnName">the column name to compare</param>
        /// <param name="op">the binary comparison filter operator</param>
        /// <param name="value">the value to compare with</param>
        /// <param name="value2">the value 2 for between operations</param>
        /// <param name="addQueryParam">a callback that will add a parameter to the query-command that needs to be executed</param>
        /// <returns>a string representing the requested binary compare operation</returns>
        string BinaryCompareOperation(string columnName, BinaryCompareFilterOperator op, object value, object value2, Func<object, string> addQueryParam);

        /// <summary>
        /// Builds the full-qualified Column name for the implemented syntax
        /// </summary>
        /// <param name="tableName">the table-name</param>
        /// <param name="columnName">the column-name</param>
        /// <returns>the full-qualified column name for this syntax</returns>
        string FullQualifyColumn(string tableName, string columnName);

        /// <summary>
        /// Builds the appropriate syntax for an object-name in the syntax of the underlaying database system
        /// </summary>
        /// <param name="objectName">the object-name to format</param>
        /// <returns>the formatted object name</returns>
        string FormatObjectName(string objectName);

        /// <summary>
        /// Gets the appropriate managed type for the given Database-column definition
        /// </summary>
        /// <param name="definition">the column definition of a database-table</param>
        /// <param name="throwOnError">indicates whether to throw an error when a type could not be resolved</param>
        /// <returns>the appropriate managed type for the given column</returns>
        DynamicDataColumnType GetAppropriateType(TableColumnDefinition definition, bool throwOnError);
    }
}
