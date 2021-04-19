using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class DiagnosticsQueryViewModel
    {
        [Key]
        public int DiagnosticsQueryId { get; set; }

        [Required, MaxLength(128)]
        public string DiagnosticsQueryName { get; set; }

        [Required, MaxLength(128)]
        public string DbContext { get; set; }

        public bool AutoReturn { get; set; }

        [DataType(DataType.MultilineText)]
        public string QueryText { get;set; }

        public int PermissionId{get;set;}

        public int[] Tenants { get; set; }
    }
}
