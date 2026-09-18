using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers
{
    /// <summary>
    /// Bildet die Feldbeschreibung einer Terminal-Anbindung auf die Maske des Toolkits ab.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die Kante, von der bei <see cref="DeclaredField"/> die Rede ist: die Deklarationen liegen in
    /// einem Paket, das der Kassen-Agent lädt und das Blazor deshalb nicht kennen darf. Übersetzt wird
    /// dort, wo die Abhängigkeit ohnehin besteht — hier.
    /// </para>
    /// <para>
    /// Die Umwandlung der Art ist ein blosser Zahlenwechsel, weil beide Aufzählungen <b>dieselbe
    /// Reihenfolge</b> haben. Das ist keine Nachlässigkeit, sondern Absicht und an beiden Stellen so
    /// vermerkt; ein Test hält es fest.
    /// </para>
    /// </remarks>
    public static class TerminalFieldProjection
    {
        /// <summary>Übersetzt eine Feldliste.</summary>
        /// <param name="fields">die Beschreibung der Anbindung</param>
        /// <param name="choices">
        /// die zur Laufzeit ermittelten Auswahllisten, nach Feldnamen. Fehlt eine, bleibt das Feld ein
        /// Textfeld — bedienbar, nur unschöner.
        /// </param>
        public static IReadOnlyList<DeclaredField> ToDeclaredFields(
            IReadOnlyList<TerminalSettingDescriptor> fields,
            IReadOnlyDictionary<string, IReadOnlyList<TerminalSettingChoice>>? choices = null)
            => [.. fields.Select(f => ToDeclaredField(f, choices))];

        /// <summary>Übersetzt ein Feld.</summary>
        public static DeclaredField ToDeclaredField(TerminalSettingDescriptor field,
            IReadOnlyDictionary<string, IReadOnlyList<TerminalSettingChoice>>? choices = null)
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
        /// Die Auswahlmöglichkeiten eines Feldes — die festen oder die zur Laufzeit ermittelten.
        /// </summary>
        /// <remarks>
        /// Eine dynamische Quelle, zu der nichts geliefert wurde, ergibt <c>null</c> und damit ein
        /// Textfeld. <b>Nicht eine leere Auswahlliste:</b> die liesse sich nicht bedienen, und wer den
        /// Namen kennt, käme nicht weiter.
        /// </remarks>
        private static IReadOnlyList<DeclaredChoice>? Resolve(TerminalSettingDescriptor field,
            IReadOnlyDictionary<string, IReadOnlyList<TerminalSettingChoice>>? choices)
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

        /// <summary>
        /// Die Vorbelegung aus den Feldern — damit die Maske die Vorschläge zeigt, die die Anbindung
        /// mitgibt.
        /// </summary>
        public static Dictionary<string, object?> Defaults(IReadOnlyList<TerminalSettingDescriptor> fields,
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
    }
}
