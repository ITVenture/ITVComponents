using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets;
using Microsoft.AspNetCore.Components;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Ein Renderer, der den Vertrag erfuellt, aber kein <see cref="WidgetRendererAttribute"/> traegt.
    /// </summary>
    /// <remarks>
    /// Er existiert nur fuer den Fall „kein Schluessel zu ermitteln" - das ist ein Registrierungsfehler und
    /// soll beim Start auffallen, nicht als leere Auswahl im Editor.
    /// </remarks>
    public class NoAttributeRenderer : ComponentBase, IWidgetRenderer
    {
        [Parameter] public string TemplateSource { get; set; } = string.Empty;

        [Parameter] public WidgetTemplateModel? Data { get; set; }

        [Parameter] public IReadOnlyDictionary<string, string?> Options { get; set; }
            = new Dictionary<string, string?>();

        [Parameter] public EventCallback<WidgetAction> OnAction { get; set; }

        [Parameter] public EventCallback<Exception> OnRenderError { get; set; }
    }
}
