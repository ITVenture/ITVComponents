using System;
using System.Collections.Generic;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.EFRepo.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace ITVComponents.EFRepo.PostgreSql.Migrations
{
    /// <summary>
    /// Applies the configured schema to schema-less migrations when they are applied or scripted. Migrations are generated
    /// without a schema, so the same migration works for public and for any schema that is configured per installation.
    /// Every operation without a schema gets the configured schema and the schema-placeholder in raw sql-operations is
    /// replaced (with public, when nothing is configured).
    /// </summary>
    public class SchemaAwareMigrationsSqlGenerator : NpgsqlMigrationsSqlGenerator
    {
        private readonly string targetSchema;

        private readonly bool schemaConfigured;

        private readonly bool ensureSchema;

        // INpgsqlSingletonOptions is an internal api of Npgsql, but it is required by the constructor of the base generator, which is
        // the only way to extend it. Re-check this constructor when Npgsql is updated.
#pragma warning disable EF1001
        public SchemaAwareMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, INpgsqlSingletonOptions npgsqlSingletonOptions, IDbContextOptions contextOptions) : base(dependencies, npgsqlSingletonOptions)
#pragma warning restore EF1001
        {
            var extension = contextOptions.FindExtension<SchemaAwareMigrationsOptionsExtension>();
            schemaConfigured = !string.IsNullOrEmpty(extension?.Schema);
            targetSchema = schemaConfigured ? extension.Schema : MigrationSchema.PostgreSqlDefaultSchema;
            ensureSchema = extension?.EnsureSchema ?? false;
        }

        public override IReadOnlyList<MigrationCommand> Generate(IReadOnlyList<MigrationOperation> operations, IModel model = null, MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
        {
            var redirected = new List<MigrationOperation>(operations.Count + 1);
            if (schemaConfigured && ensureSchema && !targetSchema.Equals(MigrationSchema.PostgreSqlDefaultSchema, StringComparison.Ordinal))
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
                    // PostgreSql-sql refers to the placeholder inside double quotes ("__SCHEMA__"), so quotes in the name are doubled
                    sqlOperation.Sql = sqlOperation.Sql.Replace(MigrationSchema.Placeholder, targetSchema.Replace("\"", "\"\""));
                }

                redirected.Add(operation);
            }

            return base.Generate(redirected, model, options);
        }
    }
}
