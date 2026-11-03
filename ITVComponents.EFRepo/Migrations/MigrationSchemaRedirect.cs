using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.Logging;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ITVComponents.EFRepo.Migrations
{
    /// <summary>
    /// Provider-independent part of the schema-aware migrations sql-generators: redirects schema-less migration-operations into
    /// the configured schema. The provider-specific generators (SqlServer, PostgreSql) only differ in their default schema and
    /// in the way they quote the schema, when they replace the schema-placeholder in raw sql.
    /// </summary>
    public static class MigrationSchemaRedirect
    {
        /// <summary>
        /// Sets the target schema on all schema-properties of the given operation and its nested operations (columns,
        /// keys and constraints of a CreateTable, old definitions of Alter-operations). NewSchema of a rename is left untouched,
        /// because null means "schema stays".
        /// </summary>
        /// <param name="operation">the operation to redirect</param>
        /// <param name="targetSchema">the schema to apply</param>
        public static void RedirectSchema(MigrationOperation operation, string targetSchema)
        {
            RedirectSchema(operation, operation, targetSchema);
        }

        private static void RedirectSchema(object target, MigrationOperation rootOperation, string targetSchema)
        {
            foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                if (property.PropertyType == typeof(string) && property.CanWrite && (property.Name == "Schema" || property.Name == "PrincipalSchema"))
                {
                    var current = (string)property.GetValue(target);
                    if (current == null)
                    {
                        property.SetValue(target, targetSchema);
                    }
                    else if (!current.Equals(targetSchema, StringComparison.OrdinalIgnoreCase))
                    {
                        LogEnvironment.LogEvent($"The migration-operation {rootOperation.GetType().Name} ({DescribeOperation(rootOperation)}) uses the explicit schema '{current}', but the schema '{targetSchema}' is configured. The migration was probably generated with a schema by mistake.", LogSeverity.Warning);
                    }
                }
                else if (typeof(MigrationOperation).IsAssignableFrom(property.PropertyType))
                {
                    var nested = property.GetValue(target);
                    if (nested != null)
                    {
                        RedirectSchema(nested, rootOperation, targetSchema);
                    }
                }
                else if (typeof(IEnumerable<MigrationOperation>).IsAssignableFrom(property.PropertyType))
                {
                    if (property.GetValue(target) is IEnumerable<MigrationOperation> nestedList)
                    {
                        foreach (var nested in nestedList.ToArray())
                        {
                            RedirectSchema(nested, rootOperation, targetSchema);
                        }
                    }
                }
            }
        }

        private static string DescribeOperation(MigrationOperation operation)
        {
            return operation is ITableMigrationOperation tableOperation ? tableOperation.Table : "-";
        }
    }
}
