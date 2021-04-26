using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public class NullSensitiveFkQueryExpressionAttribute:Attribute
    {
        private readonly string queryPart;

        /// <summary>
        /// Initializes a new instance of the NullSensitiveFkQueryExpressionAttribute class
        /// </summary>
        /// <param name="queryPart">the query-part that is used to filter the current column. It will be formatted with the parameters 'Column' for the query-qualified Property-Name and 'Parameter' for the name of the used filter-parameter</param>
        public NullSensitiveFkQueryExpressionAttribute(string queryPart)
        {
            this.queryPart = queryPart;
        }

        /// <summary>
        /// Formats the given QueryPart with the full-qualified table-name and the target parameter name for the current fq-query
        /// </summary>
        /// <param name="fullQualifiedColumn">the Query-Qualified Column-name that is being filtered</param>
        /// <param name="parameterName">the parameter name that is used for comparison</param>
        /// <returns>the formatted string to use for filtering</returns>
        public string GetQueryPart(string fullQualifiedColumn, string parameterName)
        {
            return new {Column = fullQualifiedColumn, Parameter = parameterName}.FormatText(queryPart);
        }
    }
}