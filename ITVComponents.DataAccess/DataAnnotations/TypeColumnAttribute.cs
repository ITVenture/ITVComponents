using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DataAccess.DataAnnotations
{
    /// <summary>
    /// Marks a Property of a class as Column of a TableType
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class TypeColumnAttribute:Attribute
    {
        /// <summary>
        /// Gets or sets the ColumnName of the marked property
        /// </summary>
        public string ColumnName { get; set; }

        /// <summary>
        /// Gets or sets the OrdinalPosition of the marked property. this value is ignored if the TypeLayout of the containing class is set to auto
        /// </summary>
        public int OrdinalPosition { get; set; }

        /// <summary>
        /// Gets or sets the Column Type of the target column
        /// </summary>
        public Type ColumnType { get; set; }
    }
}
