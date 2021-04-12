using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.Extensions
{
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// Configures a DbContext and sets all table-names to the Property-Names that you have specified
        /// </summary>
        /// <param name="builder">the ModelBuilder</param>
        /// <param name="targetContext">the targetcontext on which to set the tablenames</param>
        public static void TableNamesFromProperties(this ModelBuilder builder, DbContext targetContext)
        {
            Type t = targetContext.GetType();
            Type dbsType = typeof (DbSet<>);
            HashSet<Type> types = new HashSet<Type>();
            PropertyInfo[] allDbSets =
                t.GetProperties(BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.Public |
                                BindingFlags.FlattenHierarchy)
                    .Where(n => n.PropertyType.IsGenericType && n.PropertyType.GetGenericTypeDefinition() == dbsType)
                    .ToArray();
            try
            {
                foreach (PropertyInfo pi in allDbSets)
                {
                    Type[] tableArg = pi.PropertyType.GetGenericArguments();
                    if (types.Contains(tableArg[0]))
                    {
                        throw new InvalidOperationException("Do not use the same Entity-Type twice!");
                    }

                    types.Add(tableArg[0]);
                    var entityConfig = builder.Entity(tableArg[0]);
                    entityConfig.ToTable(pi.Name);
                }
            }
            finally
            {
                types.Clear();
            }
        }
    }
}
