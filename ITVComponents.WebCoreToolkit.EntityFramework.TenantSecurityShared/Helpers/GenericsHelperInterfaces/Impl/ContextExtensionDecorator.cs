using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.GenericsHelperInterfaces.Impl
{
    public class ContextExtensionDecorator<TContext, TWrapped>:IContextExtensions<TContext>
    where TWrapped:TContext
    {
        private readonly IContextExtensions<TWrapped> decorated;

        public ContextExtensionDecorator(IContextExtensions<TWrapped> decorated)
        {
            this.decorated = decorated;
        }
        public bool VerifyRoleName(TContext dbContext, string permissionName)
        {
            return decorated.VerifyRoleName((TWrapped)dbContext, permissionName);
        }

        public bool EnsureNavUniqueness(TContext dbContext)
        {
            return decorated.EnsureNavUniqueness((TWrapped)dbContext);
        }

        public bool IsCyclicRoleInheritance(TContext dbContext, int parentRole, int newChildRole)
        {
            return decorated.IsCyclicRoleInheritance((TWrapped)dbContext, parentRole, newChildRole);
        }
    }
}
