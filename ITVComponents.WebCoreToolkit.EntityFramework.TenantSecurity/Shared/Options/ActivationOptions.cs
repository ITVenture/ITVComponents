using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.SettingsExtensions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options
{
    public class ActivationOptions
    {
        public bool ActivateDbContext { get; set; }

        public string ConnectionStringName { get; set; }

        public bool UseNavigation { get; set; }


        public bool UsePlugins { get; set; }

        public int PluginBufferDuration { get; set; }
        
        public bool UseLogAdapter { get; set; }

        public bool UseGlobalSettings { get; set; }

        public bool UseTenantSettings { get; set; }

        public bool UseSharedAssets { get; set; }

        public bool UseHealthChecks { get; set; }

        public bool ActivateFilters { get; set; }
        public bool ActivateTemplateFactory { get; set; }

        /// <summary>
        /// Selects the tenant-security strategy (flat vs. hierarchical tenants). Read by the
        /// onboarding WebPart to activate the matching global filters; intended as the single
        /// place where the strategy is configured once the tenant-security packages converge.
        /// </summary>
        public TenantStrategy Strategy { get; set; }

        /// <summary>
        /// Selects the identity backend (ASP.NET Core Identity vs. basic tenant-security). Combined with
        /// <see cref="Strategy"/> to pick the concrete security context for the consolidated tenant-security
        /// registration.
        /// </summary>
        public IdentityStrategy Identity { get; set; }

        /*public bool ActivateCreateModifyAttributes { get; set; }

        public bool UseUTCForCreateModifyAttributes { get; set; }*/

        public bool UseRoleInheritance { get; set; } = false;

        public bool ActivateDefaultContextUserProvider { get; set; }

        [AutoResolveChildren]
        public List<HealthCheckDefinition> HealthChecks { get; set; } = new();

        public bool UseApplicationTokens { get; set; }
        public bool UseApplicationIdentitySchema { get; set; } = true;

        public bool UseContextLocalizationServices { get; set; } = false;
        public bool UseDefaultInterceptors { get; set; } = true;

        public bool UseServerCookies { get; set; }

        public int DefaultServerCookieValidity { get; set; } = 1;

        public int CookieLengthThreshold { get; set; } = 2048;
        public bool UseDefaultSecurityAccessProvider { get; set; } = true;
    }
}
