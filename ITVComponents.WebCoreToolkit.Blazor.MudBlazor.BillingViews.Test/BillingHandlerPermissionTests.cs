using System;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers.Impl;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.ViewModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Test
{
    /// <summary>
    /// Weist nach, dass <see cref="BillingHandler{TContext}"/> seine Rechte selbst prueft — und dass die
    /// beiden Klammern wirklich getrennt sind.
    /// </summary>
    /// <remarks>
    /// Die Trennung ist hier die eigentliche Aussage: der Kunden-Mandant ist <b>nie</b> Sysadmin und muss
    /// trotzdem Plaene lesen und abonnieren koennen. Eine Absicherung, die das Autoring und die
    /// Selbstbedienung in denselben Topf wirft, macht das Produkt unverkaeuflich, ohne dass ein Build es
    /// merkt.
    /// </remarks>
    [TestClass]
    public class BillingHandlerPermissionTests
    {
        private static readonly ClaimsPrincipal AnyUser = new();

        private static BillingHandler<BillingTestContext> Handler(HandlerTestEnvironment env)
            => new(env.DbFactory(), env.Services, env.Checkout, env.Portal, env.PlanSynchronizer);

        private static async Task AssertRefusedAsync(Func<Task> operation, string because)
            => await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(operation, because);

        // ------------------------------------------------------------------ ohne Recht: alles zu

        [TestMethod]
        public async Task EveryPathRefusesWithoutPermission()
        {
            var env = HandlerTestEnvironment.WithPermissions("SomethingElse");
            env.DatabaseIsOffLimits = true;
            var handler = Handler(env);

            await AssertRefusedAsync(() => handler.GetOverviewAsync(AnyUser), "eigenes Abo lesen");
            await AssertRefusedAsync(() => handler.GetActivePlansAsync(), "Plaene lesen");
            await AssertRefusedAsync(() => handler.GetActiveAddOnsAsync(), "Zusaetze lesen");
            await AssertRefusedAsync(() => handler.StartCheckoutAsync(AnyUser, 1, Array.Empty<int>(), "https://a.invalid", "https://b.invalid"),
                "abonnieren");
            await AssertRefusedAsync(() => handler.OpenPortalAsync(AnyUser, "https://a.invalid"), "Portal oeffnen");
            await AssertRefusedAsync(() => handler.GetFeatureCatalogAsync(), "Feature-Katalog lesen");
            await AssertRefusedAsync(() => handler.GetAllPlansAsync(), "alle Plaene lesen");
            await AssertRefusedAsync(() => handler.GetAllAddOnsAsync(), "alle Zusaetze lesen");
            await AssertRefusedAsync(() => handler.SavePlanAsync(new PlanViewModel()), "Plan schreiben");
            await AssertRefusedAsync(() => handler.SaveAddOnAsync(new AddOnViewModel()), "Zusatz schreiben");
            await AssertRefusedAsync(() => handler.PushPlanAsync(1), "Plan zum Anbieter schieben");
            await AssertRefusedAsync(() => handler.PushAddOnAsync(1), "Zusatz zum Anbieter schieben");
            await AssertRefusedAsync(() => handler.GetAllSubscriptionsAsync(), "alle Abos lesen");

            Assert.AreEqual(0, env.Checkout.Calls.Count, "Die Dienstschicht wurde trotz fehlendem Recht angefasst.");
            Assert.AreEqual(0, env.Portal.Calls.Count, "Die Dienstschicht wurde trotz fehlendem Recht angefasst.");
            Assert.AreEqual(0, env.PlanSynchronizer.Calls.Count, "Die Dienstschicht wurde trotz fehlendem Recht angefasst.");
        }

        // ------------------------------------------------------------------ die Kunden-Klammer

        /// <summary>
        /// Der Fall, der die Absicherung tragen muss: ein Kunden-Benutzer mit <c>ManageSubscription</c>, ohne
        /// jedes Plattformrecht. Er MUSS den Katalog lesen und buchen koennen.
        /// </summary>
        [TestMethod]
        public async Task CustomerTenantCanReadPlansAndSubscribe()
        {
            var env = HandlerTestEnvironment.WithPermissions(BillingHandler<BillingTestContext>.ManageSubscriptionPermission);
            var handler = Handler(env);

            await handler.GetOverviewAsync(AnyUser);
            var plans = await handler.GetActivePlansAsync();
            await handler.GetActiveAddOnsAsync();
            var url = await handler.StartCheckoutAsync(AnyUser, 7, Array.Empty<int>(), "https://a.invalid", "https://b.invalid");
            await handler.OpenPortalAsync(AnyUser, "https://a.invalid");

            Assert.AreEqual(0, plans.Count, "Leere Ablage - entscheidend ist, dass nicht verweigert wurde.");
            Assert.IsFalse(string.IsNullOrEmpty(url));
            Assert.AreEqual(1, env.Checkout.Calls.Count, "Der Kunde muss abonnieren koennen.");
            Assert.AreEqual(1, env.Portal.Calls.Count, "Der Kunde muss sein Portal oeffnen koennen.");
        }

        /// <summary>
        /// Dieselbe Klammer darf aber nicht das Autoring oeffnen: Plaene tragen PREISE, und
        /// <c>GetAllSubscriptionsAsync</c> liest ueber alle Mandanten hinweg.
        /// </summary>
        [TestMethod]
        public async Task ManagingASubscriptionIsNotAuthoringPlans()
        {
            var env = HandlerTestEnvironment.WithPermissions(BillingHandler<BillingTestContext>.ManageSubscriptionPermission);
            var handler = Handler(env);

            await AssertRefusedAsync(() => handler.SavePlanAsync(new PlanViewModel()), "Preise schreiben");
            await AssertRefusedAsync(() => handler.PushPlanAsync(1), "Preise zum Anbieter schieben");
            await AssertRefusedAsync(() => handler.GetAllPlansAsync(), "auch zurueckgezogene Plaene sind Autoring-Daten");
            await AssertRefusedAsync(() => handler.GetFeatureCatalogAsync(), "Feature-Katalog ist Autoring-Daten");
            await AssertRefusedAsync(() => handler.GetAllSubscriptionsAsync(), "Abos ALLER Mandanten");

            Assert.AreEqual(0, env.PlanSynchronizer.Calls.Count);
        }

        /// <summary>
        /// Auch ein Mandanten-Administrator bleibt beim Autoring draussen: er verwaltet seinen Mandanten, nicht
        /// den Katalog der Plattform.
        /// </summary>
        [TestMethod]
        public async Task TenantAdminMayNotAuthorPlans()
        {
            var env = HandlerTestEnvironment.WithPermissions(ToolkitPermission.TenantAdmin);
            var handler = Handler(env);

            await AssertRefusedAsync(() => handler.SavePlanAsync(new PlanViewModel()), "Preise schreiben");
            await AssertRefusedAsync(() => handler.GetAllSubscriptionsAsync(), "Abos ALLER Mandanten");

            // Die Selbstbedienung steht ihm dagegen offen.
            await handler.GetActivePlansAsync();
        }

        // ------------------------------------------------------------------ die Plattform-Klammer

        [TestMethod]
        public async Task PlatformAdminMayAuthorAndPush()
        {
            var env = HandlerTestEnvironment.WithPermissions(ToolkitPermission.Sysadmin);
            var handler = Handler(env);

            await handler.GetAllPlansAsync();
            await handler.GetAllAddOnsAsync();
            await handler.GetAllSubscriptionsAsync();
            await handler.PushPlanAsync(3);
            await handler.PushAddOnAsync(4);

            Assert.AreEqual(2, env.PlanSynchronizer.Calls.Count, "Mit dem Plattformrecht muss der Weg offenstehen.");
            StringAssert.Contains(env.PlanSynchronizer.Calls[0], "(3)");
            StringAssert.Contains(env.PlanSynchronizer.Calls[1], "(4)");
        }
    }
}
