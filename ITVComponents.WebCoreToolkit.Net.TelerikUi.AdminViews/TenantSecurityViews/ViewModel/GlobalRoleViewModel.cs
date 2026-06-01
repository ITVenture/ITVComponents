using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel
{
    public class GlobalRoleViewModel
    {
        [Key]
        public int GlobalRoleId { get; set; }

        [MaxLength(150)]
        [Required]
        public string RoleName { get; set; }

        [DataType(DataType.MultilineText)]
        public string? RoleMetaData { get; set; }
        public int? RoleId { get; set; }
        public bool Assigned { get; set; }
        public string UniQUID { get; set; }
        
        [MaxLength(512)]
        public string? RoleDescription { get; set; }
    }
}
