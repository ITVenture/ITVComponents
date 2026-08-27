using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Wie streng eine Vorlage die Bestaetigung ihrer Argumente verlangt.
    /// </summary>
    public enum AssetArgumentEnforcement
    {
        /// <summary>
        /// Gar nicht - es gelten allein die Pfadmuster. Das Verhalten aller Vorlagen, die keine Argumente
        /// fuehren, und damit das des gesamten Altbestands.
        /// </summary>
        None = 0,

        /// <summary>
        /// Ohne Bestaetigung wird die Antwort nicht ausgeliefert. Die Vorgabe fuer jede Vorlage mit
        /// Argumenten: der vergessene Check wird damit zur sichtbar leeren Seite statt zum stillen Loch.
        /// </summary>
        Confirmed = 1,

        /// <summary>
        /// Zusaetzlich muss jede weitere Bestaetigung im selben Kontext dieselben Werte liefern.
        /// </summary>
        Strict = 2
    }

    /// <summary>
    /// Die Argumentwerte einer Freigabe - das, worauf sie zeigt.
    /// <para>
    /// Bewusst als geschlossene, typbewusste Menge und nicht als lose Zeichenketten: der Vergleich muss
    /// <c>"04711"</c> und <c>4711</c> gleich beantworten, sonst kommt das in einem halben Jahr als "der
    /// Link geht manchmal nicht" zurueck.
    /// </para>
    /// </summary>
    public sealed class AssetArgumentValues
    {
        private readonly Dictionary<string, string> values;

        private AssetArgumentValues(Dictionary<string, string> values)
        {
            this.values = values;
        }

        /// <summary>Eine leere Wertemenge - fuer Vorlagen ohne Argumente.</summary>
        public static AssetArgumentValues Empty { get; } = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        /// <summary>Gibt an, ob ueberhaupt Werte vorliegen.</summary>
        public bool IsEmpty => values.Count == 0;

        /// <summary>Die Namen der belegten Argumente.</summary>
        public IReadOnlyCollection<string> Names => values.Keys;

        /// <summary>
        /// Liefert den kanonischen Wert eines Arguments, oder null.
        /// </summary>
        /// <param name="name">der Name des Arguments</param>
        /// <returns>der kanonische Wert oder null</returns>
        public string this[string name] => name != null && values.TryGetValue(name, out var found) ? found : null;

        /// <summary>
        /// Prueft die uebergebenen Werte gegen die Argumente einer Vorlage und bringt sie auf ihre
        /// kanonische Form.
        /// <para>
        /// Das ist die <b>harte</b> Pruefung: sie braucht nur die Vorlage und ist deshalb immer
        /// verlaesslich - anders als die Pruefung gegen die Konsumenten-Registry, die nach einem Neustart
        /// unvollstaendig sein kann und nur warnen darf.
        /// </para>
        /// </summary>
        /// <param name="declarations">die Argumente der Vorlage</param>
        /// <param name="input">die uebergebenen Werte</param>
        /// <param name="result">die kanonische Wertemenge</param>
        /// <param name="error">eine Meldung, die benennt, was fehlt oder nicht passt</param>
        /// <returns>true, wenn die Werte vollstaendig und typkonform sind</returns>
        public static bool TryCreate(IReadOnlyList<AssetArgumentDeclaration> declarations,
            IDictionary<string, string> input, out AssetArgumentValues result, out string error)
        {
            result = null;
            error = null;
            declarations ??= Array.Empty<AssetArgumentDeclaration>();
            input ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var declaration in declarations)
            {
                var raw = input.FirstOrDefault(n =>
                    string.Equals(n.Key, declaration.Name, StringComparison.OrdinalIgnoreCase)).Value;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    if (declaration.Required)
                    {
                        error = $"The argument '{declaration.Name}' is required by this template but was not supplied.";
                        return false;
                    }

                    continue;
                }

                if (!TryCanonicalize(raw, declaration.Type, out var normalized))
                {
                    error = $"The value of '{declaration.Name}' is not a valid {declaration.Type}.";
                    return false;
                }

                canonical[declaration.Name] = normalized;
            }

            // Ein Wert, den die Vorlage nicht kennt, wuerde spaeter von niemandem geprueft - er waere also
            // eine stille Luecke und keine Zusatzangabe.
            var unknown = input.Keys.FirstOrDefault(n => !declarations.Any(d =>
                string.Equals(d.Name, n, StringComparison.OrdinalIgnoreCase)));
            if (unknown != null)
            {
                error = $"The argument '{unknown}' is not declared by this template.";
                return false;
            }

            result = new AssetArgumentValues(canonical);
            return true;
        }

        /// <summary>
        /// Vergleicht einen gemeldeten Wert mit dem hinterlegten. Beide Seiten werden vorher auf ihre
        /// kanonische Form gebracht.
        /// </summary>
        /// <param name="name">der Name des Arguments</param>
        /// <param name="value">der gemeldete Wert</param>
        /// <param name="type">der Typ, nach dem verglichen wird</param>
        /// <returns>true, wenn beide Werte dasselbe bezeichnen</returns>
        public bool Matches(string name, object value, AssetArgumentType type)
        {
            var stored = this[name];
            if (stored == null)
            {
                return false;
            }

            var raw = value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value?.ToString();
            return TryCanonicalize(raw, type, out var normalized)
                   && string.Equals(stored, normalized, StringComparison.Ordinal);
        }

        /// <summary>
        /// Serialisiert die Werte. Dieselbe Form wird an der Freigabe gespeichert und in ein Ad-hoc-Ticket
        /// gelegt - ein Format, ein Vergleichsweg.
        /// </summary>
        /// <returns>die Werte als JSON</returns>
        public string ToJson() => JsonSerializer.Serialize(values);

        /// <summary>
        /// Liest eine gespeicherte Wertemenge. Fehlerhafte Ablage ergibt eine leere Menge - der Aufrufer
        /// entscheidet, ob das ein Fehler ist.
        /// </summary>
        /// <param name="json">die gespeicherten Werte</param>
        /// <returns>die Wertemenge, nie null</returns>
        public static AssetArgumentValues FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Empty;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return parsed == null
                    ? Empty
                    : new AssetArgumentValues(new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase));
            }
            catch (JsonException)
            {
                return Empty;
            }
        }

        /// <summary>
        /// Die kanonische Form eines Wertes. Sie entscheidet, was als gleich gilt.
        /// </summary>
        private static bool TryCanonicalize(string raw, AssetArgumentType type, out string canonical)
        {
            canonical = null;
            if (raw == null)
            {
                return false;
            }

            var trimmed = raw.Trim();
            switch (type)
            {
                case AssetArgumentType.Int:
                    if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    {
                        return false;
                    }

                    canonical = i.ToString(CultureInfo.InvariantCulture);
                    return true;
                case AssetArgumentType.Long:
                    if (!long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                    {
                        return false;
                    }

                    canonical = l.ToString(CultureInfo.InvariantCulture);
                    return true;
                case AssetArgumentType.Guid:
                    if (!Guid.TryParse(trimmed, out var g))
                    {
                        return false;
                    }

                    // Klammern und Bindestriche sind Schreibweise, nicht Inhalt.
                    canonical = g.ToString("D");
                    return true;
                case AssetArgumentType.Date:
                    if (!DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    {
                        return false;
                    }

                    // Nur das Datum: eine Freigabe fuer einen Tag soll nicht an der Uhrzeit scheitern.
                    canonical = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    return true;
                default:
                    // Zeichenketten werden nicht umgeformt, aber ohne Ruecksicht auf Gross- und
                    // Kleinschreibung verglichen - so, wie eine URL sich auch verhaelt.
                    canonical = trimmed.ToLowerInvariant();
                    return true;
            }
        }
    }
}
