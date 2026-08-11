using System;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets
{
    /// <summary>
    /// Marks a component as a widget renderer and carries the key it is registered under.
    /// </summary>
    /// <remarks>
    /// Der Schluessel steht damit genau EINMAL - am Typ. Ohne das Attribut muesste er dreimal geschrieben
    /// werden (Registrierung, Konfiguration, Datenbankspalte) und dreimal gleich; der Registrierungsaufruf
    /// und der Konfigurationsweg nennen jetzt nur noch den Typ. Uebrig bleibt die eine unvermeidbare
    /// Stelle: der Wert in der Spalte, den der Editor aber als Auswahl anbietet.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class WidgetRendererAttribute : Attribute
    {
        /// <param name="key">
        /// der Schluessel, unter dem Widgets diesen Renderer anfordern. Leer ist zulaessig und bedeutet
        /// "der Renderer fuer Widgets ohne Angabe" - das ist der eingebaute Scriban-Renderer.
        /// </param>
        public WidgetRendererAttribute(string key)
        {
            Key = key ?? string.Empty;
        }

        /// <summary>The key widgets refer to.</summary>
        public string Key { get; }

        /// <summary>
        /// Label shown in the widget editor. May be a per-culture record; plain text works as well.
        /// Falls back to <see cref="Key"/> when not set.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Monaco language for the configuration editor (<c>html</c>, <c>json</c>, <c>csharp</c>, …).
        /// Defaults to <c>html</c>, which is what the Scriban templates have always used.
        /// </summary>
        public string? EditorLanguage { get; set; }
    }
}
