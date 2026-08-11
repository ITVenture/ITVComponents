using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    [JsonDerivedType(typeof(DashboardWidgetTemplateMarkup), "base")]
    public class DashboardWidgetTemplateMarkup
    {
        public string DisplayName { get; set; }

        public string TitleTemplate { get; set; }

        public string SystemName { get; set; }

        public string DiagnosticsQueryName { get; set; }

        public string Area { get; set; }

        public string CustomQueryString { get; set; }

        public string Template { get; set; }

        /// <summary>
        /// Womit die Kachel gezeichnet wird; leer = der eingebaute Scriban-Renderer.
        /// </summary>
        /// <remarks>
        /// Muss mit exportiert werden: ohne diese beiden Felder kaeme ein Diagramm-Widget als
        /// Scriban-Widget zurueck, und seine Konfiguration landete als roher Text in der Kachel.
        /// </remarks>
        public string RendererKey { get; set; }

        /// <summary>Die Einstellungen des Renderers (JSON, invariant).</summary>
        public string RendererOptions { get; set; }

        public DashboardParamTemplateMarkup[] Parameters { get; set; }
    }
}
