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

        /// <summary>
        /// Hierarchy scenario only. When <c>false</c>, an onboarded tenant must end up with a parent —
        /// either an explicitly picked/invited one or <see cref="DefaultParentTenant"/>. If neither can be
        /// resolved, onboarding is rejected, so no further root tenants can be created. Defaults to <c>true</c>
        /// (current behaviour: roots may be created freely).
        /// </summary>
        public bool AllowRootTenantCreation { get; set; } = true;

        /// <summary>
        /// Hierarchy scenario only. Optional <c>TenantName</c> (fallback: <c>DisplayName</c>) of a tenant that
        /// newly onboarded tenants are subordinated under when no parent was explicitly picked or invited.
        /// When set, the create-tenant page hides the free parent picker and auto-assigns this parent.
        /// </summary>
        public string DefaultParentTenant { get; set; }
    }
}
