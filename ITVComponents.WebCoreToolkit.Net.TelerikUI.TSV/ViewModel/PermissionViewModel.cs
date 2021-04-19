using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class PermissionViewModel
    {
        [Key]
        public int PermissionId { get; set; }

        [MaxLength(150)]
        [Required]
        public string PermissionName { get; set; }

        [MaxLength(2048)]
        public string Description { get; set; }

        public bool Assigned { get; set; }
        public int? TenantId { get; set; }
        public int? RoleId { get; set; }

        public string UniQUID{get; set; }
        public bool IsGlobal { get; set; }
        public bool	 Editable { get; set; }
    }
}
