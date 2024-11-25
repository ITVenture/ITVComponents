using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.VirtualModels;
using Microsoft.EntityFrameworkCore;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared
{
    public interface IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TTrustConfig> : IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TTrustConfig> 
        where TTenant : HierarchyTenant 
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TTrustConfig : BaseTenantContextSecurityTrustConfig, new()
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
