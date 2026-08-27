# Plan: Stripe Connect — Tenants empfangen Zahlungen von Endkunden

Status: **Entwurf, noch nicht umgesetzt.** Stand 2026-08-26, Zweig Future_10.

## 1. Ausgangslage und Abgrenzung

Der bestehende Billing-Stack (`EntityFramework.Billing`, `Billing.Stripe`,
`Blazor.MudBlazor.BillingViews`, `EntityFramework.Billing.TenantSecurity`) deckt genau **eine** Achse ab:

> **Achse A — der Tenant zahlt dem Plattformbetreiber ein Abo.**
> Plan/AddOn -> Stripe Product+Price -> Checkout -> `TenantSubscription` -> `BillingFeatureGrant` ->
> `TenantFeatureActivation`. Alles laeuft auf dem Plattform-Stripe-Konto.

Dieser Plan ergaenzt eine **zweite, unabhaengige Achse**:

> **Achse B — der Endkunde zahlt dem Tenant.**
> Der Tenant betreibt in der Anwendung einen Shop. Das Geld darf *nicht* ueber das Plattformkonto
> laufen (Geldweiterleitung fuer Dritte ist Stripe-vertraglich untersagt und in der Schweiz nach GwG
> bewilligungspflichtig). Loesung: **Stripe Connect** mit einem eigenen Connected Account je Tenant.

Beide Achsen teilen sich den Plattform-Stripe-Account (denselben `sk_...`-Key), sind sonst aber
vollstaendig getrennt: eigene Tabellen, eigene Services, eigener Webhook, eigenes Feature.

Achse B ist **nicht** Voraussetzung fuer Achse A und umgekehrt. Ein Host kann Connect nutzen, ohne
das Abo-Billing zu aktivieren.

## 2. Entscheidungen

### 2.1 Paketierung — keine neuen Pakete

Achse B wird **in die bestehenden Billing-Pakete** eingebaut, in einem eigenen Ordner-/Namensraum-Zweig
`Payments`. Begruendung:

- Die Paketzahl wurde bewusst von 103 auf 71 gesenkt; drei weitere Pakete fuer eine Funktion, die
  ohnehin nur mit Stripe zusammen Sinn ergibt, laufen dem zuwider.
- Beide Achsen brauchen dieselbe Infrastruktur: `IStripeClient`, `StripeOptions` (derselbe API-Key),
  denselben Feature-Mechanismus, dasselbe Views-Projekt.

Damit das **nicht breaking** ist, wird `IBillingContext` *nicht* erweitert. Stattdessen ein zweiter,
opt-in Kontext-Vertrag im selben Paket:

```csharp
// ITVComponents.WebCoreToolkit.EntityFramework.Billing/IPaymentsContext.cs
public interface IPaymentsContext
{
    DbSet<TenantPaymentAccount> TenantPaymentAccounts { get; set; }
    DbSet<TenantSale> TenantSales { get; set; }
    DbSet<TenantSaleRefund> TenantSaleRefunds { get; set; }
}
```

Ein Host, der Connect nicht will, implementiert `IPaymentsContext` schlicht nicht und bekommt weder
Tabellen noch Migration. `modelBuilder.ConfigurePayments()` ist ein eigener Aufruf neben
`ConfigureBilling()`.

### 2.2 Kontotyp — Express

`Express`-Connected-Accounts: Stripe hostet Onboarding und KYC, der Tenant bekommt ein schlankes
Dashboard, die Plattform behaelt die Kontrolle ueber das Erlebnis und darf die Gebuehr einziehen.

Der Kontotyp wird in den Optionen konfigurierbar gehalten (`Express` | `Standard`), damit ein Host
ohne Codeaenderung auf `Standard` wechseln kann (dort traegt der Tenant die volle Stripe-Beziehung
inkl. Support und Disputes). Umsetzung zunaechst nur fuer Express verifiziert.

### 2.3 Zahlungsfluss — Direct Charges

Die Zahlung entsteht **direkt auf dem Konto des Tenants**; die Plattform zieht ihren Anteil als
`application_fee_amount`.

- Stripe-Gebuehren und Chargebacks liegen beim Tenant — die sachlich richtige Zuordnung, wenn er der
  Verkaeufer ist.
- Der Tenant ist Merchant of Record und damit auch mehrwertsteuerlich der Abrechnende. Die Plattform
  haftet nicht fuer die Lieferung.
- Technisch: alle Aufrufe mit gesetztem `StripeAccountId` in den `RequestOptions` (Stripe.net setzt
  daraus den `Stripe-Account`-Header).

`Destination Charges` (Zahlung auf dem Plattformkonto, Weiterleitung per `transfer_data.destination`)
bleiben als spaeterer Options-Wert vorgesehen, werden aber nicht mitgebaut. **Der Wechsel ist nicht
symmetrisch**: von Direct zu Destination zu wechseln bedeutet, dass die Plattform ab dann Merchant of
Record ist — das ist eine steuerliche und keine technische Entscheidung.

### 2.4 Verkaufserfassung — nur Betrag, keine Positionen

Ein Verkauf wird mit **Total, Waehrung, Bezeichnung und einer Fremdreferenz** erfasst. Positionsdetails
sind ausdruecklich nicht noetig:

- Ein Stripe `PaymentIntent` braucht ohnehin nur `amount` + `currency`.
- Der gewaehlte Weg ist eine **Checkout Session mit genau einem synthetischen Line-Item**
  (`price_data.unit_amount = Total`, `product_data.name = Bezeichnung`). Damit hostet Stripe die
  Bezahlmaske, es faellt keine PCI-Last an (SAQ-A), und der Host muss keine Kartenfelder bauen.

Optionaler zweiter Weg fuer Hosts mit eigener Bezahlmaske: `CreatePaymentIntentAsync` gibt das
`client_secret` zurueck. Wird in derselben Schnittstelle vorgesehen, aber erst gebaut, wenn ein
Konsument es braucht.

### 2.5 Kein Kundenprofil — der Kauf ist ad hoc

**Der Endkunde gibt seine Zahlungsdaten einmal an, und danach bleibt nichts von ihm zurueck.** Das ist
keine Nebenwirkung, sondern eine Entscheidung, und sie gehoert aufgeschrieben, weil das Gegenteil
billiger zu tippen ist als das Gewollte.

Konkret heisst das fuer die Checkout Session aus 2.4:

- `mode: payment` — **kein** `customer` mitgeben und **kein** `setup_future_usage` setzen. Ohne beides
  benutzt Stripe das Zahlungsmittel genau einmal und speichert es nicht.
- `customer_email` dient allein dem Beleg. Es entsteht dadurch **kein** Kundenprofil.
- Was bleibt, reicht fuer alles Weitere: PaymentIntent und Charge liegen auf dem Konto des Tenants, und
  `TenantSale.ProviderChargeId` traegt den Charge. **Eine Rueckerstattung braucht keinen Customer.**

**Warum das der Vorgabewert sein muss:** einen Customer "sicherheitshalber" anzulegen kostet eine
Zeile und macht aus einem anonymen Kauf einen registrierten - mit gespeicherten Zahlungsdaten, einem
Profil, das jemandem gehoert, und einer Datenschutzfrage, die vorher keine war. Wer diese Zeile
schreibt, muss es also wollen.

**Der Gegenfall ist ausdruecklich nicht vorgesehen.** "Karte fuer das naechste Mal merken" verlangt
`setup_future_usage` **und** einen Customer je Besucher - und damit eine dauerhafte Identitaet des
Endkunden, die es im anonymen Shop bewusst nicht gibt. Wer das will, braucht vorher eine Antwort auf
die Frage, wer dieser Besucher eigentlich ist; siehe `docs/Plan-SharedAsset-Objektsicherheit.md`, wo
dieselbe Frage schon einmal offengelassen wurde (der Mechanismus liefert Rechte, keine Identitaet).

**Zusammenspiel mit dem anonymen Zugang:** der Verkaufs-Service laeuft ohnehin ohne Benutzer-Scope -
das Feature-Gate prueft per `TenantId` gegen die Datenbank (siehe 5). Ein Ad-hoc-Ticket kann damit auf
den Auftrag zeigen, der Besucher bezahlt ihn spontan, und danach existiert weder ein Konto noch ein
gespeichertes Zahlungsmittel - nur der Verkauf.

**Ungeprueft:** Das ist Entwurfswissen ueber Stripes Verhalten. Beim Bauen ist gegen einen echten
Account nachzusehen, ob eine Direct Charge auf einem Connected Account sich hier genauso verhaelt -
`customer_creation` hat bei Connect andere Vorgaben als beim Plattformkonto.

## 3. Datenmodell

Neu in `ITVComponents.WebCoreToolkit.EntityFramework.Billing/Models/` (Zweig `Payments`).
Wie bei Billing: `TenantId` ist eine logische `int`-Referenz **ohne** FK auf das Tenant-Modell.

**Alle Betraege in Minor Units (`long`, Rappen/Cent).** Stripe rechnet so; das vermeidet
Rundungsdrift bei der Gebuehrenberechnung. Umrechnung `decimal <-> long` ueber eine kleine
Exponenten-Tabelle (Default 100; JPY/KRW = 1; BHD/KWD/TND = 1000 mit der Zusatzregel, dass die letzte
Stelle 0 sein muss).

### `TenantPaymentAccount`

Der Spiegel des Connected Accounts. Eine Zeile je Tenant.

| Feld | Typ | Bemerkung |
|---|---|---|
| `TenantPaymentAccountId` | int, PK | |
| `TenantId` | int | Unique-Index |
| `ProviderAccountId` | string(256) | `acct_...`, Unique-Index |
| `AccountType` | string(32) | `express` / `standard` |
| `Country` | string(2) | ISO-3166, bei der Anlage fixiert — **spaeter nicht mehr aenderbar** |
| `DefaultCurrency` | string(3) | |
| `ChargesEnabled` | bool | Spiegel aus `account.updated` |
| `PayoutsEnabled` | bool | |
| `DetailsSubmitted` | bool | |
| `RequirementsJson` | string(max) | offene/faellige Anforderungen, roh gespiegelt fuer die Anzeige |
| `DisabledReason` | string(128) | null wenn aktiv |
| `Created` / `Updated` | DateTime | UTC |

### `TenantSale`

| Feld | Typ | Bemerkung |
|---|---|---|
| `TenantSaleId` | int, PK | |
| `TenantId` | int | Index |
| `ExternalReference` | string(128) | Referenz des Hosts (Bestellnummer). **Unique-Index (TenantId, ExternalReference)** — traegt die Idempotenz |
| `Description` | string(256) | erscheint auf der Bezahlseite |
| `AmountMinor` | long | Total |
| `Currency` | string(3) | |
| `ApplicationFeeMinor` | long | zum Zeitpunkt des Verkaufs berechnet und **eingefroren** |
| `Status` | enum | `Pending`, `Paid`, `Failed`, `Canceled`, `Expired`, `Refunded`, `PartiallyRefunded` |
| `ProviderSessionId` | string(256) | `cs_...` |
| `ProviderPaymentIntentId` | string(256) | `pi_...`, Index (Rueckweg vom Webhook) |
| `ProviderChargeId` | string(256) | `ch_...`, fuer Refunds |
| `CustomerEmail` | string(256) | optional |
| `MetadataJson` | string(max) | durchgereichte Zusatzdaten des Hosts |
| `Created` / `Updated` / `PaidUtc` | DateTime? | UTC |

Die Gebuehr wird **gespeichert**, nicht bei Bedarf neu gerechnet — sonst aendert eine spaetere
Konfigurationsaenderung rueckwirkend historische Verkaeufe.

### `TenantSaleRefund`

`TenantSaleRefundId`, `TenantSaleId` (FK, Cascade), `AmountMinor`, `ApplicationFeeRefundedMinor`,
`ProviderRefundId`, `Reason`, `Status`, `Created`. Teil-Rueckerstattungen sind damit moeglich; der
Verkaufsstatus wird aus der Summe abgeleitet (`Refunded` bei Vollbetrag, sonst `PartiallyRefunded`).

`ApplicationFeeRefundedMinor` haelt fest, wieviel der Provision tatsaechlich zurueckgegeben wurde —
ohne das Feld stimmt der Provisionsbeleg aus 13.1 nicht. Warum das nicht automatisch geschieht,
steht in 7.1.

### Migration

Eine Pflicht-Migration am Host. Kein Altbestand-Nachtrag noetig (alle Tabellen neu). Aufnahme in
`docs/Migration-Future_10-MLM.md` als eigener Abschnitt.

## 4. Konfiguration — GlobalSetting `StripePayments`

Wie gewuenscht ein einziges JSON-GlobalSetting, ueber `IGlobalSettings<StripePaymentsOptions>`
gelesen (Muster: `HelpSystemOptions`).

```csharp
[SettingName("StripePayments")]
public class StripePaymentsOptions
{
    /// <summary>Master-Schalter. Steht er auf false, verweigern Service und Views den Dienst,
    /// auch wenn das Feature aktiv ist.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>express | standard.</summary>
    public string AccountType { get; set; } = "express";

    /// <summary>direct | destination. Nur direct ist umgesetzt.</summary>
    public string ChargeType { get; set; } = "direct";

    /// <summary>ISO-4217, wenn der Aufrufer keine Waehrung angibt.</summary>
    public string DefaultCurrency { get; set; } = "CHF";

    /// <summary>Laendercode fuer neue Connected Accounts, wenn er nicht aus der Firmenadresse
    /// des Tenants abgeleitet werden kann.</summary>
    public string DefaultCountry { get; set; } = "CH";

    public ApplicationFeeOptions ApplicationFee { get; set; } = new();

    /// <summary>Signing-Secret des Connect-Webhook-Endpunkts (whsec_...). Getrennt vom
    /// Plattform-Webhook-Secret in Billing:Stripe.</summary>
    public string ConnectWebhookSecret { get; set; } = string.Empty;

    /// <summary>Wenn true, sind Verkaeufe erst moeglich, sobald payouts_enabled steht — nicht
    /// nur charges_enabled. Strenger, aber verhindert Geld auf einem Konto ohne Auszahlungsweg.</summary>
    public bool RequirePayoutsEnabled { get; set; }

    /// <summary>Gibt bei einer Rueckerstattung auch die Provision anteilig zurueck. Vorgabe true —
    /// steht sie auf false, verdient die Plattform an rueckabgewickelten Geschaeften und der Tenant
    /// zahlt beim Storno drauf. Siehe 7.1.</summary>
    public bool RefundApplicationFeeByDefault { get; set; } = true;

    /// <summary>Zusatz auf dem Kontoauszug des Endkunden, max. 22 Zeichen.</summary>
    public string? StatementDescriptorSuffix { get; set; }

    /// <summary>Gueltigkeit einer Checkout-Session in Minuten (Stripe: 30..1440).</summary>
    public int CheckoutExpiryMinutes { get; set; } = 60;
}

public class ApplicationFeeOptions
{
    /// <summary>Anteil in Basispunkten (250 = 2.5 %). Ganzzahlig, damit keine
    /// Gleitkomma-Ueberraschungen entstehen.</summary>
    public int PercentBasisPoints { get; set; }

    /// <summary>Fixer Zuschlag in Minor Units, additiv zum Prozentanteil.</summary>
    public long FixedMinor { get; set; }

    /// <summary>Untergrenze in Minor Units (0 = keine).</summary>
    public long MinMinor { get; set; }

    /// <summary>Obergrenze in Minor Units (0 = keine).</summary>
    public long MaxMinor { get; set; }

    /// <summary>Abweichende Saetze je Waehrung, Schluessel = ISO-4217. Ueberschreibt die
    /// Basiswerte vollstaendig, nicht feldweise.</summary>
    public Dictionary<string, ApplicationFeeOptions>? PerCurrency { get; set; }
}
```

Berechnung: `fee = amount * bp / 10000 + fixed`, kaufmaennisch auf ganze Minor Units gerundet,
danach auf `[MinMinor, MaxMinor]` und hart auf `[0, amount]` geklemmt. Ein einzelner Helfer
`ApplicationFeeCalculator`, damit Anzeige und Buchung nie auseinanderlaufen koennen.

**Bewusst global und nicht tenant-skaliert:** `IScopedSettings` sind ueber die Tenant-Einstellungen
schreibbar (`Tenants.WriteSettings`) — ein Tenant koennte sich damit seine eigene Provision auf 0
setzen. Falls spaeter doch tenant-abhaengige Saetze noetig werden, gehoeren die in eine eigene,
nur vom Plattform-Admin schreibbare Tabelle, **nicht** in `IHierarchySettings`.

Der Stripe-API-Key wird **nicht** dupliziert: er kommt weiter aus `Billing:Stripe` ueber
`StripeOptions` / den registrierten `IStripeClient`.

## 5. Gating

Drei Schichten, jede mit einer eigenen Aufgabe:

**a) WebPart-Schalter** — `ActivatePayments` in `StripeBillingPartOptions`. Ist er aus, werden weder
Services noch Endpunkte registriert. Das ist der Deployment-Schalter.

**b) Feature `StripePayments`** — der Toolkit-Feature-Mechanismus (`HasFeature`). Damit wird der Shop
zu einem verkaufbaren Bestandteil des eigenen Abos: ein `PlanFeature`/`AddOnFeature` mit dem
Schluessel `StripePayments` fuehrt ueber den bestehenden `BillingFeatureProvisioner` automatisch zur
`TenantFeatureActivation`. **Achse A schaltet Achse B frei** — ohne eine Zeile Sonderlogik.

**c) Permissions** — neu zu registrieren (die Auto-Registrierung greift):

| Permission | Zweck |
|---|---|
| `TenantPayments.View` | eigene Verkaeufe und Kontostatus sehen |
| `TenantPayments.Manage` | Auszahlungskonto einrichten, Stripe-Dashboard oeffnen |
| `TenantPayments.Refund` | Rueckerstattungen ausloesen |
| `TenantPayments.Admin` | plattformweite Uebersicht ueber alle Connected Accounts |

Views: `<SecureView RequiredFeatures="StripePayments" RequiredPermissions="TenantPayments.Manage,TenantAdmin">`.

**Wichtig — das Gate gehoert auch in den Service.** Die View-Pruefung schuetzt nur die Anzeige. Der
Verkaufs-Service kann aus einem Hintergrundlauf ohne Benutzer-Scope aufgerufen werden, deshalb prueft
er selbst **per TenantId** gegen die Datenbank, nicht gegen den aktuellen Scope. Dafuer ein
Adapter-Interface analog zu `IFeatureProvisioner`:

```csharp
// neutral in EntityFramework.Billing
public interface IPaymentFeatureGate
{
    Task<bool> IsEnabledForTenantAsync(int tenantId, CancellationToken ct = default);
}
```

Implementierung im tenant-security-nahen Paket `EntityFramework.Billing.TenantSecurity` (dort, wo
`BillingFeatureProvisioner` schon sitzt). Ist kein Gate registriert, gilt das Feature als **nicht**
aktiv — Fail-closed.

## 6. Service-Schicht: Connect-Onboarding

Neu in `ITVComponents.WebCoreToolkit.Billing.Stripe/` unter `Payments/`.

```csharp
public interface ITenantPaymentAccountService
{
    /// <summary>Status des Connected Accounts. Null, wenn noch keiner existiert.</summary>
    Task<TenantPaymentAccountStatus?> GetStatusAsync(int tenantId, CancellationToken ct = default);

    /// <summary>Legt bei Bedarf einen Connected Account an und liefert die URL des von Stripe
    /// gehosteten Onboardings. Wiederholt aufrufbar — ein abgelaufener Link wird ersetzt,
    /// ein bestehendes Konto nie doppelt angelegt.</summary>
    Task<string> StartOnboardingAsync(int tenantId, string returnUrl, string refreshUrl,
                                      CancellationToken ct = default);

    /// <summary>Liest den Kontostand frisch von Stripe und aktualisiert den lokalen Spiegel.
    /// Fuer die Rueckkehr aus dem Onboarding, bevor der Webhook eintrifft.</summary>
    Task<TenantPaymentAccountStatus> RefreshAsync(int tenantId, CancellationToken ct = default);

    /// <summary>Einmal-Link ins Express-Dashboard des Tenants. Null bei Standard-Konten
    /// (die haben ihr eigenes Stripe-Login).</summary>
    Task<string?> CreateDashboardLinkAsync(int tenantId, CancellationToken ct = default);
}
```

Ablauf:

1. `StartOnboardingAsync` -> `AccountService.CreateAsync` (Typ aus Optionen, Land aus dem
   `BillingProfile` des Tenants, Fallback `DefaultCountry`, E-Mail des ausloesenden Benutzers)
   -> `acct_...` sofort in `TenantPaymentAccount` speichern, **bevor** der Link erzeugt wird.
   Sonst entsteht bei einem Abbruch ein verwaistes Stripe-Konto, das niemand mehr findet.
2. `AccountLinkService.CreateAsync(type: "account_onboarding", return_url, refresh_url)` -> Redirect.
3. Rueckkehr auf `return_url`: **Der Status ist dort nicht garantiert fertig.** Deshalb sofort
   `RefreshAsync` und das Ergebnis anzeigen, statt Erfolg zu unterstellen.
4. `refresh_url` wird von Stripe aufgerufen, wenn der Link abgelaufen ist -> neuen Link erzeugen und
   erneut weiterleiten.
5. `account.updated` (Connect-Webhook) haelt den Spiegel dauerhaft aktuell — das Konto kann Tage
   spaeter wieder gesperrt werden, wenn Stripe Unterlagen nachfordert.

## 7. Service-Schicht: Verkaeufe

```csharp
public interface ITenantSaleService
{
    /// <summary>Erfasst einen Verkauf im Namen des Tenants und liefert die gehostete Bezahlseite.
    /// Nur Total und Bezeichnung noetig — keine Positionen.</summary>
    Task<SaleResult> CreateSaleAsync(SaleRequest request, CancellationToken ct = default);

    Task<SaleResult> GetSaleAsync(int tenantSaleId, CancellationToken ct = default);

    /// <summary>Sucht ueber die Fremdreferenz des Hosts (Bestellnummer).</summary>
    Task<SaleResult?> FindByReferenceAsync(int tenantId, string externalReference,
                                           CancellationToken ct = default);

    /// <summary>Voll- oder Teilrueckerstattung. amountMinor null = Restbetrag.
    /// refundApplicationFee null = Vorgabe aus den Optionen (siehe 7.1).</summary>
    Task<RefundResult> RefundSaleAsync(int tenantSaleId, long? amountMinor, string? reason,
                                       bool? refundApplicationFee = null,
                                       CancellationToken ct = default);
}

public sealed class SaleRequest
{
    public int TenantId { get; set; }

    /// <summary>Total in der Haupteinheit, z.B. 49.90.</summary>
    public decimal Amount { get; set; }

    /// <summary>Null = DefaultCurrency aus den Optionen.</summary>
    public string? Currency { get; set; }

    /// <summary>Was der Endkunde auf der Bezahlseite liest.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Referenz des Hosts. Traegt die Idempotenz — zweimal dieselbe Referenz
    /// liefert denselben Verkauf zurueck, nicht einen zweiten.</summary>
    public string ExternalReference { get; set; } = string.Empty;

    public string? CustomerEmail { get; set; }
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public IDictionary<string, string>? Metadata { get; set; }
}
```

`SaleResult`: `TenantSaleId`, `Status`, `CheckoutUrl`, `AmountMinor`, `ApplicationFeeMinor`,
`Currency`, `PaidUtc`.

Ablauf von `CreateSaleAsync`:

1. Vorbedingungen pruefen — Optionen `Enabled`, `IPaymentFeatureGate`, Konto vorhanden und
   `ChargesEnabled` (bzw. `PayoutsEnabled` bei `RequirePayoutsEnabled`). Jede verletzte Bedingung
   wirft mit **eigener, unterscheidbarer Meldung**; ein gemeinsames "nicht moeglich" waere im
   Support wertlos.
2. `TenantSale` mit `Status = Pending` anlegen. Der Unique-Index auf `(TenantId, ExternalReference)`
   faengt den Doppelklick ab — bei Verletzung wird der bestehende Verkauf gelesen und dessen
   Checkout-URL zurueckgegeben.
3. Gebuehr rechnen und einfrieren.
4. Checkout-Session **auf dem Connected Account** erzeugen (`RequestOptions.StripeAccount`), mit
   einem `price_data`-Line-Item, `payment_intent_data.application_fee_amount`, `client_reference_id`
   = `TenantSaleId` und `IdempotencyKey` = `sale:{tenantId}:{externalReference}`.
5. `ProviderSessionId` nachtragen, `CheckoutUrl` zurueckgeben.

### 7.1 Rueckerstattung: die Provision geht NICHT automatisch mit zurueck

**Der teuerste Fallstrick des ganzen Vorhabens.** Bei einem Refund erstattet Stripe standardmaessig
nur den Zahlbetrag — die bereits abgezweigte `application_fee` bleibt auf dem Plattformkonto liegen.
Das Ergebnis:

> Ein Verkauf ueber 100.— mit 2.5 % Provision. Der Tenant erstattet dem Endkunden die vollen 100.—,
> hat aber nur 97.50 minus Stripe-Gebuehr erhalten. Er zahlt beim Storno **drauf** — und die Plattform
> hat an einem rueckabgewickelten Geschaeft verdient.

Bei vielen Stornos summiert sich das zu einem echten Aerger mit dem Tenant, und es faellt erst auf,
wenn er seine Zahlen anschaut. Deshalb:

- `RefundSaleAsync` reicht standardmaessig `refund_application_fee: true` mit, anteilig zur
  erstatteten Summe. Bei Teilerstattungen erstattet Stripe die Fee proportional.
- Steuerbar ueber `RefundApplicationFeeByDefault` in den Optionen (Vorgabe **true**) und je Aufruf
  ueber den Parameter — es gibt Geschaeftsmodelle, in denen eine Bearbeitungsgebuehr bewusst
  einbehalten wird. Das muss dann aber eine **Entscheidung** sein, kein Nebeneffekt der Vorgabe.
- Die tatsaechlich zurueckgegebene Fee wird an der Refund-Zeile mitgefuehrt
  (`ApplicationFeeRefundedMinor`), sonst stimmt der Provisionsbeleg aus 13.1 nicht.

Dasselbe gilt bei **Chargebacks**: der Tenant traegt bei Direct Charges den Ruecklastschriftbetrag
plus Stripe-Gebuehr, deine Fee bleibt unberuehrt. Hier greift `refund_application_fee` nicht — wer
das ausgleichen will, braucht eine eigene Gutschrift. Zunaechst bewusst nicht umgesetzt, aber im
Betrieb zu beobachten.

### Rueckmeldung an den Host

Ohne Rueckweg weiss der Shop nie, dass bezahlt wurde. Deshalb ein Beobachter-Vertrag, den der Host
implementiert und in DI registriert:

```csharp
public interface ITenantSaleObserver
{
    Task OnSaleCompletedAsync(TenantSale sale, CancellationToken ct = default);
    Task OnSaleRefundedAsync(TenantSale sale, TenantSaleRefund refund, CancellationToken ct = default);
}
```

Aufgerufen aus dem Webhook, `IEnumerable<ITenantSaleObserver>`, **nur beim echten Statuswechsel**
`Pending -> Paid` (Stripe liefert mindestens einmal, oft mehrfach — ein bereits bezahlter Verkauf darf
die Bestellung nicht ein zweites Mal freischalten). Wirft ein Beobachter, wird die Ausnahme
protokolliert und der naechste trotzdem aufgerufen; der Verkauf bleibt bezahlt.

## 8. Webhooks

**Zweiter Endpunkt, zweites Secret.** Connect-Events werden im Stripe-Dashboard ueber einen eigenen
Endpunkt-Typ ("Events on connected accounts") abonniert, der ein eigenes Signing-Secret hat. Sie mit
dem bestehenden `/billing/webhook` zu mischen, wuerde bedeuten, beide Secrets blind
durchzuprobieren — unschoen und schlechter diagnostizierbar.

Neu: `POST /billing/connect/webhook` (Pfad konfigurierbar), `AllowAnonymous`, Signaturpruefung gegen
`ConnectWebhookSecret`. Fehlerbehandlung analog zum bestehenden Endpunkt — inklusive der
Warnung im SystemLog bei Signaturfehlern, die dort schon gut funktioniert.

Verarbeitete Ereignisse:

| Event | Wirkung |
|---|---|
| `account.updated` | `TenantPaymentAccount` spiegeln (charges/payouts/details/requirements/disabled_reason) |
| `checkout.session.completed` | Verkauf `Pending -> Paid`, `PaymentIntentId`/`ChargeId` nachtragen, Beobachter rufen |
| `checkout.session.expired` | Verkauf -> `Expired` |
| `payment_intent.payment_failed` | Verkauf -> `Failed` |
| `charge.refunded` | Refund-Zeile spiegeln, Verkaufsstatus aus der Summe ableiten, Beobachter rufen |
| `account.application.deauthorized` | Konto als getrennt markieren, Verkaeufe sperren |

Zuordnung ueber `client_reference_id` (= `TenantSaleId`) mit `ProviderPaymentIntentId` als Rueckfall.
Das `account`-Feld des Events wird gegen den gespiegelten `ProviderAccountId` gegengeprueft: ein Event
fuer ein fremdes Konto darf einen Verkauf nie anfassen.

Wie beim bestehenden Handler gilt: **das Event-Payload kann veraltet sein** — Stripe liefert nicht
streng geordnet. Der aktuelle Stand wird vor dem Schreiben frisch gelesen, und Statuswechsel laufen
nur vorwaerts.

## 9. Views

Alle in `Blazor.MudBlazor.BillingViews`, neuer Ordner `Components/Payments/`. Eigener
Ressourcen-Marker `PaymentMessages` mit `Resources/PaymentMessages.{de,fr,it}.resx` + neutral
(englisch) — dasselbe Muster wie `BillingMessages`.

### 9.1 `/Account/Manage/Payments` — Auszahlungskonto (Tenant)

Die zentrale Seite. Zustaende:

- **Kein Konto** — Erklaertext (warum ein eigenes Konto noetig ist, was Stripe pruefen wird, wie lange
  es dauert) und ein Knopf *Auszahlungskonto einrichten*.
- **Onboarding unvollstaendig** (`details_submitted = false`) — Fortschrittshinweis, Knopf
  *Einrichtung fortsetzen*.
- **Angaben eingereicht, noch nicht freigeschaltet** — Wartezustand mit *Status aktualisieren*.
- **Aktiv** — gruene Statusanzeige (Zahlungen ja/nein, Auszahlungen ja/nein), Knopf
  *Stripe-Dashboard oeffnen*.
- **Gesperrt / Nachforderung** — `disabled_reason` und die faelligen Anforderungen in Klartext,
  Knopf *Angaben ergaenzen* (erzeugt einen neuen AccountLink).

Redirects zu Stripe brauchen `Nav.NavigateTo(url, forceLoad: true)` — wie beim bestehenden Checkout.

### 9.2 `/Account/Manage/Payments/Sales` — Verkaufsliste (Tenant)

Tabelle mit Datum, Referenz, Bezeichnung, Betrag, Gebuehr, Nettoertrag, Status. Filter nach Zeitraum
und Status, Detail-Popup mit den Provider-Kennungen (fuer den Support), Rueckerstattungs-Knopf hinter
`TenantPayments.Refund` mit Bestaetigungsdialog und Betragsfeld.

Responsiv auf `Breakpoint.Sm` wie die uebrigen Manage-Tabellen.

### 9.3 `/Administration/Payments` — Plattform-Uebersicht (Admin)

Alle Connected Accounts mit Status, Landeskennzeichen, offenen Anforderungen und Verkaufsvolumen je
Tenant; Summe der eigenen Provisionen im Zeitraum. Nur lesend plus *Status aktualisieren* je Konto.
Hinter `TenantPayments.Admin`.

### 9.4 Rueckkehr-Endpunkte

`/billing/connect/return` und `/billing/connect/refresh` als schlanke Endpunkte, die auf die
Verwaltungsseite weiterleiten. **Beide URLs muessen absolut sein und den Mandanten-Pfadpraefix
enthalten** — die Praefix-Middleware entfernt ihn vor dem Routing, eine root-absolute URL landet
darum beim falschen Mandanten (derselbe Fallstrick wie in BUG-PRE187 und beim TenantUrlGuard).

### 9.5 Lokalisierung

Vier Sprachen (en neutral, de, fr, it) — vollstaendig, nicht nachgereicht. Betroffen sind auch die
Fehlermeldungen des Services: der Tenant liest sie in der Oberflaeche. Sie werden deshalb als
Schluessel geworfen und in der View uebersetzt, nicht als fertiger deutscher Text.

## 10. Verdrahtung

`StripeBillingPartOptions` bekommt:

```csharp
public bool ActivatePayments { get; set; }
public string ConnectWebhookPath { get; set; } = "/billing/connect/webhook";
public string? PaymentsContextType { get; set; }   // null = ContextType
```

`AddStripePayments<TContext>()` registriert `ITenantPaymentAccountService`, `ITenantSaleService`,
`IStripeConnectWebhookHandler` und den `ApplicationFeeCalculator`. Die Views-Registrierung
(`AddMudBlazorPaymentViews<TContext>()`) ist ein eigener Aufruf, damit ein Host die Services ohne
Oberflaeche nutzen kann — genau der Fall "Shop mit eigenem Frontend".

Der Host muss zusaetzlich `IPaymentFeatureGate` registrieren (Adapter aus
`EntityFramework.Billing.TenantSecurity`) und mindestens einen `ITenantSaleObserver` bereitstellen.

## 11. Phasen

| # | Inhalt | Ergebnis |
|---|---|---|
| 1 | Modell, `IPaymentsContext`, `ConfigurePayments()`, `StripePaymentsOptions`, `ApplicationFeeCalculator` + Tests | Migration erzeugbar, Gebuehrenrechnung belegt |
| 2 | `ITenantPaymentAccountService`, Connect-Webhook mit `account.updated`, Rueckkehr-Endpunkte | Ein Tenant kann sich im Testmodus onboarden |
| 3 | `ITenantSaleService` (Checkout + Refund), restliche Webhook-Ereignisse, `ITenantSaleObserver` | Eine Testzahlung laeuft durch, Gebuehr landet auf dem Plattformkonto |
| 4 | Feature `StripePayments`, Permissions, `IPaymentFeatureGate` | Gating greift in Service und View |
| 5 | Views 9.1 + 9.2 inkl. resx in vier Sprachen | Tenant-Selfservice vollstaendig |
| 6 | Admin-Uebersicht 9.3 | Plattformsicht |
| 7 | Haertung: Idempotenz-Nachweis, Fehlerpfade protokolliert, Doku + Migrationsabschnitt | Auslieferbar |
| 8 | Umsatzabhaengiger Gebuehrenerlass (Abschnitt 14) inkl. Fortschrittsanzeige | Optional, unabhaengig nachruestbar |

Phasen 1-3 sind der harte Kern; 5 ist umfangmaessig die groesste Einzelphase (Zustandsvielfalt der
Onboarding-Seite plus vier Sprachen).

## 12. Fallstricke

- **Der Laendercode ist endgueltig.** `country` eines Connected Accounts laesst sich nach der Anlage
  nicht mehr aendern. Falsch geraten heisst: Konto wegwerfen und neu. Darum aus der Firmenadresse
  ableiten und im Zweifel den Tenant fragen, statt `DefaultCountry` still zu nehmen.
- **Ein aktives Konto kann wieder inaktiv werden.** Stripe fordert Unterlagen nach. `charges_enabled`
  gehoert vor **jeden** Verkauf geprueft, nicht nur beim Onboarding.
- **Zero-decimal-Waehrungen.** `amount * 100` ist fuer JPY falsch. Die Exponenten-Tabelle gehoert an
  eine Stelle, nicht verstreut.
- **Ressourcen-Aufloesung.** Das Views-Projekt hat einen von der Assembly abweichenden Root-Namespace;
  ohne das bereits vorhandene `[assembly: RootNamespace(...)]` findet der Localizer die resx nicht und
  gibt still den Schluessel zurueck. Neue Marker-Klasse in denselben Namensraum legen.
- **Testmodus.** Connected Accounts aus dem Testmodus existieren im Livemodus nicht. Beim Umschalten
  der Keys sind alle `acct_...` wertlos — die Tabelle muss dann geleert werden, sonst zeigt die
  Oberflaeche Konten an, die es nicht gibt.
- **Nie gegen ein echtes Stripe-Konto gelaufen.** Wie beim Abo-Billing gilt: der Testlauf gegen echte
  Stripe-Testkeys ist Bestandteil der Umsetzung, nicht optional.

## 13. Offene Punkte ausserhalb der Technik

1. **Beleg ueber die Provision.** Der Einzug ist kein Thema — die Fee wird bei jeder Zahlung
   automatisch abgezweigt, ohne Inkasso und ohne Ausfallrisiko. Es fehlt allein der **Beleg**: in der
   Stripe-Welt entsteht dazu nur ein `application_fee`-Objekt im Plattformsaldo, keine Rechnung, die
   der Tenant verbuchen koennte. (Das Abo aus Achse A ist davon nicht betroffen — dort erzeugt Stripe
   je Periode eine echte Invoice.)

   Die Daten liegen bereits vor: `TenantSale.ApplicationFeeMinor` ist je Verkauf eingefroren, die
   Rueckerstattungen stehen an den Refund-Zeilen. Ein Monatslauf, der je Tenant summiert, genuegt.
   Drei Wege:
   - eigener PDF-Beleg aus den vorhandenen Daten;
   - Stripe Invoicing auf dem Plattformkonto ueber die Monatssumme, zwingend als **bereits bezahlt**
     markiert (`paid_out_of_band`) — sonst zieht Stripe denselben Betrag ein zweites Mal ein;
   - vorerst nur ein Auszug in der Oberflaeche. Fuer den Vorsteuerabzug des Tenants reicht das
     vermutlich nicht, als erster Schritt ist es vertretbar.

   Zu klaeren bleibt die MwSt-Behandlung: bei einem inlaendischen Tenant ist die Plattformleistung
   steuerbar, bei einem auslaendischen greift in der Regel das Empfaengerortprinzip. Das gehoert vor
   dem ersten Livegang mit dem Treuhaender angeschaut, nicht danach.
2. **AGB / Vertragsbeziehung.** Der Tenant schliesst mit Stripe einen eigenen Vertrag (Connected
   Account Agreement). Die Plattform-AGB muessen darauf verweisen und regeln, wer bei Streitfaellen
   zwischen Endkunde und Tenant welche Rolle hat.
3. **Datenschutz.** Beim Onboarding fliessen Ausweis- und Bankdaten des Tenants zu Stripe. Das gehoert
   in die Datenschutzerklaerung und in die bestehende Consent-Schicht.
4. **Auszahlungssperre.** Ob die Plattform Auszahlungen zurueckhalten koennen soll (z.B. bei
   laufenden Streitfaellen), ist eine Produktentscheidung — technisch ueber den Auszahlungsplan des
   Connected Accounts moeglich, aber nur bei Express/Custom.

## 14. Umsatzabhaengiger Erlass der Abogebuehr

Gewuenschtes Modell: der Tenant zahlt CHF 20.— im Monat fuer die Zahlungsanbindung plus 1 % der
Transaktionen. Ueberschreitet sein Umsatz 10'000.— (also 100.— Provision), entfaellt die Grundgebuehr
fuer diesen Zeitraum.

Technisch ist das **umsetzbar**. Es ist die einzige Stelle, an der Achse B auf Achse A zurueckwirkt.

### 14.1 Erst die Preisfrage: die Delle an der Schwelle

Bei einer harten Schwelle sinkt der Ertrag der Plattform beim Ueberschreiten:

| Umsatz des Tenants | Grundgebuehr | Provision | Ertrag Plattform |
|---|---|---|---|
| 9'900.— | 20.— | 99.— | **119.—** |
| 10'000.— | 0.— | 100.— | **100.—** |
| 12'000.— | 0.— | 120.— | 120.— |

Zwischen 10'000 und 12'000 verdienst du **weniger als knapp darunter**. Der Tenant hat umgekehrt
einen Anreiz, die Schwelle gerade eben zu erreichen. Erst ab 12'000 bist du wieder gleichauf.

**Entschieden: die Delle wird in Kauf genommen** — 20.— sind wenig, und "ab 10'000 Umsatz ist die
Grundgebuehr geschenkt" ist in einem Satz erklaert; als Verkaufsargument ist das mehr wert als die
saubere Kurve. **Beide Verfahren werden aber gebaut** und sind konfigurierbar, damit ein Wechsel
spaeter eine Einstellung und keine Codeaenderung ist:

- `Hard` (Vorgabe) — voller Erlass ab der Schwelle, mit der Delle.
- `Sliding` — anteiliger Erlass, linear ueber ein Band unterhalb der Schwelle:
  `Erlass = Positionsbetrag * (Umsatz — Bandbeginn) / (Schwelle — Bandbeginn)`, geklemmt auf
  `[0, Positionsbetrag]`; ab der Schwelle voll.

### 14.1.1 Wie breit muss das Band sein?

Nicht jedes Band beseitigt die Delle — ein zu schmales verteilt sie nur. Im Band gilt

> `Ertrag(U) = Gebuehr — Gebuehr * (U — R) / (T — R) + Satz * U`

und damit ist der Ertrag genau dann nicht mehr fallend, wenn

> **`(Schwelle — Bandbeginn) * Provisionssatz >= Grundgebuehr`**

Fuer 1 % und CHF 20.— heisst das: das Band muss **mindestens 2'000.— breit** sein. Drei Beispiele:

| Band | Bandbreite x Satz | Ertrag im Band |
|---|---|---|
| 9'000 – 10'000 | 10.— < 20.— | faellt weiter (Delle nur gestreckt) |
| 8'000 – 10'000 | 20.— = 20.— | **flach bei 100.—** (Grenzfall) |
| 6'000 – 10'000 | 40.— > 20.— | steigt durchgehend |

Der Grenzfall ist der guenstigste Kompromiss: keine Delle, keine Einbusse ausserhalb des Bandes.
Die Bedingung wird beim Laden der Konfiguration geprueft und bei Verletzung als Warnung ins
SystemLog geschrieben — sie still zu akzeptieren hiesse, ein Preismodell auszurollen, das nicht tut,
was der Modus verspricht.

Nicht umgesetzt wird das Verrechnungsmodell (`Grundgebuehr = max(0, 20 — Provision)`): sauber
monoton, aber die Gebuehr entfiele schon ab 2'000 Umsatz — deutlich grosszuegiger als gewollt.

### 14.2 Der Perioden-Versatz — der eigentliche Stolperstein

Stripe-Abos rechnen **im Voraus** ab, und die Abo-Periode ist nicht der Kalendermonat: hat der Tenant
am 17. abonniert, laeuft seine Periode vom 17. bis zum 17. Die Rechnung, die am 17.09. entsteht,
deckt den Zeitraum 17.09.–17.10. ab — dessen Umsatz noch niemand kennt.

Daraus folgt zwingend: **der Erlass ist rueckwirkend verdient und wird nach vorne gewaehrt.**

> "Du hast zwischen dem 17.08. und dem 17.09. ueber 10'000.— umgesetzt — deshalb ist der kommende
> Monat fuer dich kostenlos."

Das ist fair und verstaendlich, muss aber **so** kommuniziert werden. Die naheliegende Lesart
("dieser Monat ist gratis, wenn ich diesen Monat genug mache") ist prinzipiell unmoeglich: am
Rechnungsdatum steht das Ergebnis noch nicht fest. Der erste Monat eines neuen Tenants kann nie
erlassen werden — auch das gehoert in den Text der Oberflaeche, sonst kommt die Rueckfrage.

Bemessungsgrundlage ist der **Nettoumsatz der abgeschlossenen Periode**: Summe der `Paid`-Verkaeufe
minus der in derselben Periode gebuchten Rueckerstattungen, Periodengrenzen in UTC.

### 14.3 Umsetzung

**Weg: ein negatives Invoice Item, kein Coupon.** Ein Coupon (`duration: once`) traefe die *ganze*
Rechnung. Wenn die Zahlungsanbindung ein Add-on neben einem groesseren Plan ist, wuerde damit auch
der Plan verschenkt. Ein negatives `InvoiceItem` in exakt der Hoehe der betroffenen Position trifft
genau das Richtige.

Ablauf, aufgehaengt am `invoice.created`-Ereignis des **Plattform**-Webhooks (nicht des Connect-
Webhooks — es geht um die Abo-Rechnung):

1. Rechnung ist eine Subscription-Rechnung eines Tenants mit aktiver Zahlungsanbindung? Sonst fertig.
2. Betroffene Rechnungsposition finden — die Zeile, deren Price zu dem als erlassfaehig markierten
   Plan/Add-on gehoert. **Deren tatsaechlichen Betrag verwenden**, nicht den Listenpreis: bei einem
   Planwechsel mitten in der Periode ist die Position anteilig berechnet.
3. Nettoumsatz der abgeschlossenen Periode ermitteln (`CurrentPeriodStart` der Rechnung rueckwaerts).
4. Schwelle erreicht -> negatives `InvoiceItem` auf denselben Customer und dieselbe Rechnung buchen,
   Betrag **geklemmt auf den Positionsbetrag** (nie mehr, sonst entsteht ein Guthaben), mit
   sprechender Beschreibung ("Grundgebuehr erlassen — Umsatz 12'340.— im Zeitraum …").
5. Entscheidung in einer eigenen Zeile festhalten (siehe unten) — fuer Nachvollziehbarkeit und
   Idempotenz.

**Das Zeitfenster ist real.** Zwischen `invoice.created` und der Finalisierung liegt bei Stripe
standardmaessig etwa eine Stunde; danach ist die Rechnung unveraenderlich. Der Erlass muss also
zuverlaessig innerhalb dieses Fensters gebucht werden. Bleibt der Webhook aus oder faellt der Dienst
aus, ist die Rechnung raus — dann greift nur noch eine Gutschrift auf der Folgerechnung. Der
Nachlauf gehoert deshalb von Anfang an eingeplant, nicht erst nach dem ersten Ausfall.

### 14.4 Stornos: der Stichtag ist der Moment der Bestimmung

**Entschieden.** Die Bemessung findet **genau einmal** statt — beim Erstellen der Abo-Rechnung
(`invoice.created`) — und liest den zu diesem Zeitpunkt gueltigen Stand. Daraus ergibt sich beides,
ohne Sonderregel:

- Eine Stornierung, die **vor** der Bestimmung gebucht wird, mindert den Umsatz und kann den Erlass
  verhindern. Das ist der Fall "der Monat laeuft noch, der Erlass steht noch nicht fest".
- Eine Stornierung **nach** der Bestimmung laesst die getroffene Entscheidung unberuehrt. Sie wirkt
  in der Periode, in der sie gebucht wird.

Zuordnungsregel dazu: **massgebend ist das Buchungsdatum der Rueckerstattung, nicht das Datum des
stornierten Verkaufs.** Ein Storno auf einen Verkauf der Vorperiode mindert also die *laufende*
Periode. Das ist die einzige Variante, die ohne rueckwirkende Korrektur auskommt, und sie gleicht
sich ueber die Zeit von selbst aus.

Es gibt bewusst **keine** Nachbelastung auf bereits bezahlten Rechnungen — der Aufwand und der Aerger
staenden bei 20.— in keinem Verhaeltnis. In den AGB entsprechend formulieren: massgebend ist der
Stand bei der Rechnungsstellung.

Randfall: bei vielen Stornos kann der Nettoumsatz einer Periode rechnerisch negativ werden. Fuer die
Schwellenpruefung ist das unproblematisch (Schwelle nicht erreicht); fuer die Anzeige in 14.6 wird
auf 0 geklemmt, sonst steht dort ein negativer Fortschritt.

### 14.5 Datenmodell und Konfiguration

Eine schmale Tabelle, damit jede Entscheidung nachvollziehbar und der Lauf idempotent ist:

`TenantFeeWaiver`: `TenantSaleWaiverId`, `TenantId`, `PeriodStartUtc`, `PeriodEndUtc`,
`NetVolumeMinor`, `ThresholdMinor`, `Granted` (bool), `WaivedAmountMinor`, `ProviderInvoiceId`,
`ProviderInvoiceItemId`, `Created`. **Unique-Index auf (TenantId, ProviderInvoiceId)** — Stripe
liefert `invoice.created` mehrfach, und ein zweiter Gutschriftsposten waere bares Geld.

Konfiguration als eigener Block in `StripePaymentsOptions`:

```csharp
public enum WaiverMode { Hard, Sliding }

public class VolumeWaiverOptions
{
    /// <summary>Aus, solange nicht gesetzt.</summary>
    public bool Enabled { get; set; }

    /// <summary>Hard = voller Erlass ab der Schwelle (Vorgabe, mit der Delle aus 14.1).
    /// Sliding = anteiliger Erlass ueber ein Band unterhalb der Schwelle.</summary>
    public WaiverMode Mode { get; set; } = WaiverMode.Hard;

    /// <summary>Nettoumsatz der abgeschlossenen Periode in Minor Units, ab dem voll erlassen wird
    /// (1'000'000 = CHF 10'000.—).</summary>
    public long ThresholdMinor { get; set; }

    /// <summary>Beginn des gleitenden Bands. Nur bei Mode = Sliding ausgewertet; muss dann
    /// zwischen 0 und ThresholdMinor liegen. Ist er ungueltig, faellt die Berechnung mit einer
    /// Warnung im SystemLog auf Hard zurueck — ein still falsch gerechneter Erlass waere
    /// schlimmer als ein sichtbar konservativer.</summary>
    public long WaiverRampStartMinor { get; set; }

    /// <summary>Schluessel der Plaene/Add-ons, deren Rechnungsposition erlassen werden darf.
    /// Leer = nichts — Fail-closed, damit nie versehentlich ein ganzer Plan verschenkt wird.</summary>
    public string[] WaivablePlanKeys { get; set; } = [];

    /// <summary>Abweichende Schwellen je Waehrung, Schluessel = ISO-4217.</summary>
    public Dictionary<string, VolumeWaiverOptions>? PerCurrency { get; set; }
}
```

Spaeter liesse sich die Schwelle an den Plan bzw. das Add-on selbst haengen — sie ist eigentlich eine
Eigenschaft des verkauften Angebots, nicht der Stripe-Anbindung. Solange es ein Angebot gibt, ist der
globale Block die guenstigere Loesung; die Erweiterung waere rein additiv.

### 14.6 Sichtbarkeit

Ohne Anzeige ist das Modell wertlos — es soll ja motivieren. Auf der Verkaufsseite (9.2) gehoert ein
Fortschrittsbalken: *"Umsatz laufende Periode: 7'340.— von 10'000.— — noch 2'660.— bis zur
kostenlosen Grundgebuehr im naechsten Monat."* Der Zusatz **"im naechsten Monat"** ist nicht
Kosmetik, sondern die Stelle, an der der Versatz aus 14.2 fuer den Tenant sichtbar wird.

## 15. Referenzen

- Bestehende Achse A: `ITVComponents.WebCoreToolkit.Billing.Stripe`, `...EntityFramework.Billing`
- Feature-Bereitstellung: `EntityFramework.Billing.TenantSecurity/BillingFeatureProvisioner.cs`
- Mandanten-Praefix-Fallstrick: `docs/BUG-PRE187-TenantPathPrefix-RouteValues.md`
- Migrationsschritte am Host: `docs/Migration-Future_10-MLM.md`
