using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel
{
    public class NavigationMenuViewModel
    {
        [Key]
        public int NavigationMenuId { get; set; }

        [Required]
        [DataType(DataType.MultilineText)]
        public string DisplayName { get; set; }

        [MaxLength(1024)]
        public string Url { get; set; }

        public int? ParentId { get; set; }

        public int? SortOrder { get; set; }

        public int? PermissionId { get; set; }

        public int? FeatureId { get; set; }

        [DataType(DataType.MultilineText)]
        public string SpanClass { get; set; }

        public bool IsPublic { get; set; } = false;

        public int[] Tenants { get; set; }
    }
}
