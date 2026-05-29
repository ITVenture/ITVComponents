using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Interfaces;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared
{
    [ExplicitlyExpose]
    public interface IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> :IUserAwareContext, ICoreSystemContext<TTrustConfig> 
    where TTenant: Tenant
    where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TWebPluginConstant: WebPluginConstant<TTenant>
    where TWebPluginGenericParameter:WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TSequence:Sequence<TTenant>
    where TTenantSetting: TenantSetting<TTenant>
    where TTenantFeatureActivation: TenantFeatureActivation<TTenant>
    where TExternalOAuthService:ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceState: ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
    {
        /// <summary>
        /// Gets the Id of the current Tenant. If no TenantProvider was provided, this value is null.
        /// </summary>
        int? CurrentTenantId
        {
            get;
        }

        /// <summary>
        /// Gets the name of the current Tenant. If no TenantProvider was provided, this value is null.
        /// </summary>
        string CurrentTenantName 
        {
            get;
        }

        public DbSet<TTenant> Tenants { get; set; }

        public DbSet<TTenantFeatureActivation> TenantFeatureActivations { get; set; }

        public DbSet<TTenantSetting> TenantSettings { get; set; }

        public DbSet<TWebPlugin> WebPlugins { get; set; }

        public DbSet<TWebPluginConstant> WebPluginConstants { get; set; }

        public DbSet<TWebPluginGenericParameter> GenericPluginParams { get; set; }

        public DbSet<TSequence> Sequences { get; set; }

        public DbSet<TExternalOAuthService> ExternalOAuthServices { get; set; }

        public DbSet<TExternalOAuthServiceState> ExternalOAuthServiceStates { get; set; }

        public DbSet<TExternalOAuthServiceTenantLogin> ExternalOAuthServiceTenantLogins { get; set; }

        public int SequenceNextVal(string sequenceName);
    }
}
