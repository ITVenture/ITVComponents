using ITVComponents.WebCoreToolkit.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Interfaces;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared
{
    
    [ExplicitlyExpose]
    public interface ICoreSystemContext
    {
        public DbSet<AuthenticationType> AuthenticationTypes { get; set; }

        public DbSet<AuthenticationClaimMapping> AuthenticationClaimMappings { get; set; }

        public DbSet<HealthScript> HealthScripts { get; set; }

        public DbSet<GlobalSetting> GlobalSettings { get; set; }

        public DbSet<SystemEvent> SystemLog { get; set; }

        public DbSet<Culture> Cultures { get; set; }

        public DbSet<Models.Localization> Localizations { get; set; }

        public DbSet<LocalizationCulture> LocalizationCultures { get; set; }

        public DbSet<LocalizationString> LocalizationCultureStrings { get; set; }

        public DbSet<VideoTutorial> Tutorials { get; set; }

        public DbSet<TutorialStream> TutorialStreams { get; set; }

        public DbSet<TrustedFullAccessComponent> TrustedFullAccessComponents { get; set; }

        public DbSet<TemplateModule> TemplateModules { get; set; }

        public DbSet<Feature> Features { get; set; }

        public DbSet<TemplateModuleConfigurator> TemplateModuleConfigurators { get; set; }

        public DbSet<TemplateModuleConfiguratorParameter> TemplateModuleConfiguratorParameters { get; set; }

        public DbSet<TemplateModuleScript> TemplateModuleScripts { get; set; }

        public DbSet<TenantTemplate> TenantTemplates { get; set; }

        public DbSet<TenantType> TenantTypes { get; set; }

        public DbSet<ServerCookie> ServerCookies { get; set; }

        DatabaseFacade Database { get; }

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

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken));

        int SaveChanges();
    }

    [ExplicitlyExpose]
    public interface ICoreSystemContext<TTrustConfig> : ITrustfulComponent<TTrustConfig>, ICoreSystemContext
        where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
    {
    }
}
