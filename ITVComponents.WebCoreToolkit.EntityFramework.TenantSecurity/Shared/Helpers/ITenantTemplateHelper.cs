using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers
{
    public interface ITenantTemplateHelper<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> 
        where TTenant: Tenant 
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation: TenantFeatureActivation<TTenant>
        where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
        where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    {
        
        TenantTemplateMarkup ExtractTemplate(TTenant tenant);

        void ApplyAllTenantsFor(int tenantTypeId);

        /// <summary>
        /// Re-applies the template attached to the tenant's own <c>TenantType</c> (resolved via
        /// <c>Tenant.TenantTypeId</c>). No-op (logged) when the tenant has no type or the type carries no template.
        /// Per-kind template modes win over <paramref name="defaultMode"/>.
        /// </summary>
        void ApplyTenantTypeTemplate(TTenant tenant, TemplateApplyMode defaultMode);

        void ApplyTemplate(TTenant tenant, TenantTemplateMarkup template);

        /// <summary>
        /// Applies the template with an explicit default apply mode. Per-kind modes on the template override it; an
        /// <see cref="TemplateApplyMode.Auto"/> default resolves to <see cref="TemplateApplyMode.Forced"/>.
        /// </summary>
        void ApplyTemplate(TTenant tenant, TenantTemplateMarkup template, TemplateApplyMode defaultMode);

        void RevokeTemplate(TTenant tenant, TenantTemplateMarkup template);

        void ApplyTemplate(TTenant tenant, TenantTemplateMarkup template, Action<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>> afterApply);

        void ApplyTemplate(TTenant tenant, TenantTemplateMarkup template, Action<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>> afterApply, TemplateApplyMode defaultMode);

        /// <summary>
        /// Applies a template on an EXTERNALLY supplied, caller-owned context instead of leasing a fresh one. Use this
        /// when the template application must participate in a transaction the caller already opened (e.g. atomic
        /// tenant onboarding): all writes go through <paramref name="externalContext"/>, so they enlist in the caller's
        /// ambient transaction and commit/roll back together. The caller owns the context and the transaction — this
        /// method neither disposes the context nor commits. <paramref name="externalContext"/> must be the concrete
        /// security-context type the lease would otherwise have produced.
        /// </summary>
        void ApplyTemplate(DbContext externalContext, TTenant tenant, TenantTemplateMarkup template, Action<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>> afterApply);

        void ApplyTemplate(DbContext externalContext, TTenant tenant, TenantTemplateMarkup template, Action<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>> afterApply, TemplateApplyMode defaultMode);

        void RevokeTemplate(TTenant tenant, TenantTemplateMarkup template, Action<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>> afterRevoke);

    }

    /*public interface ITenantTemplateHelper<TContext>:ITenantTemplateHelper
    where TContext: IBaseTenantContext
    {
        TenantTemplateMarkup ExtractTemplate(Tenant tenant);

        void ApplyTemplate(Tenant tenant, TenantTemplateMarkup template, Action<TContext> afterApply);

        void RevokeTemplate(Tenant tenant, TenantTemplateMarkup template, Action<TContext> afterRevoke);
    }*/
}
