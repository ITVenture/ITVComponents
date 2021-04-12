using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DataAccess.DataAnnotations
{
    /// <summary>
    /// Marks a Type as a TableType
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=false, Inherited = false)]
    public class TableTypeAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the Table Type that is associated with the Managed class that is extended with the TableTypeAttribute
        /// </summary>
        public string TableTypeName { get; set; }

        /// <summary>
        /// Gets or sets the Layout type that is used for the marked class
        /// </summary>
        public TableTypeLayout Layout { get; set; }

        /// <summary>
        /// Initializes a new instance of the TableTypeAttribute class
        /// </summary>
        public TableTypeAttribute()
        {
            Layout = TableTypeLayout.Auto;
        }
    }

    /// <summary>
    /// The layout type for the marked class
    /// </summary>
    public enum TableTypeLayout
    {
        /// <summary>
        /// the table types are used in the order they appear in the class
        /// </summary>
        Auto,

        /// <summary>
        /// there is an explicit order for the properties
        /// </summary>
        Explicit
    }
}
