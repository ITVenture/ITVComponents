using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Models
{
    public class EmployeeRole
    {
        [Key]
        public int EmployeeGroupId { get; set; }

        public int EmployeeId { get; set; }

        public int RoleId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public virtual Employee Employee { get; set; }

        [ForeignKey(nameof(RoleId))]
        public virtual Role Role { get; set; }
    }
}
