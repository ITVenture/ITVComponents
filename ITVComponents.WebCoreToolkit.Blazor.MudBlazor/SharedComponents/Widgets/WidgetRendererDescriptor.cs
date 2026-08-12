using System;
using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets
{
    /// <summary>
    /// Everything the dashboard and the widget editor need to know about a registered renderer.
    /// </summary>
    /// <remarks>
    /// Die Angaben haengen hier und nicht an <see cref="IWidgetRenderer"/>: der Editor muesste sonst eine
    /// Instanz der Komponente bauen, nur um ihren Anzeigenamen zu erfahren.
    /// </remarks>
    public sealed class WidgetRendererDescriptor
    {
        /// <summary>The key widgets refer to. Empty = the renderer for widgets without a key.</summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>The component that draws the tile.</summary>
        public Type ComponentType { get; init; } = default!;

        /// <summary>
        /// Label for the editor - raw, may be a per-culture record. Translated where it is shown, not here:
        /// die Sprache des Lesers steht beim Registrieren noch nicht fest.
        /// </summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Monaco language for the configuration editor.</summary>
        public string EditorLanguage { get; init; } = "html";

        /// <summary>
        /// The renderer's settings. Declared with the same <see cref="DeclaredField"/> form the widget
        /// parameters use, so the editor can show the generic mask for them.
        /// </summary>
        public IReadOnlyList<DeclaredField> Options { get; init; } = Array.Empty<DeclaredField>();

        /// <summary>
        /// Checks a configuration text before it is saved. Returns null when it is fine, otherwise the
        /// message the editor shows. Null = the renderer offers no check.
        /// </summary>
        /// <remarks>
        /// Als Delegat an der Registrierung und nicht am Attribut: ein Attribut kann keinen Delegaten
        /// tragen. Eine kaputte Konfiguration soll beim SPEICHERN auffallen und nicht erst als
        /// Fehler-Kachel bei dem, der das Dashboard oeffnet.
        /// </remarks>
        public Func<string?, IReadOnlyDictionary<string, string?>, string?>? Validate { get; init; }

        /// <summary>
        /// Returns a comment block listing what the configuration may contain beyond the obvious - shown by
        /// the editor's "insert parameters" button. Null = the renderer offers nothing to insert.
        /// </summary>
        /// <remarks>
        /// Gedacht fuer Angaben, die nur der CODE kennt (etwa die Parameter der Diagramm-Komponente samt
        /// Typ). Beispiele und Erklaerungen gehoeren dagegen ins Hilfesystem: die kann eine generierte
        /// Liste nicht liefern, und sie veralten nicht mit der naechsten Fassung einer Fremdbibliothek.
        /// </remarks>
        public Func<string>? DescribeParameters { get; init; }
    }
}
