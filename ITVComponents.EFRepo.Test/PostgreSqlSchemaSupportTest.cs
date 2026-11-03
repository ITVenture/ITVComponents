using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.EFRepo.PostgreSql.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.EFRepo.Test
{
    [TestClass]
    public class PostgreSqlSchemaSupportTest
    {
        private const string ConnectionString = "Host=localhost;Database=SchemaSupportTest;Username=test;Password=test";

        [TestMethod]
        public void GeneratorQualifiesSchemalessOperations()
        {
            var sql = GenerateScript("Partner", CreateOperations());
            StringAssert.Contains(sql, "\"Partner\".\"Child\"");
            StringAssert.Contains(sql, "REFERENCES \"Partner\".\"Parent\"");
            StringAssert.Contains(sql, "CREATE VIEW \"Partner\".\"TenantView\" AS SELECT * FROM \"Partner\".\"Tenants\"");
            Assert.IsFalse(sql.Contains(MigrationSchema.Placeholder));
        }

        [TestMethod]
        public void GeneratorUsesPublicWithoutConfiguredSchema()
        {
            var sql = GenerateScript(null, CreateOperations());
            StringAssert.Contains(sql, "CREATE VIEW \"public\".\"TenantView\"");
            Assert.IsFalse(sql.Contains("\"Partner\""));
            Assert.IsFalse(sql.Contains(MigrationSchema.Placeholder));
        }

        [TestMethod]
        public void GeneratorEnsuresSchemaOnlyWhenRequested()
        {
            var ensured = GenerateScript("Partner", CreateOperations(), true);
            // depending on the Npgsql-version, the statement is a plain CREATE SCHEMA IF NOT EXISTS or a DO-block around CREATE SCHEMA
            StringAssert.Contains(ensured, "CREATE SCHEMA");
            StringAssert.Contains(ensured, "Partner");
            Assert.IsFalse(GenerateScript("Partner", CreateOperations()).Contains("CREATE SCHEMA"));
        }

        [TestMethod]
        public void HistoryTableFollowsTheSchema()
        {
            using var context = new PgSchemaTestContext(CreateOptions("Partner"));
            var history = context.GetService<IHistoryRepository>();
            StringAssert.Contains(history.GetCreateScript(), "\"Partner\".\"__EFMigrationsHistory\"");
        }

        [TestMethod]
        public void DelimitedObjectNameFollowsTheModelSchema()
        {
            using (var context = new PgSchemaTestContext(CreateOptions(null), "Partner"))
            {
                Assert.AreEqual("\"Partner\".\"GetTree\"", context.DelimitedObjectName("GetTree"));
            }

            using (var context = new PgSchemaTestContext(CreateOptions(null)))
            {
                Assert.AreEqual("\"GetTree\"", context.DelimitedObjectName("GetTree"));
            }
        }

        private static string GenerateScript(string schema, List<MigrationOperation> operations, bool ensureSchema = false)
        {
            using var context = new PgSchemaTestContext(CreateOptions(schema, ensureSchema));
            var generator = context.GetService<IMigrationsSqlGenerator>();
            Assert.IsInstanceOfType(generator, typeof(SchemaAwareMigrationsSqlGenerator));
            return string.Join(Environment.NewLine, generator.Generate(operations).Select(n => n.CommandText));
        }

        private static DbContextOptions<PgSchemaTestContext> CreateOptions(string schema, bool ensureSchema = false)
        {
            var builder = new DbContextOptionsBuilder<PgSchemaTestContext>();
            builder.UseNpgsql(ConnectionString);
            builder.UseSchemaAwareMigrations(schema, ensureSchema);
            builder.ReplaceService<IModelCacheKeyFactory, PgSchemaTestModelCacheKeyFactory>();
            return builder.Options;
        }

        private static List<MigrationOperation> CreateOperations()
        {
            var child = new CreateTableOperation { Name = "Child" };
            child.Columns.Add(new AddColumnOperation { Name = "Id", Table = "Child", ClrType = typeof(int), ColumnType = "integer" });
            child.Columns.Add(new AddColumnOperation { Name = "ParentId", Table = "Child", ClrType = typeof(int), ColumnType = "integer" });
            child.PrimaryKey = new AddPrimaryKeyOperation { Name = "PK_Child", Table = "Child", Columns = new[] { "Id" } };
            child.ForeignKeys.Add(new AddForeignKeyOperation { Name = "FK_Child_Parent", Table = "Child", Columns = new[] { "ParentId" }, PrincipalTable = "Parent", PrincipalColumns = new[] { "Id" } });
            return new List<MigrationOperation>
            {
                child,
                new SqlOperation { Sql = $"CREATE VIEW \"{MigrationSchema.Placeholder}\".\"TenantView\" AS SELECT * FROM \"{MigrationSchema.Placeholder}\".\"Tenants\"" }
            };
        }

        public class PgSchemaTestModelCacheKeyFactory : IModelCacheKeyFactory
        {
            public object Create(DbContext context, bool designTime)
            {
                return (context.GetType(), ((PgSchemaTestContext)context).DefaultSchema, designTime);
            }
        }

        public class PgSchemaTestContext : DbContext
        {
            public PgSchemaTestContext(DbContextOptions<PgSchemaTestContext> options, string defaultSchema = null) : base(options)
            {
                DefaultSchema = defaultSchema;
            }

            public string DefaultSchema { get; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                if (DefaultSchema != null)
                {
                    modelBuilder.HasDefaultSchema(DefaultSchema);
                }
            }
        }
    }
}
