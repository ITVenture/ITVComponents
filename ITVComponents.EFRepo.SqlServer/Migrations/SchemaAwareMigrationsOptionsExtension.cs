using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.EFRepo.SqlServer.Migrations
{
    /// <summary>
    /// Carries the schema that the SchemaAwareMigrationsSqlGenerator applies to the migration-operations. The schema is part of
    /// the service-provider identity, so two contexts with different schemas never share a cached generator.
    /// </summary>
    public class SchemaAwareMigrationsOptionsExtension : IDbContextOptionsExtension
    {
        private DbContextOptionsExtensionInfo info;

        public SchemaAwareMigrationsOptionsExtension(string schema, bool ensureSchema)
        {
            Schema = schema;
            EnsureSchema = ensureSchema;
        }

        /// <summary>
        /// The configured target schema. null means dbo
        /// </summary>
        public string Schema { get; }

        /// <summary>
        /// Indicates whether the migrations create the schema, when it does not exist
        /// </summary>
        public bool EnsureSchema { get; }

        public DbContextOptionsExtensionInfo Info => info ??= new ExtensionInfo(this);

        public void ApplyServices(IServiceCollection services)
        {
        }

        public void Validate(IDbContextOptions options)
        {
        }

        private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            public ExtensionInfo(IDbContextOptionsExtension extension) : base(extension)
            {
            }

            private new SchemaAwareMigrationsOptionsExtension Extension => (SchemaAwareMigrationsOptionsExtension)base.Extension;

            public override bool IsDatabaseProvider => false;

            public override string LogFragment => $"SchemaAwareMigrations={Extension.Schema ?? "dbo"} ";

            public override int GetServiceProviderHashCode()
            {
                return ((Extension.Schema ?? "").GetHashCode() * 397) ^ Extension.EnsureSchema.GetHashCode();
            }

            public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            {
                return other is ExtensionInfo o && o.Extension.Schema == Extension.Schema && o.Extension.EnsureSchema == Extension.EnsureSchema;
            }

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            {
                debugInfo["SchemaAwareMigrations:Schema"] = Extension.Schema ?? "dbo";
                debugInfo["SchemaAwareMigrations:EnsureSchema"] = Extension.EnsureSchema.ToString();
            }
        }
    }
}
