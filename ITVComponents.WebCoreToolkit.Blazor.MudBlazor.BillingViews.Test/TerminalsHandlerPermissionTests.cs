using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Test
{
    /// <summary>
    /// Weist nach, dass <see cref="TerminalsHandler{TContext}"/> in JEDER Methode selbst prueft.
    /// </summary>
    /// <remarks>
    /// Nachgezogen zu den Tests der beiden anderen Handler. Der Aufbau ist derselbe: gesperrte Datenbank
    /// plus Attrappen, die ihre Aufrufe zaehlen — damit faellt auf, wenn eine Pruefung erst hinter dem
    /// Datenzugriff greift.
    /// </remarks>
    [TestClass]
    public class TerminalsHandlerPermissionTests
    {
        private static TerminalsHandler<BillingTestContext> Handler(HandlerTestEnvironment env,
            SpyTerminalAdministration administration, SpyTerminalCatalog catalog,
            SpyTerminalPaymentService terminals, SpyChoiceProvider choices)
            => new(env.DbFactory(), env.Services, administration, catalog, terminals,
                new ITerminalChoiceProvider[] { choices }, env.PaymentSettings(), env.FeatureGates());

        /// <summary>Ein Agent-Terminal nach dem ersten Schritt des Assistenten.</summary>
        private static TerminalDefinition AgentDraft()
            => new() { Provider = "agent", Route = "kasse-1", ConfigurationJson = "{\"objectName\":\"terminal\"}" };

        private static void AssertRefused(Action operation, string because)
        {
            var ex = Assert.ThrowsExactly<TenantPaymentException>(operation, because);
            Assert.AreEqual(PaymentErrorCodes.NotPermitted, ex.Code);
        }

        private static async Task AssertRefusedAsync(Func<Task> operation, string because)
        {
            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(operation, because);
            Assert.AreEqual(PaymentErrorCodes.NotPermitted, ex.Code);
        }

        // ------------------------------------------------------------------ ohne Recht: alles zu

        [TestMethod]
        public async Task EveryPathRefusesWithoutPermission()
        {
            var env = HandlerTestEnvironment.WithPermissions("SomethingElse");
            env.DatabaseIsOffLimits = true;
            var administration = new SpyTerminalAdministration();
            var catalog = new SpyTerminalCatalog();
            var terminals = new SpyTerminalPaymentService();
            var choices = new SpyChoiceProvider();
            var handler = Handler(env, administration, catalog, terminals, choices);

            AssertRefused(() => handler.GetProviders(), "Anbieter auflisten");
            AssertRefused(() => handler.DescribeSettings("stripe"), "Felder beschreiben");
            await AssertRefusedAsync(() => handler.DescribeDeviceSettingsAsync(AgentDraft()), "den Agenten befragen");
            await AssertRefusedAsync(() => handler.GetDeviceSettingsAsync(AgentDraft()), "Geraete-Einstellungen lesen");
            await AssertRefusedAsync(() => handler.GetDeviceChoicesAsync(AgentDraft(), "printer", null), "Geraete-Auswahl lesen");
            await AssertRefusedAsync(() => handler.ApplyDeviceSettingsAsync(AgentDraft(), "{}"), "auf dem Kassen-PC schreiben");
            await AssertRefusedAsync(() => handler.GetChoicesAsync(TerminalChoiceSources.ClientApps, null),
                "Auswahllisten lesen");
            await AssertRefusedAsync(() => handler.GetTerminalsAsync(), "Geraete auflisten");
            await AssertRefusedAsync(() => handler.SaveAsync(new TerminalDefinition()), "Geraet speichern");
            await AssertRefusedAsync(() => handler.SetEnabledAsync(1, false), "Geraet abschalten");
            await AssertRefusedAsync(() => handler.GetStatusAsync(1), "Zustand abfragen");

            Assert.AreEqual(0, catalog.Calls.Count, "Der Katalog wurde trotz fehlendem Recht angefasst.");
            Assert.AreEqual(0, administration.Calls.Count, "Die Verwaltung wurde trotz fehlendem Recht angefasst.");
            Assert.AreEqual(0, terminals.Calls.Count, "Die Dienstschicht wurde trotz fehlendem Recht angefasst.");
            Assert.AreEqual(0, choices.Calls.Count, "Die Auswahllisten wurden trotz fehlendem Recht gefuellt.");
        }

        // ------------------------------------------------------------------ Lesen ist kein Verwalten

        [TestMethod]
        public async Task ViewPermissionReadsButDoesNotManage()
        {
            var env = HandlerTestEnvironment.WithPermissions(TerminalsHandler<BillingTestContext>.ViewPermission);
            var administration = new SpyTerminalAdministration();
            var catalog = new SpyTerminalCatalog();
            var terminals = new SpyTerminalPaymentService();
            var choices = new SpyChoiceProvider();
            var handler = Handler(env, administration, catalog, terminals, choices);

            // Lesen: der Katalog, die eigenen Geraete, ihr Zustand.
            handler.GetProviders();
            handler.DescribeSettings("stripe");
            await handler.GetTerminalsAsync();
            await handler.GetStatusAsync(1);

            Assert.AreEqual(2, catalog.Calls.Count);
            Assert.AreEqual(1, administration.Calls.Count);
            Assert.AreEqual(1, terminals.Calls.Count);

            // Verwalten: nicht mit Leserecht.
            await AssertRefusedAsync(() => handler.SaveAsync(new TerminalDefinition()), "speichern");
            await AssertRefusedAsync(() => handler.SetEnabledAsync(1, false), "abschalten");

            Assert.AreEqual(1, administration.Calls.Count, "Nach dem Lesen darf nichts mehr durchgekommen sein.");
        }

        /// <summary>
        /// Die beiden Wege des Assistenten, die ueber den blossen Katalog hinausgehen, verlangen das
        /// Verwaltungsrecht — nicht das Leserecht.
        /// </summary>
        /// <remarks>
        /// Die Geraete-Wege sprechen den Agenten an, den der Entwurf nennt - und einer davon schreibt auf dem
        /// Kassen-PC; <c>GetChoicesAsync</c> liefert die Anwendungen dieses Mandanten mit Namen und
        /// Schluessel. Beides ist keine Katalogabfrage.
        /// </remarks>
        [TestMethod]
        public async Task ProbingTheDeviceAndListingChoicesNeedManagement()
        {
            var env = HandlerTestEnvironment.WithPermissions(TerminalsHandler<BillingTestContext>.ViewPermission);
            var administration = new SpyTerminalAdministration();
            var catalog = new SpyTerminalCatalog();
            var terminals = new SpyTerminalPaymentService();
            var choices = new SpyChoiceProvider();
            var handler = Handler(env, administration, catalog, terminals, choices);

            await AssertRefusedAsync(() => handler.DescribeDeviceSettingsAsync(AgentDraft()),
                "Leserecht darf keinen Agenten befragen.");
            await AssertRefusedAsync(() => handler.GetDeviceSettingsAsync(AgentDraft()),
                "Leserecht darf die Einstellungen eines Geraets nicht lesen.");
            await AssertRefusedAsync(() => handler.ApplyDeviceSettingsAsync(AgentDraft(), "{}"),
                "Leserecht darf nicht auf dem Kassen-PC schreiben.");
            await AssertRefusedAsync(() => handler.GetChoicesAsync(TerminalChoiceSources.ClientApps, null),
                "Leserecht darf die Anwendungen des Mandanten nicht auflisten.");

            Assert.AreEqual(0, catalog.Calls.Count, "Das Geraet wurde trotz fehlendem Verwaltungsrecht befragt.");
            Assert.AreEqual(0, choices.Calls.Count, "Die Auswahlliste wurde trotz fehlendem Verwaltungsrecht gefuellt.");
        }

        [TestMethod]
        public async Task ManagePermissionOpensTheWholeWizard()
        {
            var env = HandlerTestEnvironment.WithPermissions(TerminalsHandler<BillingTestContext>.ManagePermission);
            var administration = new SpyTerminalAdministration();
            var catalog = new SpyTerminalCatalog();
            var terminals = new SpyTerminalPaymentService();
            var choices = new SpyChoiceProvider();
            var handler = Handler(env, administration, catalog, terminals, choices);

            handler.GetProviders();
            handler.DescribeSettings("agent");
            await handler.DescribeDeviceSettingsAsync(AgentDraft());
            await handler.GetDeviceSettingsAsync(AgentDraft());
            await handler.ApplyDeviceSettingsAsync(AgentDraft(), "{}");
            var list = await handler.GetChoicesAsync(TerminalChoiceSources.ClientApps, null);
            await handler.SaveAsync(new TerminalDefinition { Provider = "agent" });

            Assert.AreEqual(5, catalog.Calls.Count, "Mit dem Verwaltungsrecht muss der Assistent durchlaufen.");
            Assert.AreEqual(1, choices.Calls.Count);
            Assert.AreEqual(1, list.Count, "Die Auswahlliste der Attrappe hat genau einen Eintrag.");
            Assert.AreEqual(1, administration.Calls.Count);
        }

        /// <summary>
        /// Der Mandant kommt aus dem Sicherheitsbereich und nicht aus der Anfrage: eine mitgeschickte fremde
        /// Id wird ueberschrieben, nicht uebernommen.
        /// </summary>
        [TestMethod]
        public async Task TheTenantComesFromTheScopeNotFromTheRequest()
        {
            var env = HandlerTestEnvironment.WithPermissions(TerminalsHandler<BillingTestContext>.ManagePermission);
            env.CurrentTenantId = 7;
            var administration = new SpyTerminalAdministration();
            var handler = Handler(env, administration, new SpyTerminalCatalog(), new SpyTerminalPaymentService(),
                new SpyChoiceProvider());

            await handler.SaveAsync(new TerminalDefinition { TenantId = 999, Provider = "agent" });

            Assert.AreEqual(7, administration.LastSaved!.TenantId,
                "Eine fremde Mandanten-Id aus der Anfrage darf niemals stehen bleiben.");

            var catalog = new SpyTerminalCatalog();
            handler = Handler(env, administration, catalog, new SpyTerminalPaymentService(), new SpyChoiceProvider());
            await handler.ApplyDeviceSettingsAsync(new TerminalDefinition { TenantId = 999, Provider = "agent", Route = "kasse-1" }, "{}");
            Assert.AreEqual(7, catalog.LastDraft!.TenantId,
                "Ueber den Mandanten wird der Agent aufgeloest - eine fremde Id darf nie bis dorthin kommen.");
        }

        // ------------------------------------------------------------------ Hauptschalter und Feature

        [TestMethod]
        public async Task LosingTheFeatureClosesTheDeviceManagement()
        {
            var env = HandlerTestEnvironment.WithPermissions(TerminalsHandler<BillingTestContext>.ManagePermission);
            env.FeatureGate.Enabled = false;
            var administration = new SpyTerminalAdministration();
            var handler = Handler(env, administration, new SpyTerminalCatalog(), new SpyTerminalPaymentService(),
                new SpyChoiceProvider());

            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(
                () => handler.SaveAsync(new TerminalDefinition()));
            Assert.AreEqual(PaymentErrorCodes.FeatureMissing, ex.Code,
                "Wem das Zahlungsmodul entzogen wurde, der soll auch keine Geraete mehr einrichten.");
            Assert.AreEqual(0, administration.Calls.Count);
        }

        [TestMethod]
        public void MasterSwitchClosesEvenTheCatalog()
        {
            var env = HandlerTestEnvironment.WithPermissions(ToolkitPermission.Sysadmin);
            env.Settings.Current.Enabled = false;
            env.DatabaseIsOffLimits = true;
            var catalog = new SpyTerminalCatalog();
            var handler = Handler(env, new SpyTerminalAdministration(), catalog, new SpyTerminalPaymentService(),
                new SpyChoiceProvider());

            var ex = Assert.ThrowsExactly<TenantPaymentException>(() => handler.GetProviders());
            Assert.AreEqual(PaymentErrorCodes.Disabled, ex.Code);
            Assert.AreEqual(0, catalog.Calls.Count);
        }

        // ------------------------------------------------------------------ Attrappen

        internal sealed class SpyTerminalAdministration : ITerminalAdministration
        {
            public List<string> Calls { get; } = new();

            public TerminalDefinition? LastSaved { get; private set; }

            public Task<IReadOnlyList<TerminalDefinition>> GetAsync(int tenantId, CancellationToken cancellationToken = default)
            {
                Calls.Add($"{nameof(GetAsync)}({tenantId})");
                return Task.FromResult<IReadOnlyList<TerminalDefinition>>(Array.Empty<TerminalDefinition>());
            }

            public Task<int> SaveAsync(TerminalDefinition definition, CancellationToken cancellationToken = default)
            {
                Calls.Add($"{nameof(SaveAsync)}({definition.TenantId})");
                LastSaved = definition;
                return Task.FromResult(1);
            }

            public Task SetEnabledAsync(int tenantId, int terminalId, bool enabled, CancellationToken cancellationToken = default)
            {
                Calls.Add($"{nameof(SetEnabledAsync)}({tenantId},{terminalId})");
                return Task.CompletedTask;
            }
        }

        internal sealed class SpyTerminalCatalog : ITerminalProviderCatalog
        {
            public List<string> Calls { get; } = new();

            public IReadOnlyList<TerminalProviderInfo> GetProviders()
            {
                Calls.Add(nameof(GetProviders));
                return new[] { new TerminalProviderInfo("agent", true) };
            }

            public IReadOnlyList<TerminalSettingDescriptor> DescribeSettings(string providerKey)
            {
                Calls.Add($"{nameof(DescribeSettings)}({providerKey})");
                return Array.Empty<TerminalSettingDescriptor>();
            }

            public TerminalDefinition? LastDraft { get; private set; }

            public Task<IReadOnlyList<SettingDescriptor>> DescribeDeviceSettingsAsync(TerminalDefinition draft,
                CancellationToken cancellationToken = default)
            {
                Calls.Add($"{nameof(DescribeDeviceSettingsAsync)}({draft.Provider})");
                LastDraft = draft;
                return Task.FromResult<IReadOnlyList<SettingDescriptor>>(Array.Empty<SettingDescriptor>());
            }

            public Task<string?> GetDeviceSettingsAsync(TerminalDefinition draft, CancellationToken cancellationToken = default)
            {
                Calls.Add($"{nameof(GetDeviceSettingsAsync)}({draft.Provider})");
                LastDraft = draft;
                return Task.FromResult<string?>(null);
            }

            public Task<IReadOnlyList<SettingChoice>> GetDeviceChoicesAsync(TerminalDefinition draft, string settingName,
                string? draftJson, CancellationToken cancellationToken = default)
            {
                Calls.Add($"{nameof(GetDeviceChoicesAsync)}({draft.Provider})");
                LastDraft = draft;
                return Task.FromResult<IReadOnlyList<SettingChoice>>(Array.Empty<SettingChoice>());
            }

            public Task<ApplySettingsResult> ApplyDeviceSettingsAsync(TerminalDefinition draft, string settingsJson,
                CancellationToken cancellationToken = default)
            {
                Calls.Add($"{nameof(ApplyDeviceSettingsAsync)}({draft.Provider})");
                LastDraft = draft;
                return Task.FromResult(new ApplySettingsResult { Success = true });
            }
        }

        internal sealed class SpyTerminalPaymentService : ITerminalPaymentService
        {
            public List<string> Calls { get; } = new();

            public Task<IReadOnlyList<TerminalInfo>> GetTerminalsAsync(int tenantId, bool includeDisabled = false,
                CancellationToken cancellationToken = default)
            {
                Calls.Add($"{nameof(GetTerminalsAsync)}({tenantId})");
                return Task.FromResult<IReadOnlyList<TerminalInfo>>(Array.Empty<TerminalInfo>());
            }

            public Task<TerminalStatus> GetTerminalStatusAsync(int tenantId, int terminalId,
                CancellationToken cancellationToken = default)
            {
                Calls.Add($"{nameof(GetTerminalStatusAsync)}({tenantId},{terminalId})");
                return Task.FromResult(new TerminalStatus());
            }

            public Task<TerminalSaleResult> StartPaymentAsync(TerminalSaleRequest request,
                CancellationToken cancellationToken = default)
            {
                Calls.Add(nameof(StartPaymentAsync));
                return Task.FromResult(new TerminalSaleResult());
            }

            public Task<TerminalSaleResult> GetPaymentAsync(int tenantSaleId, CancellationToken cancellationToken = default)
            {
                Calls.Add(nameof(GetPaymentAsync));
                return Task.FromResult(new TerminalSaleResult());
            }

            public Task<TerminalSaleResult> CancelPaymentAsync(int tenantSaleId, CancellationToken cancellationToken = default)
            {
                Calls.Add(nameof(CancelPaymentAsync));
                return Task.FromResult(new TerminalSaleResult());
            }

            // Die Selbstbeschreibung des Anbieters. Sie gehoert zum Vertrag, wird vom Handler aber nicht
            // benutzt - er geht ueber den Katalog. Zaehlt trotzdem mit: ein Aufruf hier waere ein Hinweis
            // darauf, dass ein Weg am Katalog vorbeilaeuft.
            public IReadOnlyList<TerminalSettingDescriptor> DescribeSettings()
            {
                Calls.Add(nameof(DescribeSettings));
                return Array.Empty<TerminalSettingDescriptor>();
            }

            public bool HasDeviceSettings => true;

            public Task<IReadOnlyList<SettingDescriptor>> DescribeDeviceSettingsAsync(TerminalDefinition draft,
                CancellationToken cancellationToken = default)
            {
                Calls.Add(nameof(DescribeDeviceSettingsAsync));
                return Task.FromResult<IReadOnlyList<SettingDescriptor>>(Array.Empty<SettingDescriptor>());
            }

            public Task<string?> GetDeviceSettingsAsync(TerminalDefinition draft, CancellationToken cancellationToken = default)
            {
                Calls.Add(nameof(GetDeviceSettingsAsync));
                return Task.FromResult<string?>(null);
            }

            public Task<IReadOnlyList<SettingChoice>> GetDeviceChoicesAsync(TerminalDefinition draft, string settingName,
                string? draftJson, CancellationToken cancellationToken = default)
            {
                Calls.Add(nameof(GetDeviceChoicesAsync));
                return Task.FromResult<IReadOnlyList<SettingChoice>>(Array.Empty<SettingChoice>());
            }

            public Task<ApplySettingsResult> ApplyDeviceSettingsAsync(TerminalDefinition draft, string settingsJson,
                CancellationToken cancellationToken = default)
            {
                Calls.Add(nameof(ApplyDeviceSettingsAsync));
                return Task.FromResult(new ApplySettingsResult());
            }
        }

        internal sealed class SpyChoiceProvider : ITerminalChoiceProvider
        {
            public List<string> Calls { get; } = new();

            public bool Handles(string source) => true;

            public Task<IReadOnlyList<SettingChoice>> GetChoicesAsync(string source, string? dependsOnValue,
                CancellationToken cancellationToken = default)
            {
                Calls.Add($"{nameof(GetChoicesAsync)}({source})");
                return Task.FromResult<IReadOnlyList<SettingChoice>>(
                    new[] { new SettingChoice { Value = "kasse-1", Label = "Kasse 1" } });
            }
        }
    }
}
