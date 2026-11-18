using System;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Test
{
    public class TestClientApp : IClientAppCore
    {
        public int ClientAppId { get; set; }
        public int TenantId { get; set; }
        public int ClientAppTemplateId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientKey { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public DateTime? LastSeenUtc { get; set; }
    }

    public class TestClientAppTemplate : IClientAppTemplateCore
    {
        public int ClientAppTemplateId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CatalogObjectName { get; set; } = string.Empty;
        public string TypeKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// The tables of the update service, without the tenant model: the tests are about stores, feed and administration,
    /// not about the mapping to SQL Server.
    /// </summary>
    public class UpdateTestContext : DbContext, IUpdateServiceContext, IClientAppContext<TestClientApp, TestClientAppTemplate>
    {
        public UpdateTestContext(DbContextOptions<UpdateTestContext> options) : base(options)
        {
        }

        public DbSet<UpdateProduct> UpdateProducts { get; set; } = null!;
        public DbSet<UpdateRelease> UpdateReleases { get; set; } = null!;
        public DbSet<UpdatePackageChunk> UpdatePackageChunks { get; set; } = null!;
        public DbSet<ClientAppTemplateModuleAssignment> ClientAppTemplateModuleAssignments { get; set; } = null!;
        public DbSet<ClientAppModuleAssignment> ClientAppModuleAssignments { get; set; } = null!;
        public DbSet<ClientAppUpdateState> ClientAppUpdateStates { get; set; } = null!;
        public DbSet<TestClientApp> ClientApps { get; set; } = null!;
        public DbSet<TestClientAppTemplate> ClientAppTemplates { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestClientApp>().HasKey(a => a.ClientAppId);
            modelBuilder.Entity<TestClientAppTemplate>().HasKey(t => t.ClientAppTemplateId);
            modelBuilder.Entity<UpdatePackageChunk>().HasKey(c => new { c.StorageKey, c.ChunkIndex });
            modelBuilder.Entity<ClientAppUpdateState>().Property(s => s.ClientAppId).ValueGeneratedNever();
        }
    }

    public class UpdateTestContextFactory : IDbContextFactory<UpdateTestContext>
    {
        private readonly DbContextOptions<UpdateTestContext> options;

        public UpdateTestContextFactory()
        {
            options = new DbContextOptionsBuilder<UpdateTestContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        }

        public UpdateTestContext CreateDbContext() => new UpdateTestContext(options);
    }
}
