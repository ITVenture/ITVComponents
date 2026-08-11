using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets
{
    /// <summary>
    /// What a dashboard tile is drawn with. An implementation is an ordinary Blazor component that declares
    /// these four parameters; the dashboard picks it by the widget's renderer key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Vertrag beschreibt den Ist-Zustand, er erfindet ihn nicht: es ist genau der Parametersatz, den
    /// der eingebaute Scriban-Renderer schon immer hatte. Was im <see cref="TemplateSource"/> steht,
    /// entscheidet der Renderer - beim Scriban-Renderer ein Template, bei einem Diagramm-Renderer eine
    /// Deklaration.
    /// </para>
    /// <para>
    /// Der Konfigurationstext wird bei JEDEM Zeichnen neu gesetzt (die Kachel haengt an einer
    /// <c>DynamicComponent</c>, und die baut ihr Parameter-Woerterbuch jedes Mal neu auf). Eine
    /// Implementierung muss ihre teure Arbeit - Uebersetzen, Auswerten, Parsen - deshalb selbst puffern und
    /// nur bei geaenderter Quelle wiederholen.
    /// </para>
    /// </remarks>
    public interface IWidgetRenderer
    {
        /// <summary>The widget's configuration text - a template, a declaration, whatever the renderer reads.</summary>
        string TemplateSource { get; set; }

        /// <summary>The query result and the resolved title: Rows / Row / Count / Params / Title.</summary>
        WidgetTemplateModel? Data { get; set; }

        /// <summary>
        /// The renderer's settings, as declared by its descriptor (invariant strings, keyed by field name).
        /// Empty when the renderer declares no options.
        /// </summary>
        IReadOnlyDictionary<string, string?> Options { get; set; }

        /// <summary>Raised when the rendered content triggers an action the application should handle.</summary>
        EventCallback<WidgetAction> OnAction { get; set; }

        /// <summary>
        /// Raised when the configuration cannot be rendered. The dashboard logs it; the renderer decides
        /// what the tile shows in the meantime.
        /// </summary>
        EventCallback<Exception> OnRenderError { get; set; }
    }
}
