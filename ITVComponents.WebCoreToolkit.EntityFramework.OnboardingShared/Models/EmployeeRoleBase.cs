using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITVComponents.WebCoreToolkit.EntityFramework.OnboardingShared.Models
{
    /// <summary>
    /// Generic join entity between an Employee and a Role within an onboarded tenant.
    /// </summary>
    public abstract class EmployeeRoleBase<TRole, TEmployee, TEmployeeRole>
        where TRole : class
        where TEmployee : class
        where TEmployeeRole : class
    {
        [Key]
        public int EmployeeGroupId { get; set; }

        public int EmployeeId { get; set; }

        public int RoleId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public virtual TEmployee Employee { get; set; }

        [ForeignKey(nameof(RoleId))]
        public virtual TRole Role { get; set; }
    }
}
