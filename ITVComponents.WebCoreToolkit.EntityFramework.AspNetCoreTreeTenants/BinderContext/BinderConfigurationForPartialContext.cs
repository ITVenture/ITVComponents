using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.BinderContext
{
    public class BinderConfigurationForPartialContext<TContext> : DbModelBuilderOptionsProvider<TContext> where TContext : Microsoft.EntityFrameworkCore.DbContext
    {
        private readonly string userTable;
        private readonly string tenantUserTable;
        private readonly bool mapProcedures;

        public BinderConfigurationForPartialContext(string userTable, string tenantUserTable, bool mapProcedures) : base()
        {
            this.userTable = userTable;
            this.tenantUserTable = tenantUserTable;
            this.mapProcedures = mapProcedures;
        }
        public BinderConfigurationForPartialContext(string userTable, string tenantUserTable, bool mapProcedures, DbModelBuilderOptionsProvider<TContext> parent) : base(parent)
        {
            this.userTable = userTable;
            this.tenantUserTable = tenantUserTable;
            this.mapProcedures = mapProcedures;
        }

        protected override void Configure(DbContextModelBuilderOptions<TContext> options)
        {
            options.AddCustomConfigurator(new BinderModelEntityConfigurator(userTable, tenantUserTable));
        }
    }
}
