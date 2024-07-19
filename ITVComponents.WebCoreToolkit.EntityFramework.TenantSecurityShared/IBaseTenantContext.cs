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
    public interface IBaseTenantContext:IUserAwareContext, ITrustfulComponent<BaseTenantContextSecurityTrustConfig>
    {
        /// <summary>
        /// Gets the Id of the current Tenant. If no TenantProvider was provided, this value is null.
        /// </summary>
        int? CurrentTenantId
        {
            get;
        }

        /// <summary>
        /// Indicates whether to switch off tenant filtering
        /// </summary>
        bool ShowAllTenants { get; set; }

        /// <summary>
        /// When tenant filtering is used, this hides tenant-relevant records that are NOT bound to a specific tenant
        /// </summary>
        bool HideGlobals { get; set; }

        /// <summary>
        /// Indicates whether there is a current http context
        /// </summary>
        bool FilterAvailable { get; }

        public DbSet<AuthenticationType> AuthenticationTypes { get; set; }

        public DbSet<AuthenticationClaimMapping> AuthenticationClaimMappings { get; set; }

        public DbSet<HealthScript> HealthScripts { get; set; }

        public DbSet<Feature> Features { get; set; }

        public DbSet<TemplateModule> TemplateModules { get; set; }

        public DbSet<TemplateModuleConfigurator> TemplateModuleConfigurators { get; set; }

        public DbSet<TemplateModuleConfiguratorParameter> TemplateModuleConfiguratorParameters { get; set; }

        public DbSet<TemplateModuleScript> TemplateModuleScripts { get; set; }

        public DbSet<Tenant> Tenants { get; set; }

        public DbSet<TenantFeatureActivation> TenantFeatureActivations { get; set; }

        public DbSet<TenantSetting> TenantSettings { get; set; }

        public DbSet<TenantTemplate> TenantTemplates { get; set; }

        public DbSet<WebPlugin> WebPlugins { get; set; }

        public DbSet<WebPluginConstant> WebPluginConstants { get; set; }

        public DbSet<WebPluginGenericParameter> GenericPluginParams { get; set; }

        public DbSet<GlobalSetting> GlobalSettings { get; set; }

        public DbSet<SystemEvent> SystemLog { get; set; }

        public DbSet<VideoTutorial> Tutorials { get; set; }

        public DbSet<TutorialStream> TutorialStreams { get; set; }

        public DbSet<TrustedFullAccessComponent> TrustedFullAccessComponents { get; set; }

        public DbSet<Sequence> Sequences { get; set; }

        public DbSet<Culture> Cultures { get; set; }

        public DbSet<Models.Localization> Localizations { get; set; }

        public DbSet<LocalizationCulture> LocalizationCultures { get; set; }

        public DbSet<LocalizationString> LocalizationCultureStrings { get; set; }

        public int SequenceNextVal(string sequenceName);

        DatabaseFacade Database { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken));

        int SaveChanges();
    }
}
