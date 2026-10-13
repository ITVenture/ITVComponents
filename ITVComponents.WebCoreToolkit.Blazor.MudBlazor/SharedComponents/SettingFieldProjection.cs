using System.Collections.Generic;
using System.Linq;
using ITVComponents.AgentServices.Abstractions;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents
{
    /// <summary>
    /// Bildet eine Selbstbeschreibung (<see cref="SettingDescriptor"/>) auf die Maske des Toolkits ab.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die Deklarationen liegen in einem Paket, das auch der Agent lädt und das Blazor deshalb nicht kennen
    /// darf. Übersetzt wird dort, wo die Abhängigkeit ohnehin besteht — hier.
    /// </para>
    /// <para>
    /// Die Umwandlung der Art ist ein blosser Zahlenwechsel, weil <see cref="SettingKind"/> und
    /// <see cref="DeclaredFieldKind"/> <b>dieselbe Reihenfolge</b> haben. Das ist Absicht, an beiden Stellen
    /// vermerkt und durch einen Test festgehalten.
    /// </para>
    /// </remarks>
    public static class SettingFieldProjection
    {
        /// <summary>Übersetzt eine Feldliste.</summary>
        /// <param name="fields">die Selbstbeschreibung</param>
        /// <param name="choices">
        /// die zur Laufzeit ermittelten Auswahllisten, nach Feldnamen. Fehlt eine, bleibt das Feld ein
        /// Textfeld — bedienbar, nur unschöner.
        /// </param>
        public static IReadOnlyList<DeclaredField> ToDeclaredFields(IReadOnlyList<SettingDescriptor> fields,
            IReadOnlyDictionary<string, IReadOnlyList<SettingChoice>>? choices = null)
            => [.. fields.Select(f => ToDeclaredField(f, choices))];

        /// <summary>Übersetzt ein Feld.</summary>
        public static DeclaredField ToDeclaredField(SettingDescriptor field,
            IReadOnlyDictionary<string, IReadOnlyList<SettingChoice>>? choices = null)
        {
            var resolved = Resolve(field, choices);
            return new DeclaredField
            {
                Name = field.Name,
                Label = field.Label,
                // Derselbe Platz in beiden Aufzaehlungen - siehe Klassenkommentar.
                Kind = resolved != null ? DeclaredFieldKind.Choice : (DeclaredFieldKind)(int)field.Kind,
                Required = field.Required,
                HelpText = field.HelpText,
                Choices = resolved
            };
        }

        /// <summary>
        /// Die Vorbelegung aus den Feldern, überschrieben von dem, was schon gespeichert ist.
        /// </summary>
        public static Dictionary<string, object?> Defaults(IReadOnlyList<SettingDescriptor> fields,
            IReadOnlyDictionary<string, string?>? existing = null)
        {
            var result = new Dictionary<string, object?>();
            foreach (var field in fields)
            {
                if (existing != null && existing.TryGetValue(field.Name, out var stored)
                                     && !string.IsNullOrWhiteSpace(stored))
                {
                    result[field.Name] = stored;
                }
                else if (!string.IsNullOrWhiteSpace(field.DefaultValue))
                {
                    result[field.Name] = field.DefaultValue;
                }
            }

            return result;
        }

        /// <summary>
        /// Die Auswahlmöglichkeiten eines Feldes — die festen oder die zur Laufzeit ermittelten.
        /// </summary>
        /// <remarks>
        /// Eine dynamische Quelle, zu der nichts geliefert wurde, ergibt <c>null</c> und damit ein Textfeld.
        /// <b>Nicht eine leere Auswahlliste:</b> die liesse sich nicht bedienen.
        /// </remarks>
        private static IReadOnlyList<DeclaredChoice>? Resolve(SettingDescriptor field,
            IReadOnlyDictionary<string, IReadOnlyList<SettingChoice>>? choices)
        {
            if (!string.IsNullOrEmpty(field.ChoiceSource)
                && choices != null
                && choices.TryGetValue(field.Name, out var dynamic)
                && dynamic.Count != 0)
            {
                return [.. dynamic.Select(c => new DeclaredChoice { Value = c.Value, Label = c.Label })];
            }

            return field.Choices is { Count: > 0 }
                ? [.. field.Choices.Select(c => new DeclaredChoice { Value = c.Value, Label = c.Label })]
                : null;
        }
    }
}
