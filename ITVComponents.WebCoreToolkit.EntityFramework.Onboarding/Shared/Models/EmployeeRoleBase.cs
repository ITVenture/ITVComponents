using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models
{
    /// <summary>
    /// Generic join entity between an Employee and an <c>EmployeeRoleMapping</c> (of kind DirectRole) within an
    /// onboarded tenant. Assigning one grants the mapping's underlying role to the employee's user (materialized
    /// by the onboarding save-changes interceptor / at invitation acceptance).
    /// </summary>
    public abstract class EmployeeRoleBase<TEmployeeRoleMapping, TEmployee, TEmployeeRole>
        where TEmployeeRoleMapping : class
        where TEmployee : class
        where TEmployeeRole : class
    {
        [Key]
        public int EmployeeGroupId { get; set; }

        public int EmployeeId { get; set; }

        public int EmployeeRoleMappingId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public virtual TEmployee Employee { get; set; }

        [ForeignKey(nameof(EmployeeRoleMappingId))]
        public virtual TEmployeeRoleMapping RoleMapping { get; set; }
    }
}
