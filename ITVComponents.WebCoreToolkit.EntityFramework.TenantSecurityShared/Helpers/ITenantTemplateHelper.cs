using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers
{
    public interface ITenantTemplateHelper
    {
        TenantTemplateMarkup ExtractTemplate(Tenant tenant);

        void ApplyTemplate(Tenant tenant, TenantTemplateMarkup template);

        void RevokeTemplate(Tenant tenant, TenantTemplateMarkup template);
    }
}
