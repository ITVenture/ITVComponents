using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.VirtualModels;
using Microsoft.EntityFrameworkCore;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared
{
    [ExplicitlyExpose]
    public interface IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : 
        IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> 
        where TTenant : HierarchyTenant 
        where TWebPlugin : HierarchyWebPlugin<TTenant,TWebPlugin,TWebPluginGenericParameter>
        where TWebPluginConstant : HierarchyWebPluginConstant<TTenant>
        where TWebPluginGenericParameter : HierarchyWebPluginGenericParameter<TTenant, TWebPlugin,TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : HierarchyTenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TTrustConfig : HierarchyTenantContextSecurityTrustConfig<TTrustConfig>, new()
        where TExternalOAuthService : HierarchyExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState : HierarchyExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : HierarchyExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    {
        /// <summary>
        /// When tenant filtering is used, enables the Tree-Inheritance User- and Permissionresolving
        /// </summary>
        bool IncludeParentTree { get; set; }

        bool IncludeChildTree { get; set; }

        /// <summary>
        /// Gets the TenantTree in case of activated IncludeParentTree flag
        /// </summary>
        IQueryable<int> CurrentTenantTree { get; }

        public DbSet<DownwardsTenantView> DownwardsTenantTreeView { get; set; }

        public DbSet<UpwardsTenantView> UpwardsTenantTreeView { get; set; }
    }
}
