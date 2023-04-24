using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers
{
    public interface ITenantTemplateHelper
    {
        TenantTemplateMarkup ExtractTemplate(Tenant tenant);

        void ApplyTemplate(Tenant tenant, TenantTemplateMarkup template);

        void RevokeTemplate(Tenant tenant, TenantTemplateMarkup template);

        void ApplyTemplate(Tenant tenant, TenantTemplateMarkup template, Action<IBaseTenantContext> afterApply);

        void RevokeTemplate(Tenant tenant, TenantTemplateMarkup template, Action<IBaseTenantContext> afterRevoke);
    }

    /*public interface ITenantTemplateHelper<TContext>:ITenantTemplateHelper
    where TContext: IBaseTenantContext
    {
        TenantTemplateMarkup ExtractTemplate(Tenant tenant);

        void ApplyTemplate(Tenant tenant, TenantTemplateMarkup template, Action<TContext> afterApply);

        void RevokeTemplate(Tenant tenant, TenantTemplateMarkup template, Action<TContext> afterRevoke);
    }*/
}
