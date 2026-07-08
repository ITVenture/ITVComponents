using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options
{
    /// <summary>
    /// Provider-agnostic options for the tenant onboarding flow.
    /// </summary>
    [SettingName("TenantSetup")]
    public class TenantSetupOptions
    {
        /// <summary>
        /// Preferred way to pick the onboarding template: the <c>TenantTypeName</c> of a <c>TenantType</c> whose
        /// attached <c>TenantTemplate</c> is applied to a newly onboarded tenant (and the tenant is tagged with that
        /// type). Takes precedence over <see cref="BasicTenantTemplate"/>. When set but the type is missing or carries
        /// no template, no template is applied (a warning is logged).
        /// </summary>
        public string BasicTenantType { get; set; }

        /// <summary>
        /// Legacy: the <c>Name</c> of the <c>TenantTemplate</c> to apply directly. Used as a fallback only when
        /// <see cref="BasicTenantType"/> is not set. Prefer <see cref="BasicTenantType"/>.
        /// </summary>
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

        /// <summary>
        /// When <c>true</c> (recommended for a productive web), creating an <c>EmployeeRoleMapping</c> always
        /// creates a NEW, dedicated tenant role: the role-mappings admin UI hides the "wrap an existing role"
        /// picker and the handler rejects any attempt to link a pre-existing role. This prevents an admin from
        /// accidentally repurposing an existing (permission-bearing) role as a DirectRole. Defaults to
        /// <c>false</c> (existing roles may be wrapped into a mapping).
        /// </summary>
        public bool ForceDedicatedRoleForMappings { get; set; }
    }
}
