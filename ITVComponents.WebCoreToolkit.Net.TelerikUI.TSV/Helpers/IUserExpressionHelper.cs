using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Helpers
{
    public interface IUserExpressionHelper<TUserId, TUser, TTenantUser>
    {
        Expression<Func<TUser, bool>> EqualsUserId(TUserId id);

        Expression<Func<TTenantUser, bool>> EqualsUserTenantId(TUserId userId, int tenantId);

        TUserId UserId(TUser user);
    }
}
