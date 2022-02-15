using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImpromptuInterface.Build;

namespace ITVComponents.EFRepo.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple=false, Inherited=true)]
    public class ManualTableNameAttribute:Attribute
    {
        /// <summary>
        /// Gets the TableName that overrides the property-name of a db-set
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// Overrides the table-name of a db-set
        /// </summary>
        /// <param name="tableName"></param>
        public ManualTableNameAttribute(string tableName)
        {
            TableName = tableName;
        }
    }
}
