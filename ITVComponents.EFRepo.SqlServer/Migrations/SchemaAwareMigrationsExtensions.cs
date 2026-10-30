using System;
using System.Linq;
using ITVComponents.EFRepo.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITVComponents.EFRepo.SqlServer.Migrations
{
    public static class SchemaAwareMigrationsExtensions
    {
        /// <summary>
        /// Applies the given schema to schema-less migrations when they are applied or scripted (e.g. in a design-time factory).
        /// Also moves the migrations-history table into that schema. Call it after UseSqlServer.
        /// </summary>
        /// <param name="builder">the options-builder of the context</param>
        /// <param name="schema">the target schema, or a reference to it (-&gt;Path or :--&gt;Path). null or empty means dbo (behavior as before)</param>
        /// <param name="ensureSchema">indicates whether to create the schema when it does not exist. By default, the schema is expected to be created by the dba</param>
        /// <returns>the provided builder</returns>
        public static DbContextOptionsBuilder UseSchemaAwareMigrations(this DbContextOptionsBuilder builder, string schema, bool ensureSchema = false)
        {
            var resolvedSchema = SchemaConfiguration.Resolve(schema);
            var infrastructure = (IDbContextOptionsBuilderInfrastructure)builder;
            infrastructure.AddOrUpdateExtension(new SchemaAwareMigrationsOptionsExtension(resolvedSchema, ensureSchema));
            builder.ReplaceService<IMigrationsSqlGenerator, SchemaAwareMigrationsSqlGenerator>();
            if (!string.IsNullOrEmpty(resolvedSchema))
            {
                var relational = builder.Options.Extensions.OfType<RelationalOptionsExtension>().FirstOrDefault();
                if (relational == null)
                {
                    throw new InvalidOperationException("UseSchemaAwareMigrations requires UseSqlServer to be called first.");
                }

                if (relational.MigrationsHistoryTableSchema == null)
                {
                    var updated = relational.WithMigrationsHistoryTableSchema(resolvedSchema);
                    // the options are keyed by the concrete extension-type, so the generic argument must be the provider-extension
                    var addOrUpdate = typeof(IDbContextOptionsBuilderInfrastructure).GetMethod(nameof(IDbContextOptionsBuilderInfrastructure.AddOrUpdateExtension))
                        .MakeGenericMethod(updated.GetType());
                    addOrUpdate.Invoke(infrastructure, new object[] { updated });
                }
            }

            return builder;
        }

        /// <summary>
        /// Applies the given schema to schema-less migrations when they are applied or scripted (e.g. in a design-time factory).
        /// Also moves the migrations-history table into that schema. Call it after UseSqlServer.
        /// </summary>
        /// <typeparam name="TContext">the context-type</typeparam>
        /// <param name="builder">the options-builder of the context</param>
        /// <param name="schema">the target schema, or a reference to it (-&gt;Path or :--&gt;Path). null or empty means dbo (behavior as before)</param>
        /// <param name="ensureSchema">indicates whether to create the schema when it does not exist. By default, the schema is expected to be created by the dba</param>
        /// <returns>the provided builder</returns>
        public static DbContextOptionsBuilder<TContext> UseSchemaAwareMigrations<TContext>(this DbContextOptionsBuilder<TContext> builder, string schema, bool ensureSchema = false) where TContext : DbContext
        {
            return (DbContextOptionsBuilder<TContext>)UseSchemaAwareMigrations((DbContextOptionsBuilder)builder, schema, ensureSchema);
        }
    }
}
