using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Logging;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Update;

namespace ITVComponents.EFRepo.SqlServer.Migrations
{
    /// <summary>
    /// Applies the configured schema to schema-less migrations when they are applied or scripted. Migrations are generated
    /// without a schema, so the same migration works for dbo and for any schema that is configured per installation.
    /// Every operation without a schema gets the configured schema and the schema-placeholder in raw sql-operations is
    /// replaced (with dbo, when nothing is configured).
    /// </summary>
    public class SchemaAwareMigrationsSqlGenerator : SqlServerMigrationsSqlGenerator
    {
        private readonly string targetSchema;

        private readonly bool schemaConfigured;

        private readonly bool ensureSchema;

        public SchemaAwareMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, ICommandBatchPreparer commandBatchPreparer, IDbContextOptions contextOptions) : base(dependencies, commandBatchPreparer)
        {
            var extension = contextOptions.FindExtension<SchemaAwareMigrationsOptionsExtension>();
            schemaConfigured = !string.IsNullOrEmpty(extension?.Schema);
            targetSchema = schemaConfigured ? extension.Schema : MigrationSchema.DefaultSchema;
            ensureSchema = extension?.EnsureSchema ?? false;
        }

        public override IReadOnlyList<MigrationCommand> Generate(IReadOnlyList<MigrationOperation> operations, IModel model = null, MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
        {
            var redirected = new List<MigrationOperation>(operations.Count + 1);
            if (schemaConfigured && ensureSchema && !targetSchema.Equals(MigrationSchema.DefaultSchema, StringComparison.OrdinalIgnoreCase))
            {
                redirected.Add(new EnsureSchemaOperation { Name = targetSchema });
            }

            foreach (var operation in operations)
            {
                if (schemaConfigured)
                {
                    RedirectSchema(operation, operation);
                }

                if (operation is SqlOperation sqlOperation && !string.IsNullOrEmpty(sqlOperation.Sql))
                {
                    sqlOperation.Sql = sqlOperation.Sql.Replace(MigrationSchema.Placeholder, targetSchema.Replace("]", "]]"));
                }

                redirected.Add(operation);
            }

            return base.Generate(redirected, model, options);
        }

        /// <summary>
        /// Sets the target schema on all schema-properties of the given operation and its nested operations (columns,
        /// keys and constraints of a CreateTable, old definitions of Alter-operations). NewSchema of a rename is left untouched,
        /// because null means "schema stays".
        /// </summary>
        /// <param name="target">the object to redirect</param>
        /// <param name="rootOperation">the top-level operation for the log-message</param>
        private void RedirectSchema(object target, MigrationOperation rootOperation)
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
                        RedirectSchema(nested, rootOperation);
                    }
                }
                else if (typeof(IEnumerable<MigrationOperation>).IsAssignableFrom(property.PropertyType))
                {
                    if (property.GetValue(target) is IEnumerable<MigrationOperation> nestedList)
                    {
                        foreach (var nested in nestedList.ToArray())
                        {
                            RedirectSchema(nested, rootOperation);
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
