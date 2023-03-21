using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class DashboardWidgetViewModel
    {
        public int DashboardWidgetId { get; set; }

        [MaxLength(1024)]
        public string DisplayName { get; set; }

        [MaxLength(2048)]
        public string TitleTemplate { get; set; }

        [MaxLength(100)]
        public string SystemName { get; set; }

        public int DiagnosticsQueryId { get; set; }

        public string Area { get; set; }

        public string CustomQueryString { get; set; }

        [DataType(DataType.MultilineText)]
        public string Template { get; set; }
    }
}