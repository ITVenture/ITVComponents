using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.EFRepo.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.BinderContext
{
    public class BinderConfigurationForPartialContext<TContext> : DbModelBuilderOptionsProvider<TContext> where TContext : Microsoft.EntityFrameworkCore.DbContext
    {
        private readonly string userTable;
        private readonly string tenantUserTable;

        public BinderConfigurationForPartialContext(string userTable, string tenantUserTable) : base()
        {
            this.userTable = userTable;
            this.tenantUserTable = tenantUserTable;
        }
        public BinderConfigurationForPartialContext(string userTable, string tenantUserTable, DbModelBuilderOptionsProvider<TContext> parent) : base(parent)
        {
            this.userTable = userTable;
            this.tenantUserTable = tenantUserTable;
        }

        protected override void Configure(DbContextModelBuilderOptions<TContext> options)
        {
            options.AddCustomConfigurator(new BinderModelEntityConfigurator(userTable, tenantUserTable));
        }
    }
}
