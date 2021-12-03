using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using ITVComponents.Logging;

//using System.Windows.Markup;

namespace ITVComponents.DataAccess.Models
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DbColumnAttribute : Attribute
    {
        /// <summary>
        /// holds known mappings for types
        /// </summary>
        private static ConcurrentDictionary<Type, MapRule[]> rules;

        /// <summary>
        /// the Database - column-name for the bound field or property
        /// </summary>
        private string columnName;

        /// <summary>
        /// indicates whether to map the bound field or property to a column
        /// </summary>
        private bool mapped;

        /// <summary>
        /// Initializes static members of the DbColumnAttribute class
        /// </summary>
        static DbColumnAttribute()
        {
            rules = new ConcurrentDictionary<Type, MapRule[]>();
        }

        /// <summary>
        /// Initializes a new instance of the DbColumnAttribute class
        /// </summary>
        /// <param name="columnName">the database column for the bound field or property</param>
        public DbColumnAttribute(string columnName)
            : this(true)
        {
            this.columnName = columnName;
        }

        /// <summary>
        /// Initializes a new instance of the DbColumnAttribute class
        /// </summary>
        /// <param name="mapped">indicates whether to map the bound field or property to a column of a database table</param>
        public DbColumnAttribute(bool mapped)
            : this()
        {
            this.mapped = mapped;
        }

        /// <summary>
        /// Prevents a default instance of the DbColumnAttribute from being created
        /// </summary>
        private DbColumnAttribute()
        {
        }

        /// <summary>
        /// Gets the ColumnName to which the bound field or property is bound
        /// </summary>
        public string ColumnName
        {
            get { return columnName; }
        }

        /// <summary>
        /// Gets a value indicating whether this column is bound to a column
        /// </summary>
        public bool Mapped
        {
            get { return mapped; }
        }

        /// <summary>
        /// Gets or sets a user-defined Expression that is executed to estimate the value of a column, that does not actually exist. Provide an expression that takes a Parameter "value", which will be the database-value of the provided Column-Name
        /// </summary>
        public string ValueResolveExpression { get; set; }

        /// <summary>
        /// Gets map rules for a specific type
        /// </summary>
        /// <param name="targetType">the targettype for which to extract mapping rules</param>
        /// <returns>a map-rule indicating how values need to be mapped to a read-only model</returns>
        internal static MapRule[] GetRules(Type targetType)
        {
            MapRule[] retVal;
            retVal = rules.GetOrAdd(targetType, tt => (from t in
                    tt.GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                                          BindingFlags.Instance |
                                          BindingFlags.GetField | BindingFlags.SetField |
                                          BindingFlags.GetProperty |
                                          BindingFlags.SetProperty |
                                          BindingFlags.FlattenHierarchy)
                select new { Att = GetDbcAttribute(t), Mbr = t }
                into n
                where n.Att.mapped && (n.Mbr is PropertyInfo || n.Mbr is FieldInfo) && !n.Mbr.Name.Contains("k__BackingField")
                select new MapRule(n.Mbr, n.Att)).ToArray());
            return retVal;
        }

        /// <summary>
        /// Finds the attribute that is bound to a specific member
        /// </summary>
        /// <param name="info">the member info that is </param>
        /// <returns></returns>
        private static DbColumnAttribute GetDbcAttribute(MemberInfo info)
        {
            if ((info is PropertyInfo pi && !pi.CanWrite) ||
                (info is FieldInfo fi && fi.IsInitOnly))
            {
                return new DbColumnAttribute(false);
            }

            Attribute retVal = Attribute.GetCustomAttribute(info, typeof(DbColumnAttribute)) ?? Attribute.GetCustomAttribute(info, typeof(ColumnAttribute));
            if (retVal == null)
            {
                retVal = new DbColumnAttribute(info.Name);
            }
            if (retVal is ColumnAttribute ca)
            {
                retVal = new DbColumnAttribute(ca.Name ?? info.Name);
            }

            return (DbColumnAttribute)retVal;
        } 
    }
}