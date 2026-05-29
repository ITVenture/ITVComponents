using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.BinderModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.BinderContext.Model
{
    [BinderEntity]
    public class BinderTenantUser:BinderTenantUser<string, BinderUser>
    {
    }
}
