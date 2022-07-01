
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public bool UseSharedAssets { get; set; }
        public List<LocalizationMappingOption> UiCultureMapping { get; set; } = new();
        public List<LocalizationMappingOption> CultureMapping { get; set; } = new();

        public List<PlugInDependencyOption> PlugInDependencies { get; set; } = new();

        public Dictionary<string, string> GroupClaims { get; set; } = new();
    }

    public class LocalizationMappingOption
    {
        public string IncomingCulture { get; set; }

        public string RedirectCulture { get; set; }
    }
}
