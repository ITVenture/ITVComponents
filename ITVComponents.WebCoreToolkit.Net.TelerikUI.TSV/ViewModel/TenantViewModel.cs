using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class TenantViewModelC<TUserId> where TUserId:class
    {
        [Key]
        public int TenantId { get; set; }

        [Required,MaxLength(150)]
        public string TenantName { get; set; }

        [MaxLength(1024)]
        public string DisplayName { get; set; }
        
        public TUserId UserId { get; set; }

        public string UniQUID{get; set; }
        
        public bool Assigned { get;set; }
    }

    public class TenantViewModelS<TUserId> where TUserId : struct
    {
        [Key]
        public int TenantId { get; set; }

        [Required, MaxLength(150)]
        public string TenantName { get; set; }

        [MaxLength(1024)]
        public string DisplayName { get; set; }

        public TUserId? UserId { get; set; }

        public string UniQUID { get; set; }

        public bool Assigned { get; set; }
    }

    public class TenantViewModel
    {
        [Key]
        public int TenantId { get; set; }

        [Required, MaxLength(150)]
        public string TenantName { get; set; }

        [MaxLength(1024)]
        public string DisplayName { get; set; }

        public dynamic UserId { get; set; }

        public string UniQUID { get; set; }

        public bool Assigned { get; set; }
    }
}
