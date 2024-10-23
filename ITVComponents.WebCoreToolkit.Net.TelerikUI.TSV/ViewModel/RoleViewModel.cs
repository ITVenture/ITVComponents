using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class RoleViewModel
    {
        [Key]
        public int RoleId { get; set; }

        [MaxLength(150)]
        [Required]
        public string RoleName { get; set; }

        public bool Assigned { get; set; }
        public int TenantId { get; set; }
        public int? PermissionId { get; set; }
        public int? UserId { get; set; }

        public int? PermissiveRoleId { get; set; }

        public string UniQUID{get; set; }
        public bool Editable { get; set; }
        
        public bool IsSystemRole { get; set; }
    }
}
