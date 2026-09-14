# Issue: Stripe Connect — eine NEUE Plattform kann mit `Accounts v1` kein verbundenes Konto mehr anlegen

**Status:** OFFEN — Wunsch, kein Fehler im Toolkit-Code. Der vorhandene Weg ist korrekt, Stripe hat
ihn für neue Integrationen nur geschlossen.
**Datum:** 2026-09-14
**Quelle:** MiniStore (Kassensystem, Achse B / Stripe Connect, Toolkit `5.0.0-PRE235`, `Stripe.net 52.4.0`)

## Der Fall

Erste Inbetriebnahme von Achse B: frisch für Connect freigeschalteter Stripe-Account (Testmodus),
Ladeninhaber klickt auf `/Account/Manage/Payments` auf *Einrichten*. Stripe lehnt ab:

```
10:32:40  StripeConnect
Could not create a connected account for tenant 7 (type 'express', country 'CH'):
Stripe.StripeException: Stripe no longer recommends Accounts v1 for new Connect integrations.
Create connected accounts with POST /v2/core/accounts instead:
https://docs.stripe.com/api/v2/core/accounts
If your integration requires v1 account creation for a supported compatibility scenario, enable
Accounts v1 support in the Dashboard:
https://dashboard.stripe.com/settings/features/feat_accounts_v1_support
```

**Die Eigenart dieses Falls: bei euch tritt er nicht auf.** Eine Plattform, die vor der Umstellung
eingerichtet wurde, legt weiterhin v1-Konten an — die Sperre gilt nur für *neue* Integrationen. Der
Fehler erscheint also nicht in der Entwicklung, sondern bei der ersten Installation, die ihr
Stripe-Konto neu aufsetzt, und dort an der Stelle, an der man ihn am wenigsten brauchen kann: beim
Kunden, der gerade sein Auszahlungskonto einrichten will.

## Was v1 benutzt

`ITVComponents.WebCoreToolkit.Billing.Stripe/Payments/Impl/TenantPaymentAccountService.cs`:

| Zeile | Aufruf | Wofür |
|---|---|---|
| 171 | `new AccountService(client).CreateAsync(new AccountCreateOptions { Type, Country, Email, Capabilities, Metadata })` | **die abgelehnte Stelle** — legt das verbundene Konto an |
| 85 | `new AccountLinkService(client).CreateAsync(… Type = "account_onboarding" …)` | der Onboarding-Link, auf den der Ladeninhaber weitergeleitet wird |
| 112 | `new AccountService(client).GetAsync(account.ProviderAccountId)` | `RefreshAsync` — holt den Zustand für die Spiegelung |
| 145 | `new AccountLoginLinkService(client).CreateAsync(…)` | das Express-Dashboard des Ladens |

Dazu die Spiegelung selbst (`Apply`, Zeile 206-227), die aus dem v1-`Account` liest:
`ChargesEnabled`, `PayoutsEnabled`, `DetailsSubmitted`, `Requirements.DisabledReason` und die
serialisierten `Requirements`. An `ChargesEnabled`/`PayoutsEnabled` hängt das `PaymentFeatureGate`,
also die Frage, ob ein Laden überhaupt kassieren darf.

Und die Ereignisseite, `Payments/Impl/StripeConnectWebhookHandler.cs`:

| Zeile | Ereignis | Wofür |
|---|---|---|
| 58 | `EventTypes.AccountUpdated` → `stripeEvent.Data.Object is Account` | hält die Spiegelung aktuell |
| 65 | `EventTypes.AccountApplicationDeauthorized` | markiert die Trennung |

Der Umstieg ist damit **kein Austausch eines Aufrufs**: Erstellung, Onboarding-Link, Zustandsabfrage,
Dashboard-Link, die gespiegelten Felder und mindestens zwei Webhook-Nutzlasten hängen daran.

## Was wir uns wünschen

Ein Weg, auf dem eine **neu aufgesetzte** Plattform ohne Dashboard-Sonderfreigabe in Betrieb geht.
Wie er aussieht, entscheidet ihr; aus unserer Sicht wäre wichtig:

1. **Bestehende `acct_`-Ids bleiben bedienbar.** Wer heute Konten hat, darf sie durch die Umstellung
   nicht verlieren — auch eine Installation, die erst später umsteigt, hat dann beide Sorten.
2. **Keine dritte Stelle für dieselbe Entscheidung.** Kontotyp und Land stehen bereits im
   GlobalSetting `StripePayments`; falls es einen Schalter für die API-Generation braucht, gehört er
   unseres Erachtens dorthin und nicht in `appsettings`.
3. **Eine Fehlermeldung, die den Fall benennt.** Heute kommt Stripes Text unverändert beim
   Ladeninhaber an — er liest „enable Accounts v1 support in the Dashboard" und hat kein Dashboard.

**Zu prüfen wäre zuerst**, ob `Stripe.net 52.4.0` die `/v2/core/accounts`-Endpunkte überhaupt schon
typisiert abbildet und wie die dortige Entsprechung zu `account_onboarding` aussieht; darüber können
wir von hier aus nichts Belastbares sagen.

## Wie wir uns bis dahin behelfen

Das Dashboard-Flag, das die Meldung selbst nennt:
`https://dashboard.stripe.com/settings/features/feat_accounts_v1_support` → *Accounts v1 support*
aktivieren. Danach läuft der vorhandene Code unverändert. Für uns reicht das, weil MiniStore noch
nicht ausgeliefert ist — als Anleitung für einen Kunden taugt es nicht.

## Nebenbefund aus derselben Inbetriebnahme (kein Fehler, aber eine Stolperstelle)

Die Zahlungs-Masken (`PaymentAccount`, `Sales`, `PaymentsAdmin`) brauchen `IPaymentsHandler`, und den
registriert `BillingViews/WebPartInit.cs:67-72` nur bei `PaymentViews.ActivatePaymentViews`. Das ist
ein **anderer** Schalter als `Billing:ActivatePayments` (Dienste statt Seiten), und die Trennung ist
nachvollziehbar begründet. Fehlt er, reisst `/Account/Manage/Payments` beim Setzen seiner
`[Inject]`-Eigenschaft den Circuit ab:

```
Cannot provide a value for property 'Handler' on type '…Components.Payments.PaymentAccount'.
There is no registered service of type '…Handlers.IPaymentsHandler'.
```

Uns hat das eine Weile gekostet, weil `ActivatePayments` bereits auf `true` stand und die Seite über
die Navigation erreichbar war. Ein Satz im Leitfaden, dass die beiden Achsen **je zwei** Schalter
haben — Dienste und Seiten —, hätte gereicht. Alternativ ein Hinweis beim Start, wenn
`ActivatePayments` an ist, `ActivatePaymentViews` aber nicht und die Assembly geladen wurde.
