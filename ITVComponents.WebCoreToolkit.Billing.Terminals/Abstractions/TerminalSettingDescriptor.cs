using System.Text.Json;
using System.Text.Json.Nodes;
using ITVComponents.AgentServices.Abstractions;

namespace ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions
{
    /// <summary>
    /// Wohin der Wert eines Feldes gespeichert wird.
    /// </summary>
    public enum TerminalSettingTarget
    {
        /// <summary>Ins Konfigurations-JSON des Geräts (Vorgabe).</summary>
        Configuration,

        /// <summary>
        /// In die Route-Spalte — den Dienst, über den ein Agent erreicht wird.
        /// </summary>
        /// <remarks>
        /// Eine eigene Spalte, damit sich die Frage „welche Geräte hängen an dieser Kasse?" beantworten
        /// lässt, ohne JSON zu durchsuchen. Genau die stellt sich, wenn ein Kassen-PC ersetzt wird.
        /// </remarks>
        Route
    }

    /// <summary>
    /// Die Namen der Auswahllisten, die eine Anwendung füllen kann.
    /// </summary>
    /// <remarks>
    /// Zeichenketten und keine Aufzählung: welche Quellen es gibt, weiss die Oberfläche, nicht dieses
    /// Paket. Wer eine eigene hinzufügt, braucht hier nichts zu ändern.
    /// </remarks>
    public static class TerminalChoiceSources
    {
        /// <summary>Die Client-Anwendungen des Mandanten — Wert ist ihr <c>ClientKey</c>.</summary>
        public const string ClientApps = "clientApps";

        /// <summary>
        /// Die Objekte auf dem gewählten Dienst, die <see cref="ITerminalDevice"/> erfüllen.
        /// </summary>
        /// <remarks>
        /// Braucht ein <see cref="SettingDescriptor.DependsOn"/> auf das Feld, das den Dienst
        /// nennt — ohne den weiss die Maske nicht, wen sie fragen soll.
        /// </remarks>
        public const string RemoteObjects = "remoteObjects";
    }

    /// <summary>
    /// Ein Einstellungsfeld, das eine Terminal-Anbindung braucht — damit eine Maske dafür entstehen
    /// kann, ohne dass jemand sie je Anbindung von Hand baut.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Das gemeinsame Feldmodell (<see cref="SettingDescriptor"/>) plus das, was nur Terminals haben:
    /// wohin ein Wert gespeichert wird. Agent-Geräte beschreiben sich mit demselben Modell, darum kann
    /// eine Maske beide zeichnen.
    /// </para>
    /// <para>
    /// Kein <c>DeclaredField</c>: das liegt im Blazor-Toolkit, und dieses Paket wird vom Kassen-Agenten
    /// geladen, der weder Blazor noch ASP.NET kennt. Projiziert wird in der Oberfläche.
    /// </para>
    /// </remarks>
    public class TerminalSettingDescriptor : SettingDescriptor
    {
        /// <summary>Wohin der Wert gespeichert wird. Vorgabe: ins Konfigurations-JSON.</summary>
        public TerminalSettingTarget StoredIn { get; set; } = TerminalSettingTarget.Configuration;
    }

    /// <summary>
    /// Macht aus dem, was eine Maske eingesammelt hat, ein Einstellungs-JSON.
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
        /// Baut das JSON aus einem oder mehreren Abschnitten.
        /// </summary>
        /// <param name="sections">
        /// die Abschnitte des Assistenten, in ihrer Reihenfolge — erst die Angaben der Web-Seite, dann
        /// die des Geräts. Spätere überschreiben gleichnamige frühere.
        /// </param>
        /// <returns>
        /// das JSON — für Cloud-Terminals das, was an <see cref="TerminalTarget.ConfigurationJson"/> gehört,
        /// für Agent-Geräte das, was an den Agenten geht
        /// </returns>
        public static string Compose(params (IReadOnlyList<SettingDescriptor> Fields,
            IReadOnlyDictionary<string, string?> Values)[] sections)
        {
            var result = new JsonObject();
            foreach (var (fields, values) in sections)
            {
                foreach (var field in fields)
                {
                    if (field is TerminalSettingDescriptor { StoredIn: not TerminalSettingTarget.Configuration })
                    {
                        // Gehoert in eine eigene Spalte, nicht ins JSON. Beides zu schreiben hiesse
                        // zwei Staende desselben Werts, und der eine wird irgendwann nicht mitgeaendert.
                        continue;
                    }

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

        /// <summary>
        /// Holt den Wert heraus, der nicht ins JSON gehört, sondern in eine eigene Spalte.
        /// </summary>
        /// <returns>der Wert des ersten Feldes mit diesem Ziel, oder null</returns>
        public static string? ValueFor(IReadOnlyList<TerminalSettingDescriptor> fields,
            IReadOnlyDictionary<string, string?> values, TerminalSettingTarget target)
        {
            foreach (var field in fields)
            {
                if (field.StoredIn == target && values.TryGetValue(field.Name, out var raw)
                                             && !string.IsNullOrWhiteSpace(raw))
                {
                    return raw.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Die Pflichtfelder, die in einer gespeicherten Geräte-Zeile fehlen.
        /// </summary>
        /// <remarks>
        /// Die Gegenprobe zu <see cref="Compose"/> und <see cref="ValueFor"/>: was die Maske verlangt,
        /// muss auch angekommen sein. Sonst liesse sich ein Gerät speichern, das nie erreichbar ist —
        /// genau so geschehen, als der Assistent einen Schritt übersprungen hat (BUG-PRE256).
        /// </remarks>
        /// <param name="fields">die Felder des Wegs, wie <c>DescribeSettings</c> sie liefert</param>
        /// <param name="route">der Wert der Route-Spalte</param>
        /// <param name="configuration">die Eigenschaften des Konfigurations-JSON, nach Namen</param>
        /// <returns>die Namen der fehlenden Felder; leer, wenn alles da ist</returns>
        public static IReadOnlyList<string> MissingRequired(IReadOnlyList<TerminalSettingDescriptor> fields,
            string? route, IReadOnlyDictionary<string, string?> configuration)
        {
            var missing = new List<string>();
            foreach (var field in fields.Where(f => f.Required))
            {
                var present = field.StoredIn switch
                {
                    TerminalSettingTarget.Route => !string.IsNullOrWhiteSpace(route),
                    _ => configuration.TryGetValue(field.Name, out var value) && !string.IsNullOrWhiteSpace(value)
                };

                if (!present)
                {
                    missing.Add(field.Name);
                }
            }

            return missing;
        }

        /// <summary>Schreibt einen Wert in der Form, in der er gelesen wird.</summary>
        private static JsonNode? Convert(SettingDescriptor field, string raw)
            => field.Kind switch
            {
                SettingKind.Number => long.TryParse(raw, out var whole)
                    ? JsonValue.Create(whole)
                    : double.TryParse(raw, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var fraction)
                        ? JsonValue.Create(fraction)
                        // Keine Zahl, obwohl als Zahl deklariert: als Text stehen lassen und den Fehler
                        // dem Leser ueberlassen. Hier still eine 0 einzusetzen waere schlimmer - eine 0
                        // ist bei einem Port oder einem Betrag eine Aussage.
                        : JsonValue.Create(raw),
                SettingKind.Boolean => JsonValue.Create(
                    bool.TryParse(raw, out var flag)
                        ? flag
                        : raw is "1" or "yes" or "ja" or "on"),
                _ => JsonValue.Create(raw)
            };
    }
}
