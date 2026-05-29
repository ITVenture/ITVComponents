using ITVComponents.EFRepo.DbContextConfig;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.BinderContext.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.VirtualModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.BinderContext
{
    public class BinderModelEntityConfigurator:IEntityConfigurator
    {
        private readonly string userTable;
        private readonly string tenantUserTable;

        public BinderModelEntityConfigurator(string userTable, string tenantUserTable)
        {
            this.userTable = userTable;
            this.tenantUserTable = tenantUserTable;
        }

        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BinderUser>().ToTable(userTable, b => b.ExcludeFromMigrations());
            modelBuilder.Entity<BinderTenantUser>().ToTable(tenantUserTable, b => b.ExcludeFromMigrations());

        }
    }
}
