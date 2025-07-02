using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.VirtualModels;
using Microsoft.EntityFrameworkCore;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared
{
    [ExplicitlyExpose]
    public interface IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TTrustConfig> : 
        IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TTrustConfig> 
        where TTenant : HierarchyTenant 
        where TWebPlugin : HierarchyWebPlugin<TTenant,TWebPlugin,TWebPluginGenericParameter>
        where TWebPluginConstant : HierarchyWebPluginConstant<TTenant>
        where TWebPluginGenericParameter : HierarchyWebPluginGenericParameter<TTenant, TWebPlugin,TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : HierarchyTenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TTrustConfig : HierarchyTenantContextSecurityTrustConfig, new()
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
