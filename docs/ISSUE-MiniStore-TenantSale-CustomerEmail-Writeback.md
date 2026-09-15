# Issue: Die vom Kunden auf der Zahlseite eingegebene E-Mail kommt nicht zurück (Anstoss aus MiniStore)

**Status:** ERLEDIGT — umgesetzt im Toolkit (siehe „Auflösung" am Ende)
**Datum:** 2026-09-15
**Quelle:** MiniStore-Session (Konsument). Der Self-Checkout-Kunde soll seinen Bon per Mail bekommen und
tippt dafür heute seine Adresse **ein zweites Mal** ein — er hat sie eine Minute vorher schon bei Stripe
angegeben.
**Toolkit-Stand:** `5.0.0-PRE238`. Alle Aussagen unten sind am Quelltext verifiziert.

## Der Fall

`TenantSale.CustomerEmail` ist heute eine **Einbahnstrasse zum Anbieter hin**:

```csharp
// …Billing.Stripe/Payments/Abstractions/PaymentAbstractions.cs:132
/// <summary>Optional, for the receipt only. Creates no customer profile.</summary>
public string? CustomerEmail { get; set; }

// …Billing.Stripe/Payments/Impl/TenantSaleService.cs:88   — vom Host in die Zeile
CustomerEmail = Trim(request.CustomerEmail, 256),

// …Billing.Stripe/Payments/Impl/TenantSaleService.cs:311  — aus der Zeile an Stripe
CustomerEmail = string.IsNullOrWhiteSpace(sale.CustomerEmail) ? null : sale.CustomerEmail,
```

Der Wert belegt also Stripes Formular vor. **Was der Kunde dort tatsächlich einträgt, liest niemand
zurück.** `StripeConnectWebhookHandler.MarkPaidAsync` holt aus der abgeschlossenen Session nur, was zum
Auffinden und Verbuchen nötig ist:

```csharp
// …Billing.Stripe/Payments/Impl/StripeConnectWebhookHandler.cs:336
sale.ProviderPaymentIntentId ??= current.PaymentIntentId;
sale.ProviderChargeId       ??= await ResolveChargeAsync(current.PaymentIntentId, …);
```

Empirisch bestätigt: in einer MiniStore-Installation mit zehn Verkäufen — davon mehrere bezahlt — steht
`CustomerEmail` ausnahmslos auf `NULL`.

**Stripe hat die Adresse.** Sie steht in der abgeschlossenen Session unter `customer_details.email`, und
`current` ist an dieser Stelle bereits die **frisch nachgelesene** Session (`SessionService.GetAsync`
einige Zeilen darüber). Es bräuchte also weder einen zusätzlichen API-Aufruf noch eine neue Abfrage.

## Betroffene Stellen

| Datei | Was dort steht |
|---|---|
| `…Billing.Stripe/Payments/Impl/StripeConnectWebhookHandler.cs:336 f.` | Die zwei bestehenden `??=`-Zeilen — hier gehört die dritte hin |
| `…EntityFramework.Billing/Models/Payments/TenantSale.cs:74` | `CustomerEmail`, heute nur vom Host beschrieben |
| `…EntityFramework.Billing/Options/StripePaymentsOptions.cs` | Ort für den Schalter unten |

## Vorschlag

Eine Zeile im selben Muster wie die beiden daneben:

```csharp
sale.CustomerEmail ??= Trim(current.CustomerDetails?.Email, 256);
```

**`??=` und nicht Zuweisung:** hat der Host bereits eine Adresse gesetzt, ist das seine Angabe — daran
hängt womöglich eine Zuordnung, die eine Korrektur auf der Zahlseite nicht kennen kann. Gefüllt wird nur,
was leer ist.

### Bitte mit Schalter, Vorgabe *aus*

`CaptureCustomerEmail` (oder ähnlich) in `StripePaymentsOptions`, **Vorgabe `false`**.

Das ist keine Vorsicht um ihrer selbst willen: die Adresse hat der Kunde *Stripe* gegeben, damit **Stripe**
ihm eine Zahlungsbestätigung schickt. Sie in die Datenbank des Hosts zu übernehmen, ist ein anderer Zweck —
und ein Host, der sie nicht braucht, soll sie nicht bekommen. Wer sie will, schaltet sie ein und trifft
diese Entscheidung damit bewusst. (Der Schalter kostet uns nichts: wir schalten ihn ein.)

## Was der Konsument damit tut — und was ausdrücklich nicht

**Vorbelegung, nicht Ersatz.** MiniStore zeigt das Adressfeld weiterhin an; steht eine Adresse aus der
Zahlung bereit, ist sie vorausgefüllt und der Kunde tippt nur noch auf *Senden*. Das Feld verschwindet
**nicht**.

Zwei Gründe, und beide sprechen gegen ein stilles Übernehmen:

1. **Der Zeitpunkt passt nicht zum Ablauf.** Die Adresse steht erst bereit, wenn der Webhook durch ist.
   Der Kunde ist zu dem Zeitpunkt aber womöglich schon auf der Rückkehrseite — die Buchung ist ja gerade
   deshalb aus zwei Richtungen gebaut. Ein Feld, das mal vorbelegt ist und mal nicht, ist erträglich; ein
   Versand, der mal automatisch geschieht und mal nicht, wäre es nicht.
2. **Es ist nicht dieselbe Zusage.** Unter dem Feld steht bei uns, dass die Adresse gespeichert wird
   (MiniStore legt sie in einer eigenen Tabelle `ReceiptDelivery` ab, damit ein Kunde seine Belege später
   wiederfindet). Diese Zusage gilt für eine Adresse, die der Kunde *uns* gibt — nicht für eine, die wir
   uns beim Anbieter abholen.

## Umgehung bis dahin

Keine nötig: der Kunde tippt seine Adresse ein zweites Mal. Es ist eine Unbequemlichkeit, kein Defekt —
entsprechend **keine Priorität von unserer Seite**.

---

## Auflösung (Toolkit)

Umgesetzt wie vorgeschlagen, samt Schalter mit Vorgabe *aus*.

**Der Schalter** — `StripePaymentsOptions.CaptureCustomerEmail`, `bool`, Vorgabe `false`. Die Begründung
des Issues ist im XML-Kommentar festgehalten, weil sie sonst als übervorsichtige Vorgabe gelesen und beim
nächsten Aufräumen auf `true` gedreht wird: die Adresse wurde dem **Anbieter** zu dessen Zweck gegeben,
die Übernahme in die Datenbank des Hosts ist ein anderer.

**Der Schreibweg** — in `MarkPaidAsync`, bei den beiden bestehenden `??=`-Zeilen, gekapselt in
`CaptureCustomerEmail(sale, current, options)`:

```csharp
sale.ProviderPaymentIntentId ??= current.PaymentIntentId;
sale.ProviderChargeId       ??= await ResolveChargeAsync(…);
CaptureCustomerEmail(sale, current, options);
```

Gefüllt wird nur ein leeres Feld, wie vorgeschlagen. `current` ist die bereits nachgelesene Session —
kein zusätzlicher API-Aufruf.

### Drei Punkte, die beim Umsetzen dazukamen

1. **`MarkPaidAsync` ist nachweislich die einzige Stelle**, die `TenantSaleStatus.Paid` setzt (das ganze
   Stripe-Paket kennt genau eine Zuweisung; die Methode trägt es selbst im Kommentar). Es gibt also keinen
   zweiten Buchungsweg, an dem die Erfassung fehlen könnte — genau die Sorge, die der Abschnitt „Der
   Zeitpunkt passt nicht zum Ablauf" nahelegt, trifft den Schreibweg nicht. Sie bleibt für den *Konsumenten*
   richtig: wann der Wert bereitsteht, hängt weiter am Webhook.
2. **Die Erfassung liegt vor dem Früh-Ausstieg für Mehrfachzustellung.** Dort werden die Bezeichner
   ausdrücklich noch gespeichert; eine Adresse, die erst bei der zweiten Zustellung ankommt, geht damit
   nicht verloren.
3. **`options` erreichte `MarkPaidAsync` bisher nicht** und `Trim` gab es im Webhook-Handler nicht (der
   Helfer ist `private static` in `TenantSaleService`). Beides nachgezogen: `options` wird durchgereicht wie
   schon bei `HandleAccountNotificationAsync`, `Trim` liegt jetzt auch im Handler.

### Eine Festlegung zum Protokoll

**Die Adresse selbst wird nie ins Log geschrieben** — das wäre genau die Weiterreise, die der Schalter
verhindern soll. Protokolliert wird nur der Fall, der sonst wie ein nicht verdrahtetes Feature aussähe:
Schalter an, aber die Session führt keine Adresse. Ein leeres Feld hat dann eine Spur, und die nennt die
Session-Id statt der Adresse.

**Für MiniStore:** Schalter einschalten, `TenantSale.CustomerEmail` ist nach dem Webhook gefüllt. Das
Adressfeld bleibt wie beschrieben stehen — das Toolkit trifft dazu keine Annahme.

---

## Nachtrag (Konsument, 2026-09-15): `SaleResult` führt das Feld nicht

Die Umsetzung ist übernommen und der Schalter eingeschaltet — aber der Wert ist über den **Dienstvertrag**
nicht erreichbar. `ITenantSaleService.FindByReferenceAsync` liefert `SaleResult`, und die Klasse kennt
`CustomerEmail` nicht:

```csharp
// …Billing.Stripe/Payments/Abstractions/PaymentAbstractions.cs:146 ff.
public sealed class SaleResult
{
    public int TenantSaleId { get; set; }
    public int TenantId { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TenantSaleStatus Status { get; set; }
    public string? CheckoutUrl { get; set; }
    public long AmountMinor { get; set; }
    public long ApplicationFeeMinor { get; set; }
    public long RefundedMinor { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime? PaidUtc { get; set; }
    public DateTime Created { get; set; }
    public bool WasExisting { get; set; }
}
```

Der Webhook füllt also eine Spalte, die der Konsument über den vorgesehenen Weg nicht lesen kann. Das ist
kein Fehler in der Umsetzung — es stand auch nicht im ursprünglichen Vorschlag — aber es fehlt das letzte
Glied.

**Vorschlag:** `public string? CustomerEmail { get; set; }` an `SaleResult`, befüllt wie die übrigen
Felder aus der Zeile. Rein additiv.

**Was wir bis dahin tun:** MiniStore liest direkt aus `TenantSales` im eigenen Systemkontext (die Tabelle
gehört über `IPaymentsContext` ohnehin dazu) und sucht über `TenantId` + `ExternalReference`. Das
funktioniert, umgeht aber die Abstraktion — die Umgehung verschwindet, sobald `SaleResult` das Feld führt.

Wie beim Hauptteil: **keine Priorität von unserer Seite.**

### Auflösung des Nachtrags (Toolkit)

Nachgezogen wie vorgeschlagen, rein additiv: `SaleResult.CustomerEmail` (`PaymentAbstractions.cs`), befüllt
in `TenantSaleService.ToResult` aus `sale.CustomerEmail` — damit für alle fünf Aufrufstellen zugleich,
`FindByReferenceAsync` eingeschlossen.

Das war eine echte Lücke im ersten Zug: der Schreibweg war gebaut, der Leseweg nicht mitgedacht. Die
Umgehung über den direkten Zugriff auf `TenantSales` kann ersatzlos weg.

Der XML-Kommentar am Feld hält fest, dass der Wert bei eingeschaltetem `CaptureCustomerEmail` erst mit der
Bezahlung erscheint — sonst liest sich ein `null` an einem noch offenen Verkauf wie ein Fehler.
