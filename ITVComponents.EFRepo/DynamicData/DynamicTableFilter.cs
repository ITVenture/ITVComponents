using System;

namespace ITVComponents.EFRepo.DynamicData
{
    public abstract class DynamicTableFilter
    {
        /// <summary>
        /// Converts the current Query-Part to a string
        /// </summary>
        /// <param name="tableColumnNameCallback">a callback that provides table-information for a specific Column when building the query-string</param>
        /// <param name="addQueryParam">a callback that will create a query-parameter and return the parameter name. the parameter of the callback will be stored in the DbParameter object that is created</param>
        /// <param name="syntaxProvider">a Query-Syntax provider object that will help the filter to form specific query parts</param>
        /// <returns>the complete query for this Filter-object</returns>
        public abstract string BuildQueryPart(TableColumnResolveCallback tableColumnNameCallback, Func<object, string> addQueryParam, IQuerySyntaxProvider syntaxProvider);
    }

    public delegate string TableColumnResolveCallback(string tableName, out TableColumnDefinition rawDef);
}
