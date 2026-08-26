using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Die Grundfassung der Registry: sie merkt sich die Meldungen im Prozess. Damit funktioniert die
    /// Konstruktor-Injektion in jedem Host, auch ohne Datenbank - die Maske weiss dann eben nur so viel,
    /// wie die laufende Instanz seit dem Start gesehen hat.
    /// <para>
    /// Die persistierende Fassung im EF-Paket leitet hiervon ab und ergaenzt das Wegschreiben. Alles, was
    /// mit dem In-Memory-Zustand zu tun hat - Vergleich, Ersetzen, Auslesen - steht deshalb hier und
    /// existiert nur einmal.
    /// </para>
    /// </summary>
    public class InMemoryAssetArgumentRegistry : IAssetArgumentRegistry
    {
        private readonly ConcurrentDictionary<string, AssetConsumerDeclaration> known =
            new(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public void Declare(AssetConsumerKind kind, string key, params AssetArgumentDeclaration[] arguments)
            => Declare(new AssetConsumerDeclaration(kind, key, arguments ?? Array.Empty<AssetArgumentDeclaration>()));

        /// <inheritdoc/>
        public void Declare(AssetConsumerDeclaration declaration)
        {
            if (declaration == null || string.IsNullOrWhiteSpace(declaration.Key))
            {
                return;
            }

            var normalized = declaration with
            {
                Key = declaration.Key.Trim(),
                Arguments = declaration.Arguments ?? Array.Empty<AssetArgumentDeclaration>()
            };

            var id = KeyOf(normalized.Kind, normalized.Key);
            var changed = true;
            known.AddOrUpdate(id, normalized, (_, existing) =>
            {
                changed = !SameArguments(existing, normalized);
                return changed ? normalized : existing;
            });

            OnDeclared(normalized, changed);
        }

        /// <inheritdoc/>
        public IReadOnlyList<AssetConsumerDeclaration> GetConsumers()
        {
            EnsureLoaded();
            return known.Values.OrderBy(n => n.Kind).ThenBy(n => n.Key, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <inheritdoc/>
        public AssetConsumerDeclaration Find(AssetConsumerKind kind, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            EnsureLoaded();
            return known.TryGetValue(KeyOf(kind, key.Trim()), out var found) ? found : null;
        }

        /// <summary>
        /// Haengt eine abgeleitete Fassung an die Meldung an. <paramref name="changed"/> ist false, wenn
        /// dieselbe Deklaration schon bekannt war - dann gibt es nichts zu schreiben ausser vielleicht
        /// einem Zeitstempel.
        /// </summary>
        /// <param name="declaration">die gemeldete Deklaration</param>
        /// <param name="changed">ob sie neu oder veraendert ist</param>
        protected virtual void OnDeclared(AssetConsumerDeclaration declaration, bool changed)
        {
        }

        /// <summary>
        /// Erlaubt einer abgeleiteten Fassung, den gespeicherten Bestand nachzuladen, bevor gelesen wird.
        /// </summary>
        protected virtual void EnsureLoaded()
        {
        }

        /// <summary>
        /// Uebernimmt eine Deklaration in den Speicher, ohne sie als Meldung zu behandeln - fuer das
        /// Nachladen aus der Ablage.
        /// </summary>
        /// <param name="declaration">die geladene Deklaration</param>
        protected void Adopt(AssetConsumerDeclaration declaration)
        {
            if (declaration != null && !string.IsNullOrWhiteSpace(declaration.Key))
            {
                known[KeyOf(declaration.Kind, declaration.Key)] = declaration;
            }
        }

        private static string KeyOf(AssetConsumerKind kind, string key) => $"{(int)kind}:{key}";

        /// <summary>
        /// Vergleicht zwei Deklarationen inhaltlich. Die Reihenfolge zaehlt mit, weil sie die Maske
        /// steuert - eine geaenderte Reihenfolge ist eine geaenderte Deklaration.
        /// </summary>
        private static bool SameArguments(AssetConsumerDeclaration left, AssetConsumerDeclaration right)
        {
            if (left.Arguments.Length != right.Arguments.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Arguments.Length; i++)
            {
                var a = left.Arguments[i];
                var b = right.Arguments[i];
                if (!string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                    || a.Type != b.Type || a.Required != b.Required)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
