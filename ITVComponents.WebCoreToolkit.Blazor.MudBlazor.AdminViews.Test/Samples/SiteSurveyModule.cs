using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Extensibility;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test.Samples
{
    /// <summary>
    /// Beispiel-Modul fuer die Zusatzangaben mit EIGENER Maske - die Vorlage, die im Leitfaden (§23.5)
    /// beschrieben ist, hier in uebersetzbarer Form.
    /// </summary>
    /// <remarks>
    /// Es steht bewusst in einem Testprojekt und nicht im ausgelieferten Paket: es waere sonst Code, den
    /// niemand benutzt, der aber bei jedem Kunden mitginge. Was es hier leistet, ist trotzdem echt - der
    /// Vertrag wird uebersetzt, nicht nur beschrieben.
    /// <para>
    /// Es faellt auf, dass hier NICHTS von Blazor vorkommt: das ist der Zweck der Trennung. Die zugehoerige
    /// Maske nennt das Modul nur ueber <see cref="ViewKey"/>; welche Komponente dahinter steht, entscheidet
    /// der Host beim Start.
    /// </para>
    /// </remarks>
    public class SiteSurveyModule : ICustomCompanyInformationHandler
    {
        /// <summary>Der abgelegte Datensatz je Mandant - im Ernstfall die eigene Datenbank des Moduls.</summary>
        private readonly Dictionary<int, JsonNode> stored = new();

        public string UniqueName { get; set; } = "SiteSurvey";

        public string Key => "sample.sitesurvey";

        public string Title => "{\"en\":\"Site survey\",\"de\":\"Standort-Erhebung\"}";

        public string Icon => "Icons.Material.Filled.Factory";

        /// <summary>
        /// Der Schluessel der eigenen Maske. Kein Typ - siehe die Anmerkung an
        /// <c>CustomCompanyInfoViewConfiguration</c>.
        /// </summary>
        public string ViewKey => "sample.sitesurvey";

        /// <summary>
        /// Nachtragen im Firmenprofil verlangt eine eigene Berechtigung; waehrend der Anlage gilt sie nicht.
        /// </summary>
        public string EditPermission => "Sample.SiteSurvey.Write";

        /// <summary>
        /// Nur Firmen werden erhoben, und nur wenn sie sich selbst anmelden: ein auf Einladung entstandener
        /// Mandant erbt die Angaben vom uebergeordneten. Genau dafuer ist der Kontext da - und er nennt die
        /// Mandanten-Strategie nirgends.
        /// </summary>
        public bool AppliesTo(CustomInfoContext ctx)
            => ctx.ProfileType == ProfileType.Company && ctx.Origin == CustomInfoOrigin.SelfService;

        /// <summary>Leer, weil dieses Modul eine eigene Maske mitbringt.</summary>
        public IReadOnlyList<CustomInfoField> GetFields(CustomInfoContext ctx)
            => Array.Empty<CustomInfoField>();

        /// <summary>
        /// Fachliche Pruefung. <paramref name="values"/> ist hier immer leer - die flache Sicht gibt es nur
        /// fuer die generische Maske; wer eine eigene mitbringt, bestimmt die Form seines Datensatzes selbst
        /// und liest <paramref name="payload"/>.
        /// </summary>
        public Task<CustomInfoValidation> ValidateAsync(IReadOnlyDictionary<string, string> values,
            JsonNode payload, CustomInfoContext ctx, CancellationToken ct)
        {
            if (payload is not JsonObject obj)
            {
                return Task.FromResult(CustomInfoValidation.Failed(
                    "{\"en\":\"Please complete the site survey.\",\"de\":\"Bitte fuellen Sie die Standort-Erhebung aus.\"}"));
            }

            int? sites = obj["Sites"]?.GetValue<int?>();
            if (sites is null or < 1)
            {
                // Das Feld wird mitgenannt, damit die Maske die Stelle markieren kann statt nur eine
                // Meldung ueber dem Reiter zu zeigen.
                return Task.FromResult(CustomInfoValidation.Failed(
                    "{\"en\":\"At least one site is required.\",\"de\":\"Mindestens ein Standort ist noetig.\"}",
                    "Sites"));
            }

            return Task.FromResult(CustomInfoValidation.Ok());
        }

        /// <summary>
        /// Liefert den abgelegten Datensatz, oder null wenn zu diesem Mandanten noch nichts vorliegt.
        /// </summary>
        /// <remarks>
        /// Das <c>!</c> ist kein Schummeln: der Vertrag erlaubt null ausdruecklich, ist aber nicht
        /// nullable-annotiert, weil die Vertrags-Bibliothek ohne eingeschalteten Nullable-Kontext
        /// uebersetzt wird.
        /// </remarks>
        public Task<JsonNode> LoadAsync(int tenantId, CancellationToken ct)
            => Task.FromResult(stored.TryGetValue(tenantId, out JsonNode? node) ? node : null)!;

        /// <summary>
        /// Ablegen. Wiederholbar gebaut, wie es der Vertrag verlangt: es gibt keine gemeinsame Transaktion
        /// mit der Mandanten-Anlage, ein zweiter Versuch muss also dasselbe Ergebnis liefern.
        /// </summary>
        public Task PersistAsync(CustomInfoPersistContext ctx, CancellationToken ct)
        {
            stored[ctx.TenantId] = ctx.Payload;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            stored.Clear();
            // Der Plugin-Bestand haengt an diesem Ereignis, um aufzuraeumen. Wer es nicht ausloest,
            // hinterlaesst dort einen Eintrag auf ein Objekt, das es nicht mehr gibt.
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? Disposed;
    }
}
