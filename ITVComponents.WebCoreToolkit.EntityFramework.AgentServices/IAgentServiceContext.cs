using ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AgentServices
{
    /// <summary>
    /// Die Tabelle der Bindungen — der Teil des Vertrags, den die Dienste dieses Pakets brauchen.
    /// </summary>
    public interface IAgentServiceBindingContext
    {
        /// <summary>Welche Kasse welches Gerät in welcher Rolle benutzt.</summary>
        DbSet<ClientAppServiceBinding> ClientAppServiceBindings { get; set; }
    }

    /// <summary>
    /// Der Kontext-Vertrag der Agent-Dienste: Mandant (über <see cref="IBaseTenantContext{TTenant,TWebPlugin,TWebPluginConstant,TWebPluginGenericParameter,TSequence,TTenantSetting,TTenantFeatureActivation,TExternalOAuthService,TExternalOAuthServiceState,TExternalOAuthServiceTenantLogin,TTrustConfig}"/>),
    /// ClientApps (über <see cref="IClientAppContext{TClientApp,TClientAppTemplate}"/>) und die Bindungen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Opt-in: wer keinen Agenten hat, implementiert ihn nicht und bekommt keine Tabelle. Die Typparameter
    /// heissen wie die des Sicherheitskontexts — das WebPart bindet sie nach Namen.
    /// </para>
    /// <para>
    /// Die Rechte-Kette der ClientApp sieht dieser Vertrag nicht: <see cref="IClientAppContext{TClientApp,TClientAppTemplate}"/>
    /// verlangt nur die schmalen Core-Schnittstellen.
    /// </para>
    /// </remarks>
    public interface IAgentServiceContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence,
        TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState,
        TExternalOAuthServiceTenantLogin, TTrustConfig, TClientApp, TClientAppTemplate>
        : IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting,
            TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin,
            TTrustConfig>,
          IClientAppContext<TClientApp, TClientAppTemplate>,
          IAgentServiceBindingContext
        where TTenant : Tenant
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
        where TClientApp : class, IClientAppCore
        where TClientAppTemplate : class, IClientAppTemplateCore
    {
    }
}
