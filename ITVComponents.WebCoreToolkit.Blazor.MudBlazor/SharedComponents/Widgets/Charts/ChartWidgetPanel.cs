using System;
using System.Collections.Generic;
using System.Linq;
using MudBlazor;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets.Charts
{
    /// <summary>
    /// One chart of a tile: its declaration, its bound parameters and what was wrong with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Eine Konfiguration darf ein Diagramm beschreiben oder mehrere. Der Unterschied endet hier: beide
    /// Renderer bauen aus ihren Eintraegen dieselbe Liste, und die Ansicht zeichnet sie.
    /// </para>
    /// <para>
    /// Die Fehler haengen am EINZELNEN Diagramm und nicht an der Kachel. Das ist der Punkt, um den es
    /// geht: ein Tippfehler im dritten Diagramm soll die beiden ersten nicht mit ausloeschen - sonst
    /// nimmt eine kleine Unachtsamkeit die ganze Kachel mit, und was noch richtig ist, sieht man nicht
    /// mehr. Was die ganze Kachel betrifft (unlesbares JSON, ein Skript, das wirft), bleibt Sache des
    /// Renderers und steht weiterhin oben.
    /// </para>
    /// </remarks>
    public sealed class ChartWidgetPanel
    {
        /// <summary>What to draw. Null means: this entry could not be read - see <see cref="Errors"/>.</summary>
        public ChartWidgetDeclaration? Declaration { get; init; }

        /// <summary>The pass-through parameters, already checked and converted.</summary>
        public Dictionary<string, object?> Parameters { get; init; } = new();

        /// <summary>Everything that was wrong with THIS entry. Empty = nothing.</summary>
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

        /// <summary>Die Beschriftungen als Zeichenketten - was die Diagramm-Komponente entgegennimmt.</summary>
        public string[] LabelTexts { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Turns the entries of a configuration into one panel each.
        /// </summary>
        /// <param name="entries">the entries - a map per chart, whatever the renderer's language</param>
        /// <returns>one panel per entry, in the order they were declared</returns>
        /// <remarks>
        /// Kein Eintrag faellt weg: was sich nicht lesen laesst, wird zu einem Platz mit Meldung. Ein
        /// stillschweigend uebersprungener Eintrag waere sonst genau die Sorte Fehler, bei der man die
        /// Konfiguration fuer richtig haelt und das fehlende Diagramm fuer ein Darstellungsproblem.
        /// </remarks>
        public static IReadOnlyList<ChartWidgetPanel> Build(IEnumerable<object?> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            var panels = new List<ChartWidgetPanel>();
            foreach (object? entry in entries)
            {
                panels.Add(Build(entry, panels.Count));
            }

            if (panels.Count == 0)
            {
                panels.Add(new ChartWidgetPanel
                {
                    Errors = new[] { "The configuration produced no chart declaration." }
                });
            }

            return panels;
        }

        private static ChartWidgetPanel Build(object? entry, int index)
        {
            var errors = new List<string>();

            // Ein Fall genuegt - siehe die Anmerkung an ChartWidgetDeclaration.AsMap: das ObjectLiteral aus
            // CScript IST ein IDictionary<string, object>.
            if (entry is not IDictionary<string, object?> map)
            {
                string what = entry == null ? "nothing" : entry.GetType().Name;
                return new ChartWidgetPanel
                {
                    Errors = new[] { $"Chart {index + 1} of the configuration is a {what}, not a declaration." }
                };
            }

            ChartWidgetDeclaration? declaration = ChartWidgetDeclaration.FromMap(map, errors);
            if (declaration == null)
            {
                return new ChartWidgetPanel { Errors = errors };
            }

            Dictionary<string, object?> parameters =
                ChartParameterBinder.Bind(declaration.Extra, typeof(MudChart<double>), errors);

            return new ChartWidgetPanel
            {
                Declaration = declaration,
                Parameters = parameters,
                Errors = errors,
                LabelTexts = declaration.Labels.Select(l => l.Text).ToArray()
            };
        }
    }
}
