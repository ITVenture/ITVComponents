using System.Text.Json;
using System.Text.Json.Nodes;

namespace ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions
{
    /// <summary>
    /// Die Art eines Einstellungsfeldes — sie bestimmt, welches Eingabe-Element die Maske zeichnet.
    /// </summary>
    /// <remarks>
    /// <b>Dieselbe Reihenfolge wie <c>DeclaredFieldKind</c> im Blazor-Toolkit</b>, und das ist keine
    /// Kosmetik: die Maske dort rendert gegen jene Aufzählung, die Werte werden numerisch abgebildet,
    /// und eine abweichende Reihenfolge machte aus einer Auswahlliste stillschweigend ein Datumsfeld.
    /// Neue Arten gehören ans Ende — hier wie dort.
    /// </remarks>
    public enum TerminalSettingKind
    {
        /// <summary>Einzeiliger Text (Vorgabe).</summary>
        Text,

        /// <summary>Mehrzeiliger Text.</summary>
        MultilineText,

        /// <summary>Zahl.</summary>
        Number,

        /// <summary>Ja/Nein.</summary>
        Boolean,

        /// <summary>Datum.</summary>
        Date,

        /// <summary>Auswahl aus <see cref="TerminalSettingDescriptor.Choices"/>.</summary>
        Choice,

        /// <summary>Datum und Uhrzeit.</summary>
        DateTime
    }

    /// <summary>
    /// Ein Einstellungsfeld, das eine Terminal-Anbindung braucht — damit eine Maske dafür entstehen
    /// kann, ohne dass jemand sie je Anbindung von Hand baut.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Eigenes Modell und keine Übernahme von <c>DeclaredField</c>: das liegt im Blazor-Toolkit, und
    /// dieses Paket wird vom Kassen-Agenten geladen, der weder Blazor noch ASP.NET kennt. Die Kante,
    /// an der projiziert wird, liegt in der Oberfläche — dort besteht die Abhängigkeit ohnehin, und es
    /// sind zwanzig Zeilen. Die Alternative wäre, dem Agenten die halbe Web-Schicht aufzuzwingen.
    /// </para>
    /// <para>
    /// Deshalb auch kein gemeinsames Interface: das zöge die Abhängigkeit in die falsche Richtung.
    /// Genau diese Entscheidung ist bei <c>DeclaredField</c> bereits so getroffen und begründet
    /// worden; hier wird sie nur befolgt.
    /// </para>
    /// </remarks>
    public class TerminalSettingDescriptor
    {
        /// <summary>Der Name des Feldes — der Schlüssel, unter dem der Wert im JSON landet.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Die Beschriftung. Leer heisst: der Name wird angezeigt.</summary>
        public string? Label { get; set; }

        /// <summary>Die Art des Feldes.</summary>
        public TerminalSettingKind Kind { get; set; } = TerminalSettingKind.Text;

        /// <summary>Ob ohne diesen Wert nicht gespeichert werden kann.</summary>
        public bool Required { get; set; }

        /// <summary>Ein Hinweis unter dem Feld — hier gehört hin, was sonst niemand weiss.</summary>
        public string? HelpText { get; set; }

        /// <summary>
        /// Die Vorbelegung, als Text. Leer heisst: kein Vorschlag.
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>Die Auswahlmöglichkeiten — nur bei <see cref="TerminalSettingKind.Choice"/>.</summary>
        public IReadOnlyList<TerminalSettingChoice>? Choices { get; set; }
    }

    /// <summary>Eine Auswahlmöglichkeit.</summary>
    public class TerminalSettingChoice
    {
        /// <summary>Der Wert, der gespeichert wird.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>Die Beschriftung; leer = <see cref="Value"/>.</summary>
        public string? Label { get; set; }
    }

    /// <summary>
    /// Macht aus dem, was eine Maske eingesammelt hat, das JSON des Geräts.
    /// </summary>
    /// <remarks>
    /// <b>Der Grund, warum es diesen Helfer gibt und nicht jeder selbst serialisiert:</b> eine Maske
    /// liefert Text, auch für Zahlen und Schalter. Wer <c>{"port":"50000"}</c> speichert, bekommt beim
    /// Lesen einen Fehler, den niemand mit dem Ausfüllen der Maske in Verbindung bringt — und zwar
    /// erst beim ersten Kassiervorgang. Hier werden die Werte nach ihrer deklarierten Art
    /// geschrieben.
    /// </remarks>
    public static class TerminalSettings
    {
        /// <summary>
        /// Baut das Konfigurations-JSON aus einem oder mehreren Abschnitten.
        /// </summary>
        /// <param name="sections">
        /// die Abschnitte des Assistenten, in ihrer Reihenfolge — erst die Angaben der Web-Seite, dann
        /// die des Geräts. Spätere überschreiben gleichnamige frühere.
        /// </param>
        /// <returns>das JSON, das an <see cref="TerminalTarget.ConfigurationJson"/> gehört</returns>
        public static string Compose(params (IReadOnlyList<TerminalSettingDescriptor> Fields,
            IReadOnlyDictionary<string, string?> Values)[] sections)
        {
            var result = new JsonObject();
            foreach (var (fields, values) in sections)
            {
                foreach (var field in fields)
                {
                    if (!values.TryGetValue(field.Name, out var raw))
                    {
                        continue;
                    }

                    raw ??= field.DefaultValue;
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        // Ein leeres Feld gar nicht erst schreiben: null im JSON und "Feld fehlt" sind
                        // beim Lesen dasselbe, aber ein leerer Text ist es NICHT - der ueberschreibt
                        // eine Vorbelegung mit nichts.
                        continue;
                    }

                    result[field.Name] = Convert(field, raw.Trim());
                }
            }

            return result.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }

        /// <summary>Schreibt einen Wert in der Form, in der er gelesen wird.</summary>
        private static JsonNode? Convert(TerminalSettingDescriptor field, string raw)
            => field.Kind switch
            {
                TerminalSettingKind.Number => long.TryParse(raw, out var whole)
                    ? JsonValue.Create(whole)
                    : double.TryParse(raw, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var fraction)
                        ? JsonValue.Create(fraction)
                        // Keine Zahl, obwohl als Zahl deklariert: als Text stehen lassen und den Fehler
                        // dem Leser ueberlassen. Hier still eine 0 einzusetzen waere schlimmer - eine 0
                        // ist bei einem Port oder einem Betrag eine Aussage.
                        : JsonValue.Create(raw),
                TerminalSettingKind.Boolean => JsonValue.Create(
                    bool.TryParse(raw, out var flag)
                        ? flag
                        : raw is "1" or "yes" or "ja" or "on"),
                _ => JsonValue.Create(raw)
            };
    }
}
