# Issue: Connect v2 — Express-Dashboard mit Stripe-Haftung braucht eine Vorschau-API-Version, die der Host nicht setzen kann

**Status:** OFFEN — Wunsch, kein Fehler. Der v2-Weg aus PRE236 funktioniert; er erreicht nur eine
Kombination nicht, die vorher (v1) der Normalfall war.
**Datum:** 2026-09-14
**Quelle:** MiniStore (Kassensystem, Achse B, Toolkit `5.0.0-PRE236`, `Stripe.net 52.4.0`)
**Vorgänger:** `ISSUE-MiniStore-StripeConnect-Accounts-v2.md` (aufgenommen und umgesetzt)

## Der Fall

Erste Kontoanlage über den neuen v2-Weg. Das Profil war vollständig — der neue
`ProfileIncomplete`-Riegel hatte vorher korrekt auf die fehlende Rechtsform hingewiesen —, und der
Aufruf ging an Stripe. Von dort:

```
13:01:32  StripeConnect
Could not create a connected account for tenant 7 (dashboard 'express', country 'CH',
entity type 'company'): Stripe.StripeException: This account configuration is not supported.
Please reference https://docs.stripe.com/connect/design-an-integration.
```

Die verlinkte Seite nennt den Grund:

> **Vorschau-API-Version für das Express-Dashboard erforderlich**
> Die Kombination aus Express-Dashboard-Zugriff und der Verantwortung von Stripe für negative Salden
> befindet sich in der öffentlichen Vorschau. Verwenden Sie die Zeichenfolge der aktuellen
> Vorschauversion (`2026-08-26.preview`) in Ihrer Stripe-SDK-Konfiguration, wenn Sie verbundene
> Konten erstellen.

Gesendet wird genau diese Kombination (`TenantPaymentAccountService.cs:211-262`):

| Feld | Wert | Herkunft |
|---|---|---|
| `Dashboard` | `express` | GlobalSetting `StripePayments.DashboardType` |
| `Defaults.Responsibilities.FeesCollector` | `stripe` | GlobalSetting |
| `Defaults.Responsibilities.LossesCollector` | `stripe` | GlobalSetting — **das ist die Hälfte, die es zur Vorschau macht** |

Jede der beiden Eigenschaften für sich ist allgemein verfügbar; nur zusammen brauchen sie die
Vorschau-Version.

## Warum das mehr ist als eine Einstellungsfrage

Ein Host kann heute zwischen drei Dingen wählen, und zwei davon sind schlechter als das, was v1 ohne
Zutun konnte:

1. **`LossesCollector = "application"`** — Express bleibt, aber die **Plattform** haftet für negative
   Salden. Das ist eine Haftungsübernahme, keine Konfiguration. Bei MiniStore steht ihr eine
   Kommission von 0.1 % gegenüber: eine ungedeckte Rückbuchung von 200 Franken kostet die Marge aus
   200'000 Franken Ladenumsatz. Für eine Plattform mit dünner Marge ist das keine Option.
2. **`DashboardType = "full"`** — Stripe haftet weiter, aber der Laden bekommt ein vollwertiges
   Stripe-Konto und meldet sich dort selbst an. Der Login-Link entfällt
   (`CreateDashboardLinkAsync` liefert bei `full` bewusst `null`), das schlanke Express-Fenster mit
   dem Branding der Plattform ebenso.
3. **Warten.**

MiniStore ist Weg 2 gegangen — bewusst und dokumentiert, weil der Dashboard-Typ je Konto
unveränderlich ist und wir noch keine produktiven Konten haben. Für eine Plattform mit Bestand an
Express-Konten wäre derselbe Weg eine Neuanlage aller Konten.

## Was wir uns wünschen

Die API-Version konfigurierbar machen, mit der der Connect-Client arbeitet — sodass ein Host, der
Express plus Stripe-Haftung braucht, `2026-08-26.preview` setzen kann, ohne dass das Toolkit sie
allen anderen aufzwingt.

Aus unserer Sicht wichtig:

1. **Nicht als Vorgabe.** Eine Vorschau-Version darf sich ändern; wer sie nicht braucht, soll sie
   nicht bekommen.
2. **Bei den übrigen Connect-Angaben.** Kontotyp und die beiden Kassierer stehen im GlobalSetting
   `StripePayments`; eine Versionsangabe gehört unseres Erachtens daneben und nicht in `appsettings`.
3. **Sichtbar, wenn sie greift.** Läuft eine Installation auf einer Vorschau-Version, ist das eine
   Zeile im Protokoll beim Start wert — sonst merkt es niemand, bis Stripe die Version abkündigt.

**Zu prüfen wäre**, ob `Stripe.net 52.4.0` eine Vorschau-Version überhaupt annimmt (in der v1-Welt
gab es `StripeConfiguration.ApiVersion` bzw. `RequestOptions.StripeVersion`; wie das bei
`StripeClient.V2` aussieht, können wir von hier aus nicht sagen) und ob die Vorschau über die reine
Versionsangabe hinaus etwas am Aufruf ändert.

## Kein Zeitdruck

MiniStore läuft mit `full` und braucht nichts davon, um in Betrieb zu gehen. Der Wunsch zielt auf
den Zeitpunkt, an dem wir die Frage „Express oder Full" für echte Kunden entscheiden — und darauf,
dass eine Plattform mit bestehenden Express-Konten beim Umstieg auf v2 nicht vor der Wahl zwischen
Haftungsübernahme und Neuanlage steht.
