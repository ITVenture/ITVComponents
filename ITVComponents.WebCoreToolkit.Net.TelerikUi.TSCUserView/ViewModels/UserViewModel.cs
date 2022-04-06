using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityContextUserView.ViewModels
{
    public class UserViewModel
    {
        [Key]
        public int UserId { get;set; }

        [MaxLength(150)]
        [Required]
        public string UserName { get; set; }

        public int? AuthenticationTypeId { get; set; }

        public bool Assigned { get; set; }
        public int? RoleId { get; set; }
        
        public string UniQUID{get; set; }
        
        public int? TenantId{get; set; }
        public bool Enabled { get; set; }
    }
}
