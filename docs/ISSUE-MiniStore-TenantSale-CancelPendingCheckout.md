# ISSUE: Einen offenen Verkauf (gehostete Zahlseite) abbrechen können — `ITenantSaleService.CancelSaleAsync`

**Status:** umgesetzt für PRE260 — `ITenantSaleService.CancelSaleAsync`, `TenantSaleServiceBase.CancelCheckoutAsync` (Stripe: expire, Payrexx: Gateway löschen, wallee: nur feststellen, siehe Leitfaden §76); dazu: späte Zahlung auf Failed/Expired/Canceled wird gebucht, Absage einer ersetzten Seite wird übergangen, Stripe-Idempotenz-Schlüssel je Anlauf · **Datum:** 2026-10-14 · **Version:** 5.0.0-PRE259 · **Quelle:** MiniStore, Selbstbedienungs-Station
(Übergabe eines Handy-Einkaufs an eine Kasse)

## Der Fall

Ein Kunde kauft im Self-Checkout am Handy ein und tippt auf „Jetzt bezahlen“. Der Host ruft
`ITenantSaleService.CreateSaleAsync` (Referenz `order-{Id}`), und der Kunde landet auf der Zahlseite von
Stripe. Dort bricht er ab, entweder über den Zurück-Link (`CancelUrl`) oder indem er die Seite verlässt.
Danach will er am Handy weitermachen oder **an einer Kasse bezahlen**. In MiniStore heisst das: den
Einkauf an eine Selbstbedienungs-Station übergeben und dort mit Karte am Terminal zahlen.

Der Verkauf steht danach weiter auf `Pending`, und die Stripe-Checkout-Session **bleibt offen**:

- Sie verfällt erst nach `CheckoutExpiryMinutes`. Das sind mindestens 30 Minuten, weil der Wert auf
  30–1440 begrenzt ist, und in MiniStore 60.
- Wer den Link noch hat (Browserverlauf, zweites Gerät, Zurück-Taste), **kann auf ihr weiterhin
  bezahlen**.

Für den Host gibt es keinen Weg, das zu verhindern. `ITenantSaleService` kennt nur `CreateSaleAsync`,
`GetSaleAsync`, `FindByReferenceAsync` und `RefundSaleAsync`. Der Status `Canceled` ist dokumentiert als
„The tenant or the host cancelled the sale before payment“, aber es gibt keinen Aufruf, der ihn setzt.

Die Folge in MiniStore: Die Station **muss die Übergabe ablehnen**, solange zu `order-{Id}` ein
`Pending`-Verkauf besteht. Sonst zahlt der Kunde an der Kasse, und eine Minute später erledigt der
alte Tab die Stripe-Zahlung. Das Geld wäre dann zweimal da und müsste von Hand erstattet werden. Der
Kunde steht also bis zu einer Stunde vor einer Kasse, die seinen Einkauf nicht annimmt.

Dasselbe Loch hat der reine Online-Fall. Hat der Kunde abgebrochen und den Korb am Handy geändert,
kann die alte Session mit dem **alten Betrag** noch bezahlt werden.

## Betroffene Stellen

| Stelle | Was dort steht |
|---|---|
| `EntityFramework.Billing/Abstractions/TenantPaymentAbstractions.cs:93–113` | `ITenantSaleService`: kein Abbruch |
| `EntityFramework.Billing/Models/Payments/TenantSaleStatus.cs:18–19` | `Canceled = 3`, „cancelled … before payment“, nirgends gesetzt |
| `EntityFramework.Billing/Payments/TenantSaleServiceBase.cs:287–291` | Ein `Pending`-Verkauf mit Session wird beim nächsten `CreateSaleAsync` unverändert zurückgegeben. Die Gültigkeit ist auf mindestens 30 Minuten begrenzt |
| `Billing.Stripe/Payments/Impl/TenantSaleService.cs:89–99` | Die Session entsteht mit `ExpiresAt` auf dem verbundenen Konto (`StripePaymentsRuntime.ForAccount`) |
| `Billing.Stripe/Payments/Impl/StripeConnectWebhookHandler.cs:117–121` | `checkout.session.expired` setzt den Verkauf auf `Expired`. Der Rückweg für eine abgelaufene Session ist also schon da |

## Vorschlag

Eine Methode am Vertrag, umgesetzt in der Basisklasse mit einem anbieterspezifischen Schritt:

```csharp
/// <summary>
/// Bricht einen noch nicht bezahlten Verkauf ab: die gehostete Zahlseite wird beim Anbieter ungültig
/// gemacht, der Verkauf geht auf Canceled. Bereits bezahlt → Ergebnis mit Status Paid, nichts geändert
/// (der Aufrufer muss das sehen: dann ist das Geld da).
/// </summary>
Task<SaleResult> CancelSaleAsync(int tenantSaleId, CancellationToken cancellationToken = default);
```

- **Basis (`TenantSaleServiceBase`):**
  - Ist der Verkauf `Pending`, ruft sie den anbieterspezifischen Abbruch auf und setzt danach
    `Canceled`. `CheckoutUrl` wird geleert, damit `ToResult` keine Zahlseite mehr anbietet.
  - Ist er schon `Paid`, `Failed`, `Expired` oder `Canceled`, wird nichts geändert und der Stand
    zurückgegeben.
- **Stripe:** `SessionService.ExpireAsync(sale.ProviderSessionId, …)` auf dem verbundenen Konto
  (`StripePaymentsRuntime.ForAccount`). Stripe lässt das nur für offene Sessions zu.
  - Ist die Session in der Zwischenzeit bezahlt worden, antwortet Stripe mit einem Fehler. Dann den
    Verkauf frisch lesen (`GetSaleAsync`) bzw. auf den Webhook warten und **`Paid` melden**, nicht
    `Canceled`. Das ist der eigentliche Wert der Methode: Der Aufrufer weiss danach sicher, ob noch
    bezahlt werden kann.
  - Der anschliessende Webhook `checkout.session.expired` trifft einen Verkauf, der schon `Canceled`
    ist, und darf ihn nicht auf `Expired` umschreiben, oder es ist egal, welcher der beiden Zustände
    steht. Das sollte im Handler entschieden werden.
- **Idempotenz:** Ein zweiter Aufruf auf einen `Canceled`-Verkauf tut nichts. Ein späteres
  `CreateSaleAsync` mit derselben Referenz sollte eine **neue** Session anlegen dürfen, wie heute
  nach `Expired` mit dem nächsten `attempt`. Sonst sperrt ein Abbruch die Referenz für immer.

## Wie MiniStore es benutzen würde

1. **Übergabe an die Station** (`TillHandoffService.ClaimAsync`): Statt bei `Pending` abzulehnen,
   `CancelSaleAsync` rufen. Kommt `Canceled` zurück, wird übergeben. Kommt `Paid` zurück, wird nicht
   übergeben, und der Auftrag wird über den normalen Weg gebucht.
2. **Rückkehr mit `?zahlung=abbruch`** (`MobileCheckout`): die Session gleich ungültig machen, damit
   ein später geänderter Korb nicht mit dem alten Betrag bezahlt werden kann.

## Umgehung heute

MiniStore lehnt die Übergabe ab, solange ein `Pending`-Verkauf zu `order-{Id}` besteht
(`Outcome.PaymentInProgress`). Der Kunde sieht dann: „Für diesen Einkauf läuft bereits eine
Online-Zahlung. Bitte dort abschliessen oder etwas später nochmals versuchen.“ Das ist sicher, aber
unfreundlich. Den Stripe-Aufruf selbst am Toolkit vorbei abzusetzen, wäre möglich, aber dann liefen
Session-Verwaltung und Status zweimal. Darauf verzichten wir bewusst.
