using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.GenericsHelperInterfaces
{
    public interface IContextExtensions
    {
    }

    public interface IContextExtensions<TContext>:IContextExtensions
    {
        bool VerifyRoleName(TContext dbContext, string permissionName);

        bool EnsureNavUniqueness(TContext dbContext);

        bool IsCyclicRoleInheritance(TContext dbContext, int parentRole, int newChildRole);
    }
}
