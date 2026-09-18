using System;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Test
{
    /// <summary>
    /// Weist nach, dass <see cref="PaymentsHandler{TContext,TTenant}"/> seine Rechte SELBST prueft.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Handler liegt im DI und ist damit von jeder Komponente aus erreichbar. Eine Pruefung, die nur in
    /// der Maske steht, haelt genau so lange, bis jemand einen zweiten Aufrufer schreibt — diese Tests sind
    /// die Stelle, an der das auffaellt.
    /// </para>
    /// <para>
    /// Jeder Verweigerungs-Test laeuft mit gesperrter Datenbank: kommt die Rechte-Absage zurueck und nicht
    /// die Sperrmeldung der Kontext-Fabrik, stand die Pruefung nachweislich VOR dem ersten Datenzugriff.
    /// </para>
    /// </remarks>
    [TestClass]
    public class PaymentsHandlerPermissionTests
    {
        private static PaymentsHandler<BillingTestContext, Tenant> Handler(HandlerTestEnvironment env)
            => new(env.DbFactory(), env.Services, env.AccountService, env.SaleService, env.PaymentSettings(),
                env.FeatureGates());

        /// <summary>
        /// Ein angemeldeter Benutzer ohne jedes Zahlungsrecht. Nicht ein anonymer: der wuerde schon eine Stufe
        /// frueher abgewiesen und liesse offen, ob die Methode selbst prueft.
        /// </summary>
        private static HandlerTestEnvironment WithoutRights()
            => HandlerTestEnvironment.WithPermissions("SomethingElse");

        private static async Task AssertRefusedAsync(Func<Task> operation, string because)
        {
            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(operation, because);
            Assert.AreEqual(PaymentErrorCodes.NotPermitted, ex.Code,
                "Eine Rechte-Absage muss als solche erkennbar sein - sonst sieht sie in der Maske aus wie eine Anbieterstoerung.");
        }

        // ------------------------------------------------------------------ ohne Recht: alles zu

        [TestMethod]
        public async Task EveryTenantPathRefusesWithoutPermission()
        {
            var env = WithoutRights();
            env.DatabaseIsOffLimits = true;
            var handler = Handler(env);

            await AssertRefusedAsync(() => handler.GetAccountAsync(), "Konto lesen");
            await AssertRefusedAsync(() => handler.RefreshAccountAsync(), "Konto neu lesen");
            await AssertRefusedAsync(() => handler.OpenDashboardAsync(), "Anbieter-Oberflaeche oeffnen");
            await AssertRefusedAsync(() => handler.StartOnboardingAsync("https://a.invalid", "https://b.invalid", "CH", null),
                "Konto einrichten");
            await AssertRefusedAsync(() => handler.GetSalesAsync(null, null, null), "Verkaeufe lesen");
            await AssertRefusedAsync(() => handler.RefundAsync(1, null, null, null), "erstatten");

            Assert.AreEqual(0, env.AccountService.Calls.Count, "Die Dienstschicht wurde trotz fehlendem Recht angefasst.");
            Assert.AreEqual(0, env.SaleService.Calls.Count, "Die Dienstschicht wurde trotz fehlendem Recht angefasst.");
        }

        [TestMethod]
        public async Task PlatformPathsRefuseWithoutPermission()
        {
            var env = WithoutRights();
            env.DatabaseIsOffLimits = true;
            var handler = Handler(env);

            await AssertRefusedAsync(() => handler.GetAdminOverviewAsync(null, null), "Plattformsicht");
            await AssertRefusedAsync(() => handler.RefreshAccountAsync(42), "fremdes Konto neu lesen");

            Assert.AreEqual(0, env.AccountService.Calls.Count, "Die Dienstschicht wurde trotz fehlendem Recht angefasst.");
        }

        // ------------------------------------------------------------------ die Rechte sind unterscheidbar

        [TestMethod]
        public async Task ViewPermissionDoesNotAllowManaging()
        {
            var env = HandlerTestEnvironment.WithPermissions(PaymentsHandler<BillingTestContext, Tenant>.ViewPermission);
            var handler = Handler(env);

            // Lesen ist erlaubt und geht bis zur Dienstschicht durch ...
            await handler.GetAccountAsync();
            Assert.AreEqual(1, env.AccountService.Calls.Count, "Mit Leserecht muss das Konto gelesen werden koennen.");

            // ... einrichten, erstatten und die Plattformsicht aber nicht.
            await AssertRefusedAsync(() => handler.StartOnboardingAsync("https://a.invalid", "https://b.invalid", "CH", null),
                "Leserecht ist kein Einrichtungsrecht");
            await AssertRefusedAsync(() => handler.RefundAsync(1, null, null, null), "Leserecht ist kein Erstattungsrecht");
            await AssertRefusedAsync(() => handler.GetAdminOverviewAsync(null, null), "Leserecht ist kein Plattformrecht");

            Assert.AreEqual(1, env.AccountService.Calls.Count, "Nach der ersten Abfrage darf nichts mehr durchgekommen sein.");
        }

        /// <summary>
        /// Der Mandanten-Administrator ist NICHT der Plattform-Administrator. Genau diese beiden Wege lesen
        /// bzw. schreiben ueber alle Mandanten hinweg.
        /// </summary>
        [TestMethod]
        public async Task TenantAdminIsNotPlatformAdmin()
        {
            var env = HandlerTestEnvironment.WithPermissions(ToolkitPermission.TenantAdmin);
            env.DatabaseIsOffLimits = true;
            var handler = Handler(env);

            await AssertRefusedAsync(() => handler.GetAdminOverviewAsync(null, null),
                "Ein Mandanten-Admin darf nicht die Umsaetze aller Mandanten sehen.");
            await AssertRefusedAsync(() => handler.RefreshAccountAsync(42),
                "Ein Mandanten-Admin darf nicht das Konto eines fremden Mandanten anfassen.");

            Assert.AreEqual(0, env.AccountService.Calls.Count, "Die Dienstschicht wurde trotz fehlendem Recht angefasst.");
        }

        /// <summary>
        /// Die Ueberladung mit der Mandanten-Id ist der heikelste Weg des Handlers: sie nimmt den Mandanten vom
        /// AUFRUFER statt aus dem Sicherheitsbereich. Die Zugehoerigkeitspruefung der Dienstschicht laeuft
        /// gegen genau diese uebergebene Id und ist hier deshalb wirkungslos.
        /// </summary>
        [TestMethod]
        public async Task ForeignTenantRefreshNeedsThePlatformPermission()
        {
            var env = HandlerTestEnvironment.WithPermissions(PaymentsHandler<BillingTestContext, Tenant>.AdminPermission);
            var handler = Handler(env);

            await handler.RefreshAccountAsync(42);

            Assert.AreEqual(1, env.AccountService.Calls.Count);
            StringAssert.Contains(env.AccountService.Calls[0], "(42)",
                "Mit dem Plattformrecht muss der Weg offenstehen - sonst prueft der Test nur sich selbst.");
        }

        // ------------------------------------------------------------------ Hauptschalter und Feature

        [TestMethod]
        public async Task ReadingRefusesWhenTheTenantLostTheFeature()
        {
            var env = HandlerTestEnvironment.WithPermissions(PaymentsHandler<BillingTestContext, Tenant>.ViewPermission);
            env.FeatureGate.Enabled = false;
            var handler = Handler(env);

            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => handler.GetAccountAsync());
            Assert.AreEqual(PaymentErrorCodes.FeatureMissing, ex.Code,
                "Wem das Zahlungsmodul entzogen wurde, der soll auch dessen Daten nicht mehr sehen.");
            Assert.AreEqual(0, env.AccountService.Calls.Count);
        }

        /// <summary>
        /// Der Plattform-Weg darf NICHT am Feature des aktuellen Mandanten haengen: ein Administrator arbeitet
        /// womoeglich aus einem Mandanten, der das Zahlungsmodul nie gekauft hat.
        /// </summary>
        [TestMethod]
        public async Task PlatformViewDoesNotDependOnTheCurrentTenantsFeature()
        {
            var env = HandlerTestEnvironment.WithPermissions(PaymentsHandler<BillingTestContext, Tenant>.AdminPermission);
            env.FeatureGate.Enabled = false;

            var result = await Handler(env).GetAdminOverviewAsync(null, null);

            Assert.AreEqual(0, result.Count, "Ohne Konten ist die Liste leer - entscheidend ist, dass sie nicht verweigert wurde.");
            Assert.AreEqual(0, env.FeatureGate.Queries, "Der Plattform-Weg darf das Mandanten-Feature gar nicht erst befragen.");
        }

        [TestMethod]
        public async Task MasterSwitchRefusesEvenWithEveryPermission()
        {
            var env = HandlerTestEnvironment.WithPermissions(ToolkitPermission.Sysadmin);
            env.Settings.Current.Enabled = false;
            env.DatabaseIsOffLimits = true;
            var handler = Handler(env);

            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => handler.GetAccountAsync());
            Assert.AreEqual(PaymentErrorCodes.Disabled, ex.Code);
        }
    }
}
