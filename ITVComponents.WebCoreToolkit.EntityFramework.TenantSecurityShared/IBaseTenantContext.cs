using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Interfaces;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared
{
    [ExplicitlyExpose]
    public interface IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> :IUserAwareContext, ICoreSystemContext 
    where TTenant: Tenant
    where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TWebPluginConstant: WebPluginConstant<TTenant>
    where TWebPluginGenericParameter:WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TSequence:Sequence<TTenant>
    where TTenantSetting: TenantSetting<TTenant>
    where TTenantFeatureActivation: TenantFeatureActivation<TTenant>
    {
        /// <summary>
        /// Gets the Id of the current Tenant. If no TenantProvider was provided, this value is null.
        /// </summary>
        int? CurrentTenantId
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

        public int SequenceNextVal(string sequenceName);
    }
}
