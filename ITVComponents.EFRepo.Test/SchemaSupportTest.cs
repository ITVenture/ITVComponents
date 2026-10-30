using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.EFRepo.DbContextConfig.Impl;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.EFRepo.SqlServer;
using ITVComponents.EFRepo.SqlServer.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.EFRepo.Test
{
    [TestClass]
    public class SchemaSupportTest
    {
        private const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=SchemaSupportTest;Trusted_Connection=True;";

        [TestMethod]
        public void GeneratorQualifiesSchemalessOperations()
        {
            var sql = GenerateScript("partner", CreateOperations());
            StringAssert.Contains(sql, "[partner].[Child]");
            StringAssert.Contains(sql, "REFERENCES [partner].[Parent]");
            StringAssert.Contains(sql, "[partner].[Tenants]");
            StringAssert.Contains(sql, "[partner].[Old]");
            Assert.IsFalse(sql.Contains(MigrationSchema.Placeholder), sql);
            Assert.IsFalse(sql.Contains("[dbo]"), sql);
        }

        [TestMethod]
        public void GeneratorWithoutSchemaOnlyResolvesPlaceholder()
        {
            var sql = GenerateScript(null, CreateOperations());
            StringAssert.Contains(sql, "[dbo].[Tenants]");
            Assert.IsFalse(sql.Contains(MigrationSchema.Placeholder), sql);
            Assert.IsFalse(sql.Contains("[dbo].[Child]"), sql);
            StringAssert.Contains(sql, "CREATE TABLE [Child]");
        }

        [TestMethod]
        public void GeneratorEnsuresSchemaOnlyWhenRequested()
        {
            Assert.IsFalse(GenerateScript("partner", CreateOperations()).Contains("CREATE SCHEMA"));
            StringAssert.Contains(GenerateScript("partner", CreateOperations(), true), "CREATE SCHEMA [partner]");
        }

        [TestMethod]
        public void HistoryTableMovesIntoConfiguredSchema()
        {
            using var context = new SchemaTestContext(CreateOptions("partner"));
            var script = context.GetService<IHistoryRepository>().GetCreateScript();
            StringAssert.Contains(script, "[partner].[__EFMigrationsHistory]");
        }

        [TestMethod]
        public void SchemaReferenceIsResolvedFromConfiguration()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { { "Database:Schema", "partner" } }).Build();
            Assert.AreEqual("partner", SchemaConfiguration.Resolve("->Database:Schema", config));
            Assert.AreEqual("partner", SchemaConfiguration.Resolve(":-->Database:Schema", config));
            Assert.AreEqual("fallback", SchemaConfiguration.Resolve("->Database:Missing??fallback", config));
            Assert.IsNull(SchemaConfiguration.Resolve("->Database:Missing", config));
            Assert.AreEqual("direct", SchemaConfiguration.Resolve("direct", config));
            Assert.IsNull(SchemaConfiguration.Resolve(null, config));
        }

        [TestMethod]
        public void DbFunctionsFollowTheDefaultSchemaRegardlessOfOrder()
        {
            using (var context = new SchemaTestContext(CreateOptions(null), "partner", schemaFirst: false))
            {
                Assert.AreEqual("partner", context.Model.GetDbFunctions().Single().Schema);
                Assert.AreEqual("partner", context.GetDbSchema());
                Assert.AreEqual("[partner].[Sequences]", context.QualifiedObjectName("Sequences"));
            }

            using (var context = new SchemaTestContext(CreateOptions(null), "partner2", schemaFirst: true))
            {
                Assert.AreEqual("partner2", context.Model.GetDbFunctions().Single().Schema);
            }

            using (var context = new SchemaTestContext(CreateOptions(null), null, schemaFirst: false))
            {
                Assert.AreEqual("dbo", context.Model.GetDbFunctions().Single().Schema);
                Assert.AreEqual("dbo", context.GetDbSchema());
            }
        }

        [TestMethod]
        public void SyntaxProviderQualifiesTableNames()
        {
            var provider = new SqlQuerySyntaxProvider("partner");
            Assert.AreEqual("[partner].[Items]", provider.FormatTableName("Items"));
            Assert.AreEqual("[other].[Items]", provider.FormatTableName("other.Items"));
            Assert.AreEqual("[dbo].[Items]", new SqlQuerySyntaxProvider().FormatTableName("Items"));
            Assert.AreEqual("[t].[Col]", provider.FullQualifyColumn("t", "Col", false));
        }

        private static string GenerateScript(string schema, List<MigrationOperation> operations, bool ensureSchema = false)
        {
            using var context = new SchemaTestContext(CreateOptions(schema, ensureSchema));
            var generator = context.GetService<IMigrationsSqlGenerator>();
            Assert.IsInstanceOfType(generator, typeof(SchemaAwareMigrationsSqlGenerator));
            return string.Join(Environment.NewLine, generator.Generate(operations).Select(n => n.CommandText));
        }

        private static DbContextOptions<SchemaTestContext> CreateOptions(string schema, bool ensureSchema = false)
        {
            var builder = new DbContextOptionsBuilder<SchemaTestContext>();
            builder.UseSqlServer(ConnectionString);
            builder.UseSchemaAwareMigrations(schema, ensureSchema);
            // the model differs per default-schema of the test-context, so the model-cache must distinguish them
            builder.ReplaceService<IModelCacheKeyFactory, SchemaTestModelCacheKeyFactory>();
            return builder.Options;
        }

        private static List<MigrationOperation> CreateOperations()
        {
            var child = new CreateTableOperation { Name = "Child" };
            child.Columns.Add(new AddColumnOperation { Name = "Id", Table = "Child", ClrType = typeof(int), ColumnType = "int" });
            child.Columns.Add(new AddColumnOperation { Name = "ParentId", Table = "Child", ClrType = typeof(int), ColumnType = "int" });
            child.PrimaryKey = new AddPrimaryKeyOperation { Name = "PK_Child", Table = "Child", Columns = new[] { "Id" } };
            child.ForeignKeys.Add(new AddForeignKeyOperation { Name = "FK_Child_Parent", Table = "Child", Columns = new[] { "ParentId" }, PrincipalTable = "Parent", PrincipalColumns = new[] { "Id" } });
            return new List<MigrationOperation>
            {
                child,
                new SqlOperation { Sql = $"CREATE VIEW [{MigrationSchema.Placeholder}].[TenantView] AS SELECT * FROM [{MigrationSchema.Placeholder}].[Tenants]" },
                new RenameTableOperation { Name = "Old", NewName = "Renamed" }
            };
        }

        public class SchemaTestModelCacheKeyFactory : IModelCacheKeyFactory
        {
            public object Create(DbContext context, bool designTime)
            {
                var ctx = (SchemaTestContext)context;
                return (ctx.GetType(), ctx.DefaultSchema, ctx.SchemaFirst, designTime);
            }
        }

        public class SchemaTestContext : DbContext
        {
            private readonly string defaultSchema;

            private readonly bool schemaFirst;

            public SchemaTestContext(DbContextOptions<SchemaTestContext> options) : this(options, null, false)
            {
            }

            public SchemaTestContext(DbContextOptions<SchemaTestContext> options, string defaultSchema, bool schemaFirst) : base(options)
            {
                this.defaultSchema = defaultSchema;
                this.schemaFirst = schemaFirst;
            }

            public string DefaultSchema => defaultSchema;

            public bool SchemaFirst => schemaFirst;

            public static int TreeFunction(int value) => throw new NotSupportedException();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                if (schemaFirst && defaultSchema != null)
                {
                    new SchemaRedirect(defaultSchema).ConfigureEntity(modelBuilder);
                }

                modelBuilder.HasDbFunction(typeof(SchemaTestContext).GetMethod(nameof(TreeFunction))).HasName("TreeFunction");
                if (!schemaFirst && defaultSchema != null)
                {
                    new SchemaRedirect(defaultSchema).ConfigureEntity(modelBuilder);
                }
            }
        }
    }
}
