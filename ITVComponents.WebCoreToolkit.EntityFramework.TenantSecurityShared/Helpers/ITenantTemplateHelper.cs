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

        void ApplyTemplate(TTenant tenant, TenantTemplateMarkup template);

        void RevokeTemplate(TTenant tenant, TenantTemplateMarkup template);

        void ApplyTemplate(TTenant tenant, TenantTemplateMarkup template, Action<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>> afterApply);

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
