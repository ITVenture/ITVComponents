using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models
{
    /// <summary>
    /// Generic base for a per-tenant <c>EmployeeRoleMapping</c> — a friendly-labelled wrapper around a
    /// security <typeparamref name="TRole"/>. A mapping is either a <see cref="EmployeeRoleMappingKind.DirectRole"/>
    /// (assignable to employees) or a <see cref="EmployeeRoleMappingKind.PermissionSet"/> (a bundle activated on
    /// a DirectRole via role inheritance). The mapping lets a tenant admin compose and assign roles by friendly
    /// name without knowing the underlying permission detail.
    /// </summary>
    public abstract class EmployeeRoleMappingBase<TTenant, TRole, TEmployeeRole, TEmployeeRoleMapping>
        where TTenant : Tenant
        where TRole : class
        where TEmployeeRole : class
        where TEmployeeRoleMapping : class
    {
        [Key]
        public int EmployeeRoleMappingId { get; set; }

        public int TenantId { get; set; }

        public int RoleId { get; set; }

        public EmployeeRoleMappingKind Kind { get; set; }

        /// <summary>
        /// Optional multilingual display name as JSON (same convention as navigation labels). When null the
        /// underlying role name is used.
        /// </summary>
        public string DisplayNameJson { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }

        [ForeignKey(nameof(RoleId))]
        public virtual TRole Role { get; set; }

        /// <summary>Employee assignments referencing this mapping (only meaningful for DirectRole mappings).</summary>
        public virtual ICollection<TEmployeeRole> EmployeeRoles { get; set; } = new List<TEmployeeRole>();
    }
}
