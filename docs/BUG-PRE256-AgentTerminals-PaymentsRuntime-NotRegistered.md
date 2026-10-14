# BUG PRE256: `AddAgentTerminals` — `PaymentsRuntime` wird nirgends registriert

**Status:** behoben auf `Future_10`, noch nicht publiziert — `AgentTerminalPaymentService` baut sein `PaymentsRuntime` selbst aus `IGlobalSettings<TenantPaymentsOptions>` und `IEnumerable<IPaymentFeatureGate>` (wie Payrexx/Stripe). Test `TerminalRegistrationTests.AddAgentTerminals_BuildsWithValidation` baut die Verdrahtung mit `ValidateOnBuild`. Die Umgehung im MiniStore kann danach entfallen · **Datum:** 2026-10-14 · **Quelle:** MiniStore, Umstellung auf die Schalter aus §74

## Der Fall

Mit `BillingEF:ActivateAgentTerminals = true` (und `TerminalsContextType` gesetzt) scheitert der Host
schon beim Bauen des Dienstanbieters:

```
System.AggregateException: Some services are not able to be constructed (Error while validating the
service descriptor 'ServiceType: ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments.
AgentTerminalPaymentService`1[MiniStore.Infrastructure.Identity.ApplicationDbContext] Lifetime: Scoped
...': Unable to resolve service for type
'ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments.PaymentsRuntime' while attempting to
activate 'ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments.AgentTerminalPaymentService`1[...]'.)
   at Microsoft.AspNetCore.Builder.WebApplicationBuilder.Build()
```

Das ist kein Fehler des neuen Schalters — er ruft nur `AddAgentTerminals<TContext>`. Derselbe Absturz
tritt beim direkten Aufruf im Code auf. Er besteht seit `c0c188b3` (Achse C eingeführt); nur bisher hat
offenbar kein Host mit `ValidateOnBuild` den Agent-Weg tatsächlich gestartet.

## Die betroffenen Stellen

| Stelle | Was dort steht |
|---|---|
| `EntityFramework.Billing/Payments/AgentTerminalPaymentService.cs:41` | Konstruktor nimmt `PaymentsRuntime runtime` **aus der DI** |
| `EntityFramework.Billing/Extensions/TerminalDependencyInjectionExtensions.cs:53` (`AddAgentTerminals`) | registriert `AgentTerminalPaymentService<TContext>`, aber kein `PaymentsRuntime` |
| `Billing.Stripe/Payments/Impl/StripeTerminalPaymentService.cs:54` | baut sich sein Runtime **selbst**: `new StripePaymentsRuntime(settings, featureGates.FirstOrDefault())` |
| `Billing.Payrexx/Impl/PayrexxTerminalPaymentService.cs:50` | ebenso: `new PaymentsRuntime(settings, featureGates.FirstOrDefault())` |
| `BillingViews/Handlers/Impl/TerminalsHandler.cs:62`, `PaymentsHandler.cs:62` | ebenso |

Der Agent-Weg ist also der einzige, der vom Muster abweicht.

## Vorschlag

`AgentTerminalPaymentService` wie die Geschwister bauen lassen:

```csharp
public AgentTerminalPaymentService(IDbContextFactory<TContext> dbFactory,
    IGlobalSettings<TenantPaymentsOptions> settings, IEnumerable<IPaymentFeatureGate> featureGates,
    TenantSaleWebhookSink<TContext> sink, ITerminalAgentLocator locator, IAgentConnector connector)
    : base(dbFactory, new PaymentsRuntime(settings, featureGates.FirstOrDefault()), sink)
```

`IEnumerable<IPaymentFeatureGate>` statt eines Pflicht-Parameters hält das Fail-closed-Verhalten
(kein Tor → `FeatureMissing`) bei, statt beim Auflösen zu scheitern.

Alternativ in `AddAgentTerminals` ein `TryAddScoped<PaymentsRuntime>(…)` — dann aber mit derselben
Fabrik, nicht über den Konstruktor (der `IPaymentFeatureGate?` ist für die DI ein Pflicht-Parameter).

Ein Test, der `AddAgentTerminals` auf einer `ServiceCollection` mit `ValidateOnBuild = true` baut, hätte
das gefangen.

## Umgehung (MiniStore)

In `AddBillingAndPayments`:

```csharp
services.TryAddScoped(sp => new PaymentsRuntime(
    sp.GetRequiredService<IGlobalSettings<TenantPaymentsOptions>>(),
    sp.GetServices<IPaymentFeatureGate>().FirstOrDefault()));
```

Verschwindet, sobald das Toolkit es selbst löst.
