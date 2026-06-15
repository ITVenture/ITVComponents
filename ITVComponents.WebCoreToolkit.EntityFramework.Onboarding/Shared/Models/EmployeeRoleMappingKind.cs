namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models
{
    /// <summary>
    /// Classifies an <c>EmployeeRoleMapping</c>. Both kinds reference a security <c>Role</c>; the kind only
    /// decides how the mapping is used.
    /// </summary>
    public enum EmployeeRoleMappingKind
    {
        /// <summary>
        /// A concrete, assignable role. <c>EmployeeRole</c> rows point at DirectRole mappings; assigning one to
        /// an employee grants the underlying role to the employee's user. PermissionSets activated on it
        /// (via role inheritance) add their permissions.
        /// </summary>
        DirectRole = 0,

        /// <summary>
        /// A reusable bundle of permissions (its role carries the fine-grained permissions). Not assigned to
        /// employees directly; it is activated on a <see cref="DirectRole"/> mapping, which technically grants
        /// the DirectRole's role inheritance over this set's role.
        /// </summary>
        PermissionSet = 1
    }
}
