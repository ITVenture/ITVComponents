using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options
{
    /// <summary>
    /// Provider-agnostic options for the tenant onboarding flow.
    /// </summary>
    [SettingName("TenantSetup")]
    public class TenantSetupOptions
    {
        public string BasicTenantTemplate { get; set; }

        public string AdminUserRole { get; set; }

        public string SubscriptionAssetKey { get; set; }
    }
}
