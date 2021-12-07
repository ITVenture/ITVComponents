using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ForeignKeySelectionAttribute:Attribute
    {
        /// <summary>
        /// Initializes a new instance of the ForeignKeySelectionAttribute class
        /// </summary>
        /// <param name="completeSelect">the complete select expression for the linq query</param>
        /// <param name="orderByExpression">the order-by expression of the linq query</param>
        public ForeignKeySelectionAttribute(string completeSelect, string orderByExpression)
        {
            CompleteSelect = completeSelect;
            OrderByExpression = orderByExpression;
        }

        /// <summary>
        /// Gets the Select Expression. The current record is exposed as t
        /// </summary>
        public string CompleteSelect { get; }

        /// <summary>
        /// Gets the OrderBy Expression of the query
        /// </summary>
        public string OrderByExpression { get;  }

        /// <summary>
        /// Gets or sets the FilterKeys that can be present in the Filter-Dictionary. For Each Filterkey entry, a matching Filter- and FilterDeclaration-Entry is expected
        /// </summary>
        public string[] FilterKeys { get;set; }

        /// <summary>
        /// Gets or sets the Filters that must be applied, when a matching FilterKey is present in the Filter-Dictionary
        /// </summary>
        public string[] Filters { get; set; }

        /// <summary>
        /// Gets or sets the Declarations of variables (including required conversions) that are used to filter the entities when a foreign-key is requested
        /// </summary>
        public string[] FilterDeclarations { get; set; }
    }
}
