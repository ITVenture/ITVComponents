using ITVComponents.EFRepo.DbContextConfig;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.BinderContext.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.VirtualModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.BinderContext
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
