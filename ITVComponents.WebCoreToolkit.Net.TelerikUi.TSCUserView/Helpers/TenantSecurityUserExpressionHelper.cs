using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Helpers;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityContextUserView.Helpers
{
    public class TenantSecurityUserExpressionHelper:IUserExpressionHelper<int,User,TenantUser>
    {
        public Expression<Func<User, bool>> EqualsUserId(int id)
        {
            return u => u.UserId == id;
        }

        public Expression<Func<TenantUser, bool>> EqualsUserTenantId(int userId, int tenantId)
        {
            return u => u.UserId == userId && u.TenantId == tenantId;
        }

        public int UserId(User user)
        {
            return user.UserId;
        }
    }
}
