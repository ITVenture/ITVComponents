using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.GenericsHelperInterfaces
{
    public interface IContextExtensions
    {
        void ApplyTenantTemplates(IServiceProvider services, TenantType tenantType);
    }

    public interface IContextExtensions<TContext>:IContextExtensions
    {
        bool VerifyRoleName(TContext dbContext, string permissionName);

        bool EnsureNavUniqueness(TContext dbContext);

        bool IsCyclicRoleInheritance(TContext dbContext, int parentRole, int newChildRole);
    }
}
