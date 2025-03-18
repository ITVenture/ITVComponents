using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.BinderModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.BinderContext.Model
{
    [BinderEntity]
    public class BinderTenantUser:BinderTenantUser<string, BinderUser>
    {
    }
}
