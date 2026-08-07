using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Extensibility
{
    /// <summary>
    /// Standard-Implementierung von <see cref="ICustomCompanyInfoProvider"/>: liest die Namensliste aus
    /// den GlobalSettings und least die Module ueber den frischen Ladeweg.
    /// </summary>
    /// <remarks>
    /// Bewusst der frische Ladeweg (<see cref="IFreshInjectablePlugin{T}"/>) und nicht der geteilte: ein
    /// Modul legt seine Angaben in einer eigenen Datenbank ab und haelt damit typischerweise einen
    /// Datenbank-Kontext. Ein pro Scope geteiltes solches Objekt ist unter Blazor genau die Bauart, aus
    /// der "a second operation was started on this context" entsteht.
    /// </remarks>
    public class CustomCompanyInfoProvider : ICustomCompanyInfoProvider
    {
        private readonly IServiceProvider services;
        private readonly IGlobalSettings<CustomCompanyInfoOptions> options;
        private readonly ILogger<CustomCompanyInfoProvider> logger;

        public CustomCompanyInfoProvider(IServiceProvider services,
            IGlobalSettings<CustomCompanyInfoOptions> options,
            ILogger<CustomCompanyInfoProvider> logger)
        {
            this.services = services;
            this.options = options;
            this.logger = logger;
        }

        /// <summary>
        /// Der Plugin-Zugang - bewusst erst dann aufgeloest, wenn wirklich Module konfiguriert sind.
        /// Waere er eine Konstruktor-Abhaengigkeit, muesste jeder Host, der gar keine Zusatzangaben
        /// benutzt, trotzdem <c>UseInjectablePlugins</c> aufgerufen haben - und bekaeme sonst beim
        /// Onboarding einen Fehler ueber ein Feature, das er nicht eingeschaltet hat.
        /// </summary>
        private IFreshInjectablePlugin<ICustomCompanyInformationHandler> Plugins
            => (IFreshInjectablePlugin<ICustomCompanyInformationHandler>)services
                .GetService(typeof(IFreshInjectablePlugin<ICustomCompanyInformationHandler>));

        public async Task<IReadOnlyList<CustomInfoTab>> DescribeAsync(CustomInfoContext ctx, bool loadExisting = false,
            CancellationToken ct = default)
        {
            var result = new List<CustomInfoTab>();
            foreach (string name in ConfiguredNames())
            {
                using (IPluginLease<ICustomCompanyInformationHandler> lease = Lease(name))
                {
                    ICustomCompanyInformationHandler handler = lease?.Value;
                    if (handler == null || !IsResponsible(handler, ctx, name))
                    {
                        continue;
                    }

                    try
                    {
                        var tab = new CustomInfoTab
                        {
                            Key = handler.Key,
                            Title = handler.Title,
                            Icon = handler.Icon,
                            ViewKey = handler.ViewKey,
                            EditPermission = handler.EditPermission,
                            Fields = string.IsNullOrWhiteSpace(handler.ViewKey)
                                ? handler.GetFields(ctx) ?? new List<CustomInfoField>()
                                : new List<CustomInfoField>()
                        };

                        if (loadExisting && ctx.TenantId != null)
                        {
                            tab.Existing = await handler.LoadAsync(ctx.TenantId.Value, ct);
                        }

                        result.Add(tab);
                    }
                    catch (Exception ex)
                    {
                        // Ohne diese Zeile fehlte einfach ein Reiter - und niemand koennte sagen, warum
                        // ausgerechnet die Zusatzangaben dieses Moduls nicht erscheinen.
                        logger.LogError(ex, "Das Zusatzangaben-Modul '{Plugin}' konnte nicht beschrieben werden; sein Reiter fehlt.", name);
                    }
                }
            }

            return result;
        }

        public async Task<CustomInfoCheckResult> ValidateAsync(CustomInfoContext ctx,
            IReadOnlyDictionary<string, JsonNode> buckets, CancellationToken ct = default)
        {
            foreach (string name in ConfiguredNames())
            {
                using (IPluginLease<ICustomCompanyInformationHandler> lease = Lease(name))
                {
                    ICustomCompanyInformationHandler handler = lease?.Value;
                    if (handler == null || !IsResponsible(handler, ctx, name))
                    {
                        continue;
                    }

                    JsonNode payload = Bucket(buckets, handler.Key);
                    CustomInfoValidation verdict;
                    try
                    {
                        verdict = await handler.ValidateAsync(FlatValues(handler, payload), payload, ctx, ct);
                    }
                    catch (Exception ex)
                    {
                        // Ein Modul, das beim Pruefen zerbricht, darf nicht als "hat nichts einzuwenden"
                        // durchgehen - sonst entstuende ein Tenant mit Angaben, die nie geprueft wurden.
                        logger.LogError(ex, "Das Zusatzangaben-Modul '{Plugin}' ist beim Pruefen fehlgeschlagen; die Erfassung wird abgelehnt.", name);
                        return CustomInfoCheckResult.Rejected(handler.Key, null, null);
                    }

                    if (verdict != null && !verdict.Valid)
                    {
                        logger.LogDebug("Das Zusatzangaben-Modul '{Plugin}' hat die Angaben beanstandet: {Message}", name, verdict.Message);
                        return CustomInfoCheckResult.Rejected(handler.Key, verdict.Message, verdict.FieldName);
                    }
                }
            }

            return CustomInfoCheckResult.Ok();
        }

        public async Task<CustomInfoPersistResult> PersistAsync(CustomInfoContext ctx,
            IReadOnlyDictionary<string, JsonNode> buckets, CancellationToken ct = default)
        {
            if (ctx.TenantId == null)
            {
                // Programmierfehler, kein Betriebsfall: ohne Tenant gibt es nichts, woran die Angaben
                // haengen koennten.
                logger.LogError("Zusatzangaben sollten ohne TenantId abgelegt werden; es wurde nichts geschrieben.");
                return new CustomInfoPersistResult { Failed = ConfiguredNames().ToArray() };
            }

            var failed = new List<string>();
            var missing = new List<string>();
            foreach (string name in ConfiguredNames())
            {
                using (IPluginLease<ICustomCompanyInformationHandler> lease = Lease(name))
                {
                    ICustomCompanyInformationHandler handler = lease?.Value;
                    if (handler == null)
                    {
                        failed.Add(name);
                        continue;
                    }

                    if (!IsResponsible(handler, ctx, name))
                    {
                        continue;
                    }

                    JsonNode payload = Bucket(buckets, handler.Key);
                    if (payload == null)
                    {
                        // Zustaendig, aber es liegt nichts vor: typischerweise ein Modul, das erst nach dem
                        // Erfassen dazu kam. Kein Fehler des Benutzers - aber es fehlt etwas.
                        logger.LogWarning("Zum Zusatzangaben-Modul '{Plugin}' liegen fuer Tenant {TenantId} keine Angaben vor; sie muessen im Firmenprofil nachgetragen werden.", name, ctx.TenantId);
                        missing.Add(handler.Key);
                        continue;
                    }

                    try
                    {
                        await handler.PersistAsync(new CustomInfoPersistContext
                        {
                            TenantId = ctx.TenantId.Value,
                            Info = ctx,
                            Payload = payload,
                            Fields = FlatValues(handler, payload)
                        }, ct);
                    }
                    catch (Exception ex)
                    {
                        // Der Tenant besteht bereits - abbrechen wuerde die uebrigen Module nur ebenfalls
                        // um ihre Angaben bringen. Also weitermachen und den Ausfall melden.
                        logger.LogError(ex, "Das Zusatzangaben-Modul '{Plugin}' konnte seine Angaben zu Tenant {TenantId} nicht ablegen.", name, ctx.TenantId);
                        failed.Add(handler.Key);
                    }
                }
            }

            return new CustomInfoPersistResult { Failed = failed, Missing = missing };
        }

        /// <summary>Die konfigurierten Plugin-Namen in ihrer Reihenfolge; leer wenn nichts konfiguriert ist.</summary>
        private IEnumerable<string> ConfiguredNames()
            => (options.ValueOrDefault?.Handlers ?? new string[0]).Where(n => !string.IsNullOrWhiteSpace(n));

        /// <summary>
        /// Least ein Modul. Ist es nicht (mehr) vorhanden oder laesst es sich nicht laden, wird das
        /// protokolliert und null geliefert - eine kaputte Konfiguration darf die Erfassung nicht
        /// verhindern, aber sie darf auch nicht unbemerkt bleiben.
        /// </summary>
        private IPluginLease<ICustomCompanyInformationHandler> Lease(string name)
        {
            try
            {
                IFreshInjectablePlugin<ICustomCompanyInformationHandler> loader = Plugins;
                if (loader == null)
                {
                    logger.LogError("Es sind Zusatzangaben-Module konfiguriert ('{Plugin}' und ggf. weitere), aber der Plugin-Ladeweg ist nicht eingerichtet - dem Host fehlt der Aufruf von UseInjectablePlugins. Es werden keine Zusatzangaben erfasst.", name);
                    return null;
                }

                IPluginLease<ICustomCompanyInformationHandler> lease = loader.Lease(name);
                if (lease?.Value == null)
                {
                    logger.LogError("Das als Zusatzangaben-Modul konfigurierte Plugin '{Plugin}' wurde nicht gefunden; seine Angaben werden weder erfasst noch abgelegt.", name);
                    lease?.Dispose();
                    return null;
                }

                return lease;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Das als Zusatzangaben-Modul konfigurierte Plugin '{Plugin}' konnte nicht geladen werden; seine Angaben werden weder erfasst noch abgelegt.", name);
                return null;
            }
        }

        /// <summary>
        /// Fuehlt sich das Modul in diesem Fall zustaendig? Eine Ausnahme dabei gilt als "nein" - mit
        /// Protokolleintrag, weil das Modul sonst spurlos aus der Erfassung verschwaende.
        /// </summary>
        private bool IsResponsible(ICustomCompanyInformationHandler handler, CustomInfoContext ctx, string name)
        {
            try
            {
                return handler.AppliesTo(ctx);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Das Zusatzangaben-Modul '{Plugin}' konnte seine Zustaendigkeit nicht bestimmen; es bleibt in diesem Vorgang aussen vor.", name);
                return false;
            }
        }

        private static JsonNode Bucket(IReadOnlyDictionary<string, JsonNode> buckets, string key)
        {
            if (buckets == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            return buckets.TryGetValue(key, out JsonNode node) ? node : null;
        }

        /// <summary>
        /// Die flache Sicht auf einen Datensatz - nur fuer Module mit generischer Maske. Wer eine eigene
        /// Maske mitbringt, bestimmt die Form seines Datensatzes selbst; ihn flach zu machen waere
        /// Raterei.
        /// </summary>
        private static IReadOnlyDictionary<string, string> FlatValues(ICustomCompanyInformationHandler handler, JsonNode payload)
        {
            if (!string.IsNullOrWhiteSpace(handler.ViewKey) || payload is not JsonObject obj)
            {
                return null;
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in obj)
            {
                // ToString() und nicht GetValue<string>(): der Datensatz kommt aus einer fremden Ablage
                // und muss keine reinen Zeichenketten enthalten. GetValue<string> wuerde bei einer Zahl
                // werfen, ToString liefert die Zeichenkette ohne Anfuehrungszeichen.
                values[entry.Key] = entry.Value?.ToString();
            }

            return values;
        }
    }
}
