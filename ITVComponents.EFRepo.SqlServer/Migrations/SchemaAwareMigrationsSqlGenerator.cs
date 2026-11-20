using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
                    if (options.HasFlag(MigrationsSqlGenerationOptions.Idempotent) && MustStartABatch(sqlOperation.Sql))
                    {
                        // A copy: EF keeps the operations of a migration, a second run must not see the wrapped form.
                        redirected.Add(new SqlOperation
                        {
                            Sql = $"EXEC(N'{sqlOperation.Sql.Replace("'", "''")}')",
                            SuppressTransaction = sqlOperation.SuppressTransaction
                        });
                        continue;
                    }
                }

                redirected.Add(operation);
            }

            return base.Generate(redirected, model, options);
        }

        /// <summary>
        /// Matches a definition that T-SQL only accepts as the first statement of a batch (leading line comments allowed).
        /// </summary>
        private static readonly Regex BatchLeadingDefinition = new(
            @"\A(\s*--[^\n]*\n)*\s*(CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+(VIEW|FUNCTION|PROCEDURE|PROC|TRIGGER|SCHEMA)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BatchSeparator = new(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Indicates whether a raw sql-operation must be wrapped in EXEC for an idempotent script.
        /// </summary>
        /// <remarks>
        /// An idempotent script puts every command into <c>IF NOT EXISTS (...) BEGIN ... END</c>. CREATE VIEW, FUNCTION,
        /// PROCEDURE, TRIGGER and SCHEMA must be the first statement of a batch and are invalid there; EF wraps its own
        /// operations of that kind in EXEC, but not the sql of <c>migrationBuilder.Sql</c> (e.g. from <c>ConfigureViews</c>).
        /// Sql that contains its own GO separators is left alone: EF splits it into batches, which EXEC would break.
        /// </remarks>
        private static bool MustStartABatch(string sql)
        {
            return BatchLeadingDefinition.IsMatch(sql) && !BatchSeparator.IsMatch(sql);
        }
    }
}
