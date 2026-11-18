using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Extensions
{
    /// <summary>
    /// Maps the tables of the update service.
    /// </summary>
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// Maps the tables - call it from <c>OnModelCreating</c>, so that the migrations see the same model as the runtime.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item>A release is unique per product, channel and version; deleting a product deletes its releases.</item>
        /// <item>Package blocks are keyed by storage key and block number, without foreign key: the store is independent of
        /// the releases (a package is written before its release row exists).</item>
        /// <item>Assignments of a client app go with it (<c>Cascade</c>); the tenant is <c>NoAction</c> - otherwise there
        /// would be two cascade paths, which SQL Server refuses. The same for the state.</item>
        /// </list>
        /// </remarks>
        public static ModelBuilder ConfigureUpdateService<TTenant, TClientApp, TClientAppTemplate>(this ModelBuilder modelBuilder)
            where TTenant : Tenant
            where TClientApp : class
            where TClientAppTemplate : class
        {
            modelBuilder.Entity<UpdateProduct>(b =>
            {
                b.ToTable("UpdateProducts");
                b.HasIndex(n => n.ProductKey).IsUnique().HasDatabaseName("UQ_UpdateProduct_Key");
            });

            modelBuilder.Entity<UpdateRelease>(b =>
            {
                b.ToTable("UpdateReleases");
                b.HasIndex(n => new { n.UpdateProductId, n.Channel, n.Version }).IsUnique().HasDatabaseName("UQ_UpdateRelease_Version");
                b.HasIndex(n => n.StorageKey).IsUnique().HasDatabaseName("UQ_UpdateRelease_Storage");
                b.HasOne<UpdateProduct>().WithMany().HasForeignKey(n => n.UpdateProductId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UpdatePackageChunk>(b =>
            {
                b.ToTable("UpdatePackageChunks");
                b.HasKey(n => new { n.StorageKey, n.ChunkIndex });
            });

            modelBuilder.Entity<ClientAppTemplateModuleAssignment>(b =>
            {
                b.ToTable("ClientAppTemplateModuleAssignments");
                b.HasIndex(n => new { n.ClientAppTemplateId, n.ModuleProductKey }).IsUnique().HasDatabaseName("UQ_TemplateModule");
                b.HasOne<TClientAppTemplate>().WithMany().HasForeignKey(n => n.ClientAppTemplateId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ClientAppModuleAssignment>(b =>
            {
                b.ToTable("ClientAppModuleAssignments");
                b.HasIndex(n => new { n.ClientAppId, n.ModuleProductKey }).IsUnique().HasDatabaseName("UQ_ClientAppModule");
                b.HasIndex(n => n.TenantId).HasDatabaseName("IX_ClientAppModule_Tenant");
                b.HasOne<TClientApp>().WithMany().HasForeignKey(n => n.ClientAppId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne<TTenant>().WithMany().HasForeignKey(n => n.TenantId).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ClientAppUpdateState>(b =>
            {
                b.ToTable("ClientAppUpdateStates");
                b.Property(n => n.ClientAppId).ValueGeneratedNever();
                b.HasIndex(n => n.TenantId).HasDatabaseName("IX_ClientAppUpdateState_Tenant");
                b.HasOne<TClientApp>().WithMany().HasForeignKey(n => n.ClientAppId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne<TTenant>().WithMany().HasForeignKey(n => n.TenantId).OnDelete(DeleteBehavior.NoAction);
            });

            return modelBuilder;
        }
    }
}
