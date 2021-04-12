using System;
using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.EFRepo.DynamicData
{
    public class DynamicCompositeFilter:DynamicTableFilter
    {
        private readonly DynamicCompositeFilterType type;

        private List<DynamicTableFilter> innerFilter = new List<DynamicTableFilter>();

        /// <summary>
        /// Initializes a new instance of the DynamicAggregateFilter class
        /// </summary>
        /// <param name="type">the query-type of this AggregateFilter</param>
        /// <param name="innerFilter">the inner filter to use in this filter</param>
        public DynamicCompositeFilter(DynamicCompositeFilterType type, DynamicTableFilter[] innerFilter):this(type)
        {
            this.innerFilter.AddRange(innerFilter);
        }
        
        /// <summary>
        /// Initializes a new instance of the DynamicAggregateFilter class
        /// </summary>
        /// <param name="type">the query-type of this AggregateFilter</param>
        public DynamicCompositeFilter(DynamicCompositeFilterType type)
        {
            this.type = type;
        }
        
        /// <summary>
        /// Indicates whether to invert this filter-part
        /// </summary>
        public bool Invert { get; set; }
        
        /// <summary>
        /// Adds a filter object to the innerList of filters for this aggregate
        /// </summary>
        /// <param name="filter"></param>
        public void AddFilter(DynamicTableFilter filter)
        {
            innerFilter.Add(filter);
        }

        /// <summary>
        /// Converts the current Query-Part to a string
        /// </summary>
        /// <param name="tableColumnNameCallback">a callback that provides table-information for a specific Column when building the query-string</param>
        /// <param name="addQueryParam">a callback that will create a query-parameter and return the parameter name. the parameter of the callback will be stored in the DbParameter object that is created</param>
        /// <param name="syntaxProvider">a Query-Syntax provider object that will help the filter to form specific query parts</param>
        /// <returns>the complete query for this Filter-object</returns>
        public override string BuildQueryPart(TableColumnResolveCallback tableColumnNameCallback, Func<object,string> addQueryParam, IQuerySyntaxProvider syntaxProvider)
        {
            if (innerFilter.Count == 0)
            {
                return "";
            }

            return syntaxProvider.BooleanLogicFilter(type, innerFilter, tableColumnNameCallback, addQueryParam, Invert);
        }
    }

    public enum DynamicCompositeFilterType
    {
        And,
        Or
    }
}
