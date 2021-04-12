using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel
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

        public string SpanClass { get; set; }

        public int[] Tenants { get; set; }
    }
}
