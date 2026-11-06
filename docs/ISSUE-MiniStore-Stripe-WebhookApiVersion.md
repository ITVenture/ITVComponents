# Issue: Stripe.net 53.1.0 weist Webhooks der bisherigen API-Linie zurück

**Status:** OFFEN
**Datum:** 2026-11-06
**Quelle:** MiniStore-Session (Konsument), beim Bump auf `5.0.0-PRE267`.
**Toolkit-Stand:** `5.0.0-PRE267`, `Stripe.net 53.1.0` (vorher `52.4.0`).

## Der Fall

Leitfaden 64.7 sagt zum Umstieg auf `Stripe.net 53.1.0`: „für Hosts ohne eigenen Stripe-Code nichts zu tun".
Das stimmt für die Webhooks nicht. Die Linie wechselt von `dahlia` auf `endive`, und beide Handler rufen
`EventUtility.ConstructEvent` mit der Vorgabe `throwOnApiVersionMismatch: true`:

- `Billing.Stripe/Impl/StripeWebhookHandler.cs:47`
- `Billing.Stripe/Payments/Impl/StripeConnectWebhookHandler.cs:91`

## Nachgestellt

`EventUtility.ParseEvent` gegen `Stripe.net 53.1.0` (dieselbe Prüfung wie in `ConstructEvent`):

| `api_version` des Ereignisses | Ergebnis |
|---|---|
| `2026-05-27.dahlia` | `StripeException` |
| `2026-08-26.dahlia` | `StripeException` |
| `2026-09-30.endive` | angenommen |
| `2026-10-28.endive` | angenommen |

```
Received event with API version 2026-05-27.dahlia, but Stripe.net 53.1.0 expects API version 2026-10-28.endive.
```

Der Lauf ging nur bis zu dieser Prüfung. Ein echtes Ereignis über den Endpunkt wurde nicht geschickt.

Die MiniStore-Sandbox liefert Ereignisse in `2026-05-27.dahlia`, das ist die Standard-Version des Kontos.

## Was das für einen Host bedeutet

Jedes Abonnement, das noch auf einer Dahlia-Version steht oder der Standard-Version eines Dahlia-Kontos folgt, fällt
mit dem Bump aus: `checkout.session.*`, `charge.refunded`, `payment_intent.payment_failed` und auf Achse A die
Abo-Ereignisse. Für Achse B heisst das: Der Kunde hat bezahlt, und `Pending → Paid` kommt nicht mehr an. Eine
Rückerstattung wird nicht gebucht. Stripe versucht die Zustellung zwar erneut, aber ohne Eingriff scheitert sie weiter.

Die Abonnements müssen also spätestens beim Einspielen auf der neuen Version stehen. Das macht den Wechsel zu einem
Betriebsschritt mit Reihenfolge.

## Vorschlag

1. **Leitfaden 64.7 korrigieren:** Der Umstieg verlangt eine Handlung, samt Reihenfolge für eine laufende Installation.
2. **Entscheiden, ob das Toolkit weiter hart ablehnt.** `throwOnApiVersionMismatch: false` übersteht den Wechsel,
   dafür warnt Stripe vor falsch gelesenen Objekten. Eine Option je Handler wäre der Mittelweg, damit ein Host einen
   Linienwechsel ohne Ausfall überbrücken kann.
3. **Die Meldung eindeutig machen:** Beim Webhook sollte im Log als Fehler stehen, welche Version kam und welche
   erwartet wird, statt unter „Signatur" zu laufen. Der Kommentar in `StripeBillingEndpointExtensions.cs:35` nennt
   dort fast nur die Signaturprüfung als Ursache.

## Umgehung in MiniStore

`scripts/Start-StripeWebhookProxy.ps1` startet `stripe listen` lokal mit `--latest`. Eine echte Installation gibt es
noch nicht, die Abonnements dort werden gleich mit `2026-10-28.endive` angelegt.
