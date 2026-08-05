# Issue: Abgebrochener Checkout sperrt die Abo-Seite dauerhaft — Planliste kommt nicht zurück

**Status:** BEHOBEN im Toolkit (Future_10, noch nicht publiziert) — Host-Test offen
**Datum:** 2026-08-05
**Quelle:** MLMManager-Session (Konsument), beobachtet auf `/Account/Manage/Subscription`.
**Toolkit-Stand:** `5.0.0-PRE159`
**Betroffen:**
- `…Blazor.MudBlazor.BillingViews/Handlers/Impl/BillingHandler.cs:59-66` (Kern)
- `…Blazor.MudBlazor.BillingViews/Components/Subscription/Subscription.razor:25, 203-204`
- `…Billing.Stripe/Impl/StripeWebhookHandler.cs:42-66` (Nebenbefund)

> **Kurzfassung:** Wer den Stripe-Checkout startet und dann „zurück" geht, ohne zu bestellen, sieht
> danach nicht mehr die Planliste, sondern „Aktuelles Abonnement: **None**" mit „Abonnement
> verwalten". Es gibt **keinen Weg zurück zur Planliste** — der Tenant kann nichts mehr abonnieren.
> Ursache: `HasSubscription` prüft nur, ob *irgendeine* Zeile existiert, nicht ob ein gültiges Abo
> besteht.

## Beobachtung

1. `/Account/Manage/Subscription` zeigt die Planliste („MLM_Basic, 15.00 CHF/mo … ABONNIEREN").
2. „Abonnieren" → Weiterleitung zu Stripe.
3. Im Stripe-Checkout „zurück", **ohne** zu bestellen.
4. Die Seite zeigt jetzt dauerhaft:

   > **Aktuelles Abonnement** · Chip: `None` · Knopf: `ABONNEMENT VERWALTEN`

   Die Planliste ist weg — auch nach Neuladen, auch in einer neuen Sitzung.

## Ursache

Beim Start des Checkouts wird eine `TenantSubscriptions`-Zeile angelegt, die den Stripe-*Customer*
festhält, aber naturgemäss noch keine *Subscription*. Aus der MLM-Datenbank:

| Id | TenantId | Status | ProviderSubscriptionId | ProviderCustomerId | Created |
|---|---|---|---|---|---|
| 1 | 5 | 1 | `sub_1TseQH…` | `cus_UsPEq0…` | 2026-07-13 |
| 2 | 10 | 1 | `sub_1TuDUb…` | `cus_Uu1X5Q…` | 2026-07-17 |
| **1002** | **1** | **0** | **NULL** | `cus_V16Xw9…` | **2026-08-05** ← der abgebrochene Versuch |

`GetOverviewAsync` (`BillingHandler.cs:59-66`) macht daraus:

```csharp
var sub = await db.TenantSubscriptions...FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, ct);
if (sub == null)
{
    return vm;              // HasSubscription bleibt false → Planliste
}

vm.HasSubscription = true;  // ← sobald IRGENDEINE Zeile existiert
vm.Status = sub.Status;
```

Weder `Status` noch `ProviderSubscriptionId` gehen in die Entscheidung ein. `Subscription.razor:25`
verzweigt auf `overview.HasSubscription`, zeigt also die „Aktuelles Abonnement"-Karte — mit dem
Status, der wörtlich `None` heisst. Der einzige Knopf dort öffnet über `OpenPortalAsync` das
Stripe-Portal für einen Kunden, der gar keine Subscription hat.

**Es gibt keinen Ausweg in der Oberfläche.** Der Zustand ist dauerhaft, bis jemand die Zeile in der
Datenbank entfernt.

## Nebenbefund 1 — der `cancel`-Parameter wird gesetzt, aber nie gelesen

`Subscription.razor:203-204` baut beide Rückkehr-URLs:

```csharp
var success = $"{basePath}/Account/Manage/Subscription?checkout=success";
var cancel  = $"{basePath}/Account/Manage/Subscription?checkout=cancel";
```

Im ganzen Bereich gibt es aber keine Auswertung von `checkout` — weder in `OnInitializedAsync` noch
sonst. Die Rückkehr aus einem Abbruch ist für die Seite nicht von einem gewöhnlichen Aufruf zu
unterscheiden, obwohl die Information in der URL steht.

## Nebenbefund 2 — kein Stripe-Event räumt das je auf

Der Webhook-Handler (`StripeWebhookHandler.cs:42-66`) behandelt genau vier Ereignisse:

- `CustomerSubscriptionCreated`
- `CustomerSubscriptionUpdated`
- `CustomerSubscriptionDeleted`
- `InvoicePaymentFailed`

**Kein** `checkout.session.completed`, **kein** `checkout.session.expired`.

Das ist deshalb wichtig, weil beim Testen der Verdacht aufkam, der Zustand könnte daher rühren, dass
der Webhook-Proxy zum Zeitpunkt des Klicks nicht lief. **Das erklärt es nicht:** ein abgebrochener
Checkout erzeugt bei Stripe überhaupt keine Subscription, also auch kein `customer.subscription.*`.
Selbst bei laufendem Proxy wäre nie ein Ereignis eingetroffen, das die Zeile aufräumt. Der Befund ist
unabhängig von der Proxy-Frage reproduzierbar.

## Vorschläge

### 1. `HasSubscription` an Gültigkeit knüpfen (der eigentliche Fix)

Eine Zeile ohne `ProviderSubscriptionId` bzw. mit `Status == None` ist kein Abonnement, sondern die
Notiz „für diesen Tenant gibt es bereits einen Stripe-Kunden". Beides sollte auseinandergehalten
werden — der Kundenbezug ist wertvoll (er verhindert doppelte Stripe-Kunden beim nächsten Versuch)
und darf nur nicht als Abo gelten:

```csharp
vm.HasSubscription = sub.Status != SubscriptionStatus.None
                     && !string.IsNullOrEmpty(sub.ProviderSubscriptionId);
```

Alles Weitere (`Status`, `Items`, …) kann unverändert befüllt werden; die Seite zeigt dann wieder die
Planliste, während der Kundenbezug erhalten bleibt.

### 2. Den `checkout`-Parameter auswerten

Bei `checkout=cancel` eine kurze Rückmeldung („Vorgang abgebrochen — Sie können jederzeit einen Plan
wählen") statt kommentarlos dieselbe Seite. Bei `checkout=success` ist der Hinweis noch nützlicher:
dort trifft die Bestätigung erst per Webhook ein, die Seite kann also **direkt nach der Rückkehr noch
kein Abo zeigen** — ohne Hinweis wirkt das wie ein Fehlschlag. Ein „wird verarbeitet"-Zustand mit
Aktualisieren-Möglichkeit wäre hier die ehrlichere Anzeige.

### 3. Optional: `checkout.session.expired` behandeln

Damit liesse sich die verwaiste Zeile aufräumen, wenn Stripe die Session endgültig verfallen lässt.
Nicht dringend, wenn Vorschlag 1 umgesetzt ist — dann stört die Zeile nicht mehr —, aber es hielte
die Tabelle sauber.

## Sofortmassnahme für betroffene Installationen

Bis der Fix da ist, hilft nur das Entfernen der halbfertigen Zeile:

```sql
DELETE FROM TenantSubscriptions
WHERE ProviderSubscriptionId IS NULL AND Status = 0;
```

Danach zeigt die Seite wieder die Planliste. (In MLM ist das die Zeile mit `TenantSubscriptionId = 1002`.)

## Nachtrag der Toolkit-Session — was zusaetzlich gefunden und wie es umgesetzt wurde

Der Befund stimmt, aber der vorgeschlagene Fix greift zu kurz.

### Derselbe Fehler trifft ein zweites Mal — nach der Kuendigung

`Status != None && ProviderSubscriptionId != null` repariert nur den Abbruch. Bei `Canceled` behaelt die
Zeile ihre `ProviderSubscriptionId`, der Status ist nicht `None` — die Bedingung waere also erfuellt und
die Sackgasse entstuende erneut, nur eine Stufe spaeter: **wer einmal kuendigt, kann nie wieder
abonnieren.** Aufgefallen ist das erst beim Durchgehen der Statuswerte (`SubscriptionStatus` kennt `None`,
`Trialing`, `Active`, `PastDue`, `Canceled`, `Incomplete`).

### Anzeige und Guard mussten dasselbe Praedikat benutzen

`StripeCheckoutSessionFactory.cs:85` blockte einen zweiten Checkout bei `Active/Trialing/PastDue`. Bei
`Incomplete` (Abo besteht, Zahlung noch nicht bestaetigt) lief ein zweiter Checkout durch und haette genau
die parallele Subscription erzeugt, die der Kommentar dort verhindern will. Eine Anzeige, die anders
entscheidet als der Guard, ist immer eine der beiden Sackgassen — je nachdem, welche Seite strenger ist.

### Umgesetzt

- **`TenantSubscriptionExtensions.IsLive()`** neu in `EntityFramework.Billing/Models/` — ein Praedikat,
  eine Stelle: live sind `Active/Trialing/PastDue/Incomplete` **mit** gesetzter `ProviderSubscriptionId`.
  `None` (abgebrochener Checkout) und `Canceled` sind es nicht.
- `BillingHandler.GetOverviewAsync` setzt `HasSubscription = sub.IsLive()`.
- `StripeCheckoutSessionFactory` benutzt dasselbe `IsLive()` statt seiner eigenen Statusliste.
- `Subscription.razor` wertet `checkout` aus: bei `cancel` ein Hinweis ueber der Planliste, bei `success`
  ein Wartezustand („wird verarbeitet") mit Aktualisieren-Knopf und einem Ausweg zur Planliste — die
  Bestaetigung kommt per Webhook, direkt nach der Rueckkehr ist noch kein Abo da.
- Bei `Canceled` traegt die Planliste einen Hinweis auf das gekuendigte Abo, sonst wirkt sie wie
  Datenverlust.
- Die Stub-Zeile bleibt: sie ist Absicht und idempotent (`StripeCheckoutSessionFactory.cs:138-146`, ein
  Stripe-Kunde pro Tenant), wiederholte Abbrueche vervielfachen sie nicht.

`checkout.session.expired` (Vorschlag 3) wurde **nicht** umgesetzt — nach dem Praedikat-Fix stoert die
Zeile nicht mehr, und der Kundenbezug darin ist wertvoll.

**Nicht geprueft:** der Ablauf am Host. Der Abbruch-Fall und der Kuendigungs-Fall sind beide nur aus dem
Code hergeleitet; die urspruengliche Beobachtung stammt aus MLM, die Kuendigungs-Variante nicht.

## Verifikationsstand

- Der Datenbankzustand ist ausgelesen, nicht vermutet — die Tabelle oben stammt aus der
  MLM-Datenbank.
- Der Code-Pfad (`GetOverviewAsync` → `HasSubscription` → `Subscription.razor:25`) ist gelesen.
- Dass kein Checkout-Ereignis behandelt wird, ist am `switch` in `StripeWebhookHandler.cs:42` geprüft.
- **Nicht geprüft:** ob `OpenPortalAsync` für einen Kunden ohne Subscription einen brauchbaren
  Portal-Link liefert oder scheitert — der Knopf ist in diesem Zustand ohnehin sinnlos.
- **Nicht geprüft:** ob der Ablauf mit laufendem Webhook-Proxy und *erfolgreichem* Checkout sauber
  durchläuft; hier ging es nur um den Abbruch.
