using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.SharedAssets
{
    /// <summary>
    /// Die Registry, die ihre Deklarationen ueber den Neustart hinaus behaelt. Ohne sie waere die
    /// Teilen-Maske nach jedem Deployment blind, bis jemand die betreffende Seite besucht hat.
    /// <para>
    /// <b>Gemeldet wird aus Konstruktoren</b>, also potenziell bei jedem Seitenaufbau. Deshalb passiert
    /// auf dem Anfragepfad nur ein Vergleich im Speicher; wirklich Neues wandert in eine Warteschlange,
    /// die ein Hintergrund-Worker gebuendelt wegschreibt. Genau an dieser Stelle hat die
    /// Auto-Berechtigungs-Registrierung ihren teuersten Fehler gehabt - inline schreiben und
    /// invalidieren, bis der Datenbank die Ressourcen ausgingen. Denselben Weg gehen wir nicht zweimal.
    /// </para>
    /// </summary>
    public sealed class PersistentAssetArgumentRegistry : InMemoryAssetArgumentRegistry, IDisposable
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<PersistentAssetArgumentRegistry> logger;
        private readonly AssetArgumentRegistryOptions options;

        private readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> lastWritten = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AssetConsumerDeclaration> queued = new(StringComparer.OrdinalIgnoreCase);
        private readonly object gate = new();
        private readonly SemaphoreSlim wake = new(0);
        private readonly CancellationTokenSource cts = new();
        private readonly Task worker;

        private volatile bool loaded;
        private readonly object loadGate = new();

        public PersistentAssetArgumentRegistry(IServiceScopeFactory scopeFactory,
            ILogger<PersistentAssetArgumentRegistry> logger, IOptions<AssetArgumentRegistryOptions> options)
        {
            this.scopeFactory = scopeFactory;
            this.logger = logger;
            this.options = options?.Value ?? new AssetArgumentRegistryOptions();
            worker = Task.Run(() => RunAsync(cts.Token));
        }

        /// <summary>
        /// Laedt den gespeicherten Bestand genau einmal nach. Das ist der eigentliche Zweck der
        /// Persistenz: die Maske kennt danach auch, was seit dem Neustart niemand besucht hat.
        /// </summary>
        protected override void EnsureLoaded()
        {
            if (loaded || !options.Persist)
            {
                return;
            }

            lock (loadGate)
            {
                if (loaded)
                {
                    return;
                }

                // Erst danach setzen: schlaegt das Laden fehl, versucht es der naechste Leser erneut,
                // statt fuer die Lebensdauer des Prozesses eine leere Registry zu behaupten.
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var factory = scope.ServiceProvider.GetService<ICoreSystemContextFactory>();
                    if (factory == null)
                    {
                        logger.LogWarning(
                            "No ICoreSystemContextFactory registered - the asset-argument registry stays memory-only for this process.");
                        loaded = true;
                        return;
                    }

                    var db = factory.CreateContext();
                    try
                    {
                        db.ShowAllTenants = true;
                        var stored = db.AssetConsumers.Include(n => n.Arguments).AsNoTracking().ToArray();
                        foreach (var consumer in stored)
                        {
                            Adopt(ToDeclaration(consumer));
                            lastWritten[Identity(consumer.DeclarationKind, consumer.DeclarationKey)] =
                                consumer.LastSeenUtc;
                        }

                        logger.LogDebug("Loaded {count} asset-argument consumer(s) from the database.", stored.Length);
                    }
                    finally
                    {
                        (db as IDisposable)?.Dispose();
                    }

                    loaded = true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Could not load the declared asset-argument consumers; the registry answers from memory until this succeeds.");
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnDeclared(AssetConsumerDeclaration declaration, bool changed)
        {
            if (!options.Persist)
            {
                return;
            }

            EnsureLoaded();
            var id = Identity(declaration.Kind, declaration.Key);
            lock (gate)
            {
                if (!changed && lastWritten.TryGetValue(id, out var written)
                    && DateTime.UtcNow - written < TimeSpan.FromHours(Math.Max(1, options.TouchIntervalHours)))
                {
                    // Unveraendert und erst kuerzlich gesehen: den Zeitstempel bei jedem Seitenaufbau
                    // nachzufuehren waere genau der Schreibsturm, den wir vermeiden wollen. Ein Zeitstempel
                    // auf Tagesgenauigkeit reicht fuer die Frage, ob eine Zeile noch aktuell ist.
                    return;
                }

                queued[id] = declaration;
                pending.Add(id);
            }

            wake.Release();
        }

        private async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await wake.WaitAsync(ct).ConfigureAwait(false);
                    if (options.DebounceMilliseconds > 0)
                    {
                        await Task.Delay(options.DebounceMilliseconds, ct).ConfigureAwait(false);
                    }

                    while (wake.Wait(0))
                    {
                    }

                    AssetConsumerDeclaration[] batch;
                    lock (gate)
                    {
                        batch = pending.Select(n => queued[n]).ToArray();
                        pending.Clear();
                    }

                    if (batch.Length != 0)
                    {
                        ProcessBatch(batch);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Asset-argument registry worker iteration failed.");
                }
            }
        }

        private void ProcessBatch(AssetConsumerDeclaration[] batch)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var factory = scope.ServiceProvider.GetRequiredService<ICoreSystemContextFactory>();
                var db = factory.CreateContext();
                try
                {
                    db.ShowAllTenants = true;
                    var now = DateTime.UtcNow;
                    foreach (var declaration in batch)
                    {
                        Upsert(db, declaration, now);
                    }

                    db.SaveChanges();
                    lock (gate)
                    {
                        foreach (var declaration in batch)
                        {
                            lastWritten[Identity(declaration.Kind, declaration.Key)] = now;
                        }
                    }

                    logger.LogDebug("Persisted {count} asset-argument declaration(s).", batch.Length);
                }
                finally
                {
                    (db as IDisposable)?.Dispose();
                }
            }
            catch (Exception ex)
            {
                // Nicht als geschrieben vermerken: die naechste Meldung desselben Endpunkts versucht es
                // erneut. Eine verlorene Deklaration wuerde sonst bis zum naechsten Neustart fehlen.
                logger.LogError(ex,
                    "Could not persist a batch of asset-argument declarations; they will be retried on the next declaration.");
            }
        }

        private static void Upsert(ICoreSystemContext db, AssetConsumerDeclaration declaration, DateTime now)
        {
            var consumer = db.AssetConsumers.Include(n => n.Arguments)
                .FirstOrDefault(n => n.DeclarationKind == declaration.Kind && n.DeclarationKey == declaration.Key);
            if (consumer == null)
            {
                consumer = new AssetConsumer
                {
                    DeclarationKind = declaration.Kind,
                    DeclarationKey = declaration.Key,
                    FirstSeenUtc = now
                };

                db.AssetConsumers.Add(consumer);
            }

            consumer.LastSeenUtc = now;

            // Die Meldung ist die vollstaendige Liste aus Sicht des Endpunkts, also darf hier ersetzt
            // werden - wir haben in diesem Moment frische, vollstaendige Information. Was NICHT passiert:
            // einen Konsumenten loeschen, der sich nicht gemeldet hat. Abwesenheit ist keine Information.
            var wanted = declaration.Arguments;
            var byName = consumer.Arguments.ToDictionary(n => n.ArgumentName, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < wanted.Length; i++)
            {
                if (!byName.TryGetValue(wanted[i].Name, out var argument))
                {
                    argument = new AssetConsumerArgument
                    {
                        Consumer = consumer,
                        ArgumentName = wanted[i].Name
                    };

                    consumer.Arguments.Add(argument);
                    db.AssetConsumerArguments.Add(argument);
                }
                else
                {
                    byName.Remove(wanted[i].Name);
                }

                argument.ArgumentType = wanted[i].Type;
                argument.Required = wanted[i].Required;
                argument.SortOrder = i;
            }

            foreach (var orphan in byName.Values)
            {
                db.AssetConsumerArguments.Remove(orphan);
            }
        }

        private static AssetConsumerDeclaration ToDeclaration(AssetConsumer consumer)
            => new(consumer.DeclarationKind, consumer.DeclarationKey,
                consumer.Arguments.OrderBy(n => n.SortOrder)
                    .Select(n => new AssetArgumentDeclaration(n.ArgumentName, n.ArgumentType, n.Required))
                    .ToArray());

        private static string Identity(AssetConsumerKind kind, string key) => $"{(int)kind}:{key}";

        public void Dispose()
        {
            cts.Cancel();
            try
            {
                worker.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Shutdown race while stopping the asset-argument registry worker.");
            }

            cts.Dispose();
            wake.Dispose();
        }
    }
}
