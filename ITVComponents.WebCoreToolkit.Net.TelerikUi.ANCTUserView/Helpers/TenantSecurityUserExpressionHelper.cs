using System;
using System.Linq.Expressions;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Helpers;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTenantSecurityUserView.Helpers
{
    public class TenantSecurityUserExpressionHelper:IUserExpressionHelper<string,User,TenantUser, RoleRole>
    {
        public Expression<Func<User, bool>> EqualsUserId(string id)
        {
            return u => u.Id == id;
        }

        public Expression<Func<TenantUser, bool>> EqualsUserTenantId(string userId, int tenantId)
        {
            return u => u.UserId == userId && u.TenantId == tenantId;
        }

        public string UserId(User user)
        {
            return user.Id;
        }
    }
}
