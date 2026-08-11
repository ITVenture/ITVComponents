using System;
using System.Collections.Generic;
using System.Text;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazorLib.Config
{
    public class WebPartConfig
    {
        public bool UseViews { get; set; }

        /// <summary>
        /// Widget renderers contributed by the host. The built-in ones register themselves; this is the way
        /// in for a consumer who would rather not write a registration call.
        /// </summary>
        /// <remarks>
        /// Der Weg ueber die Konfiguration kann - anders als der Code-Weg - nicht beim Uebersetzen pruefen,
        /// ob der Typ eine Komponente ist und den Vertrag erfuellt. Genau deshalb wird jeder Eintrag beim
        /// START geprueft und mit Schluessel und Typname abgelehnt, statt erst beim Zeichnen einer Kachel
        /// aufzufallen.
        /// </remarks>
        public WidgetRendererConfig[]? WidgetRenderers { get; set; }
    }

    /// <summary>
    /// One configured widget renderer.
    /// </summary>
    public class WidgetRendererConfig
    {
        /// <summary>
        /// The type of the component, in the notation used everywhere else in the part configuration
        /// (<c>Namespace.Type, Assembly</c>).
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Overrides the key from the type's <c>WidgetRendererAttribute</c>. Normalerweise leer lassen -
        /// der Schluessel gehoert an den Typ, dort kann er nicht auseinanderlaufen.
        /// </summary>
        public string? Key { get; set; }

        /// <summary>Overrides the label from the attribute. May be a per-culture record.</summary>
        public string? DisplayName { get; set; }

        /// <summary>Overrides the Monaco language from the attribute.</summary>
        public string? EditorLanguage { get; set; }
    }
}
