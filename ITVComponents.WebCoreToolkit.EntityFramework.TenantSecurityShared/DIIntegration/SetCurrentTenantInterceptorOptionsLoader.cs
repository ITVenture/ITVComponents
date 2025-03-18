using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.EFRepo.Interceptors;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Interceptors;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.DIIntegration
{
    public class SetCurrentTenantInterceptorOptionsLoader<TContext> : ContextOptionsLoader<TContext> where TContext : DbContext
    {
        private readonly IServiceProvider services;
        private readonly SetTenantType tenantType;

        public SetCurrentTenantInterceptorOptionsLoader(ContextOptionsLoader<TContext> parent, IServiceProvider services, SetTenantType tenantType) : base(parent)
        {
            this.services = services;
            this.tenantType = tenantType;
        }
        protected override void ConfigureOptionsBuilder(DbContextOptionsBuilder<TContext> builder)
        {
            builder.AddInterceptors(new SetCurrentTenantInterceptor(services, tenantType));
        }
    }

    public enum SetTenantType
    {
        Tenant,
        BinderTenant
    }
}
