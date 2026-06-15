namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models
{
    /// <summary>
    /// Serialized representation of one <c>EmployeeRoleMapping</c> in a tenant template's extension payload.
    /// The underlying role is referenced by name (the stable, template-portable key); the PermissionSet
    /// composition itself is carried by the template's role-grant (RoleRole) section, so only the mapping's
    /// classification and label are captured here.
    /// </summary>
    public class EmployeeRoleMappingTemplateEntry
    {
        /// <summary>Part key under which the onboarding mapping payload is stored in the template Extensions bag.</summary>
        public const string PartKey = "OnboardingEmployeeRoleMappings";

        public string RoleName { get; set; }

        public EmployeeRoleMappingKind Kind { get; set; }

        public string DisplayNameJson { get; set; }
    }
}
