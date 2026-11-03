using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.EFRepo.Migrations;
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
                    MigrationSchemaRedirect.RedirectSchema(operation, targetSchema);
                }

                if (operation is SqlOperation sqlOperation && !string.IsNullOrEmpty(sqlOperation.Sql))
                {
                    sqlOperation.Sql = sqlOperation.Sql.Replace(MigrationSchema.Placeholder, targetSchema.Replace("]", "]]"));
                }

                redirected.Add(operation);
            }

            return base.Generate(redirected, model, options);
        }
    }
}
