
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dynamitey.DynamicObjects;

namespace ITVComponents.WebCoreToolkit.Options
{
    public class WebPartInitOptions
    {
        public bool UseSimpleUserNameMapping { get; set; }
        public bool InitializePluginSystem { get; set; }
        public bool UseInitPlugins { get; set; } = false;
        public bool EnablePermissionBaseAuthorization { get; set; }
        public bool EnableFeatureBasedAuthorization { get; set; }
        public bool UseContextUserAccessor { get; set; }
        public bool UseUser2GroupMapper { get; set; }
        public bool UseCollectedClaimsTransformation { get; set; }
        public bool UseRepositoryClaimsTransformation { get; set; }
        public bool UseNavigator { get; set; }
        public bool UseScopedSettings { get; set; }
        public bool UseGlobalSettings { get; set; }
        public bool UseHierarchySettings { get; set; }
        public bool UseToolkitMvcMessages { get; set; }
        public bool UseUrlFormatter { get; set; }
        public bool UseBackgroundTasks { get; set; }
        public int TaskQueueCapacity { get; set; } = 100;
        public bool UseLocalization { get; set; }

        /// <summary>
        /// ResourcesPath that <see cref="Microsoft.Extensions.Localization.LocalizationOptions"/>
        /// will be configured with when <see cref="UseLocalization"/> is true. <c>null</c> (the
        /// default) preserves the historical toolkit behavior of <c>"Resources"</c>; an empty
        /// string opts out of any path prefix entirely (resource type FullName is used as-is for
        /// the resource base name); any other string sets the path explicitly.
        /// </summary>
        public string? ResourcesPath { get; set; }

        public bool UsePageModelHandlerFactory { get; set; }
        public bool UseSharedAssets { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the deprecated query form of a shared-asset link
        /// (<c>?SharedAssetKey=…&amp;__AccessToken=…</c>) is still accepted. Defaults to true so links that
        /// were already sent out keep working; new links are always created in the path form
        /// (<c>/~{key}[.{token}]/…</c>). Only relevant when <see cref="UseSharedAssets"/> is on.
        /// </summary>
        public bool AcceptQuerySharedAssetKey { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the <c>Referer</c> may serve as a last resort for the
        /// deprecated query form. The path form needs no such fallback - sub-resources inherit the prefix.
        /// </summary>
        public bool AcceptSharedAssetRefererFallback { get; set; } = true;
        public List<CultureConfigOption> CultureConfig { get; set; } = new();
        public List<LocalizationMappingOption> CultureMapping { get; set; } = new();
        public List<LocalizationMappingOption> UiCultureMapping { get; set; } = new();

        public List<PlugInDependencyOption> PlugInDependencies { get; set; } = new();

        public List<PageHandlerOptions> PageHandlers { get; set; } = new();

        public Dictionary<string, string> GroupClaims { get; set; } = new();
        public bool UseDefaultCookies { get; set; } = true;
        public bool UseOAuthClientFactory { get; set; } = false;
    }

    public class LocalizationMappingOption
    {
        public string IncomingCulture { get; set; }

        public string RedirectCulture { get; set; }
    }
}
