# ANFRAGE: Wird eine Terminalzahlung immer über das Toolkit gemeldet, auch wenn die Kasse weg ist?

**Status:** beantwortet; Lücken behoben (Future_10, Leitfaden §86, ab 5.0.0-PRE263) · **Datum:** 2026-10-27 · **Version:** 5.0.0-PRE262 ·
**Quelle:** MiniStore, Kartenzahlung an der Kasse (Achse C), offener Entscheid „E10“ im MiniStore-Plan

## Worum es geht

MiniStore bucht eine Kartenzahlung **ausschliesslich** über `ITenantSaleObserver.OnSaleCompletedAsync`
(`OrderSaleObserver`). Die Kasse fragt zwar selbst nach (`GetPaymentAsync`, solange der Dialog offen ist), und
wenn danach der Bon fehlt, bucht sie idempotent nach. Die **Verlässlichkeit** soll aber beim Toolkit liegen,
nicht beim Browser der Kasse.

Der Fall, um den es geht: **Die Zahlung ist am Terminal gestartet, und dann ist die Kasse weg.** Der Browser
wird geschlossen, der Laden-PC stürzt ab, das Netz am Laden-PC fällt aus, oder der Circuit bricht ab. Der Kunde
hat aber am Gerät bezahlt. Bei Achse B ist das gelöst, weil der Webhook bucht, auch wenn der Kunde den Browser
zuklappt. Bei Achse C möchten wir wissen, ob dasselbe für **jeden** Weg gilt, oder wo nicht.

Wir fragen ausdrücklich, statt es aus dem Code zu lesen, weil es hier um eine Zusage geht und nicht um den
heutigen Stand einer Implementierung.

## Unser Verständnis (bitte bestätigen oder korrigieren)

1. Ein Terminalverkauf **ist** ein `TenantSale`. Er entsteht beim Start im Backend, bevor das Gerät angestossen
   wird (§70.1).
2. `Unknown` und eine Ausnahme beim Start heissen **nicht** „nicht bezahlt“. Der Verkauf bleibt offen (§70.2).
3. `OnSaleCompletedAsync` wird **genau einmal** gerufen, auch über mehrere Instanzen (§75.2).
4. `TerminalCompletionSweeper` kümmert sich nur um Verkäufe, die **schon `Paid` sind** und deren Meldung noch
   aussteht (`CompletionPendingSince`). Einen Verkauf, der noch `Pending` ist, treibt er **nicht** voran (§75.3).

## Die Fragen

### F1 — Wer treibt einen offenen Terminalverkauf zum Endzustand, wenn niemand mehr nachfragt?

Je Weg: Kommt das Ergebnis ohne Zutun der Kasse ins Backend, und wenn ja, worüber?

| Weg | Unsere Vermutung | Bitte bestätigen |
|---|---|---|
| `stripe` (server-driven) | Webhook `payment_intent.succeeded`/`.payment_failed` auf dem Connect-Endpunkt | Ist das so verdrahtet? Welche Ereignisse müssen im Stripe-Abonnement stehen? Unser `stripe listen` führt heute nur die Achse-B-Ereignisse |
| `wallee` (Cloud, auch mit eigenem Konto §81) | Webhook der Transaktion | Wird der Webhook auch beim eigenen Konto des Mandanten zugeordnet? Was muss der Mandant bei wallee einrichten? |
| `payrexx` (§80) | Webhook | wie wallee |
| `sumup` (§79) | ? | Gibt es einen Rückkanal, oder nur das Nachfragen? |
| `agent` (lokales Gerät, z. B. ep2 im LAN) | **keiner**: Das Ergebnis kennt nur das Gerät hinter dem Agenten | Fragt das Toolkit nach, sobald der Agent wieder verbunden ist? |

### F2 — Gibt es einen Abgleich für hängende `Pending`-Terminalverkäufe?

Falls ein Weg keinen Rückkanal hat oder ein Webhook verloren geht: Gibt es einen Hintergrunddienst, der offene
Terminalverkäufe nach einer Frist **beim Anbieter bzw. Gerät nachfragt** (`GetPaymentAsync` über die Weiche),
ähnlich dem `TerminalCompletionSweeper`?

- Wenn ja: Wie oft, wie lange, und was passiert nach der Frist? Bleibt der Verkauf `Pending`, wird er `Unknown`,
  oder wird er abgebrochen?
- Wenn nein: Ist das so gewollt (der Host ist zuständig), oder wäre es ein Kandidat für das Toolkit?

### F3 — Agenten-Weg: Was passiert mit einer Zahlung, während der Agent getrennt ist?

- Der Agent stürzt mitten in der Zahlung ab und kommt nach 5 Minuten wieder. Fragt jemand das Gerät nach dem
  Ergebnis (das Gerät hat es womöglich noch), oder bleibt der Verkauf offen, bis ein Mensch eingreift?
- Läuft das Nachfragen dabei über dieselbe Route (`TenantPaymentTerminal.Route` = `ClientKey`), also nur, wenn
  **derselbe** Agent wieder da ist?

### F4 — Was sieht der Laden, wenn es ungeklärt bleibt?

MiniStore zeigt ungeklärte Kartenaufträge an der Kasse an (`PendingCardPaymentsDialog`, nach dem Stand des
`TenantSale`). Gibt es im Toolkit eine Stelle, die solche Verkäufe **mandantenweit** sichtbar macht, etwa in der
Verkaufsübersicht mit Filter „offen seit mehr als N Minuten“? Dann würden wir nichts Eigenes dafür bauen.

### F5 — Gilt „genau einmal“ auch zwischen Webhook und Nachfragen?

Kommen Webhook und `GetPaymentAsync` der Kasse gleichzeitig mit `Paid` an: Bestätigt das Toolkit, dass der
Beobachter auch dann nur einmal gerufen wird? Wir lesen §75.2 so, dass das Leeren von `CompletionPendingSince`
der Anspruch ist. Gilt das auch für einen Verkauf, der nie `CompletionPendingSince` hatte, also wenn das Terminal
zuerst antwortet?

## Was MiniStore mit der Antwort macht

- Ist F1 für einen Weg mit „ja, über Webhook/Abgleich“ beantwortet, betrachten wir diesen Weg als verlässlich.
  Das Nachfragen an der Kasse bleibt dann nur der schnelle Weg für die Anzeige, wie die Rückkehrseite bei
  Achse B.
- Für Wege ohne Rückkanal (vermutlich `agent`) entscheiden wir danach, ob wir auf ein Toolkit-Nachfragen warten
  oder bis dahin selbst einen Abgleich bauen.
- Der Plan-Entscheid E10 (Teil 1: „wer erfährt, dass bezahlt wurde“) wird mit der Antwort geschlossen.

## Nicht Teil dieser Anfrage

Wo die Zugangsdaten eines Anbieters liegen (Backend über ExternalServices oder Laden-PC) und welche
Anmeldetypen eigene Zugangsdaten zulassen, klärt der Betreiber separat (E10, Teil 2).


---

## Antwort des Toolkits (2026-10-27)

Wir haben am Code geprüft statt zu bestätigen, und die Prüfung hat Lücken gefunden. Sie sind behoben (Leitfaden
§86, kein Schema-Change). **Die folgenden Zusagen gelten ab dem Paket nach PRE262**, nicht für PRE262 selbst. Gegen
echte Geräte und Konten ist davon noch nichts gelaufen.

### Zu „Unser Verständnis“

1. **Bestätigt.** Der `TenantSale` entsteht und wird gespeichert, bevor das Gerät angestossen wird.
   `OperationId` ist die `TenantSaleId`.
2. **Bestätigt, mit einer Präzisierung.** `Unknown` und eine Ausnahme beim Start lassen den Verkauf offen. `Failed`
   wird nur gebucht, wenn der Anbieter **ausdrücklich** ablehnt (z. B. Stripe lehnt den Befehl ab, SumUp meldet das
   Gerät offline: dann hat das Gerät nichts bekommen). Bis PRE262 buchte Stripe auch einen Netzwerkfehler ohne
   Antwort als `Failed`, das ist jetzt `Unknown`.
   **Neu:** Stripe und wallee speichern ihre Vorgangskennung **vor** dem Gerätestart. Bis PRE262 ging sie verloren,
   wenn die Antwort auf den Start ausblieb: Der Verkauf war dann nicht mehr nachzufragen, und ein erneuter Start
   legte einen zweiten Vorgang an.
3. **Gilt jetzt allgemein, bis PRE262 nicht.** Bis PRE262 war nur die *zurückgehaltene* Meldung (Webhook vor Terminal)
   geschützt. Bei „Terminal zuerst“ konnten zwei gleichzeitige Wege beide melden: Kasse und SumUp-Rückmeldung,
   zwei Fenster, Terminal und Webhook. Jetzt ist der Übergang auf bezahlt selbst der Anspruch
   (`TenantSale.PaidUtc` als Concurrency-Token, §86.1). Genau einmal gilt für jede Reihenfolge und über mehrere Instanzen.
4. **Für den Sweeper bestätigt, aber nicht mehr die ganze Wahrheit.** Der Teil „Beleg nachholen“ fasst weiterhin nur
   `Paid` mit `CompletionPendingSince` an. Neu hat derselbe Dienst eine zweite Aufgabe: offene Terminalverkäufe
   abgleichen (siehe F2).

### F1 — Wer treibt einen offenen Terminalverkauf zum Endzustand?

Für **alle** Wege gilt jetzt der Hintergrund-Abgleich aus F2. Zusätzlich:

| Weg | Rückkanal | Was einzurichten ist |
|---|---|---|
| `stripe` | **kein Webhook** für Terminalverkäufe, und es braucht keinen: der Abgleich fragt die Zahlungsabsicht bei Stripe ab | nichts Zusätzliches im Stripe-Abonnement; euer `stripe listen` mit den Achse-B-Ereignissen genügt |
| `wallee` (Plattform und eigenes Konto) | Transaktions-Webhook; der Verkauf wird jetzt immer gefunden, weil die Transaktions-Kennung vor dem Gerätestart gespeichert ist | wie §81.1: Webhook-URL (eigenes Konto: `…/billing/wallee/webhook/{accessId}`), Listener für **Transaction und Refund**, „payload signing and state“ |
| `payrexx` (ECR) | **kein** Webhook für ECR-Zahlungen; nur der Abgleich | nichts |
| `sumup` | `return_url`-Rückmeldung (`WebhookUrl` in den SumUp-Optionen); sie fragt über die Weiche nach und bucht | `WebhookUrl` setzen; ohne sie bleibt der Abgleich |
| `agent` | **keiner**; der Abgleich fragt nach, sobald derselbe Agent wieder erreichbar ist | nichts |

### F2 — Abgleich für hängende `Pending`-Terminalverkäufe

**Ja, jetzt im Toolkit** (§86.3, Vorgabe eingeschaltet). Der `TerminalCompletionSweeper` fragt offene
Terminalverkäufe über die Weiche nach (`GetPaymentAsync`, wie die Kasse) und bucht, was zurückkommt:

- **Wann:** wenn seit `TerminalReconcileAfterSeconds` (Vorgabe 120) niemand gefragt hat. Massgebend ist
  `TenantSale.Updated`, das jede Nachfrage der Kasse auffrischt. Solange eure Kasse fragt, bleibt der Hintergrund weg.
- **Wie oft:** alle `TerminalReconcileIntervalSeconds` (Vorgabe 60) je Verkauf, auch wenn das Gerät nicht antwortet.
- **Wie lange:** `TerminalReconcileMaxAgeHours` (Vorgabe 24) ab Start.
- **Nach der Frist:** Der Verkauf **bleibt `Pending`**. Er wird nicht abgebrochen und nicht auf `Failed` gesetzt,
  weil ob die Karte belastet wurde weiterhin offen ist. Es wird einmal ein Fehler protokolliert, und der Verkauf
  bleibt in der Verkaufsübersicht sichtbar (F4). Abschliessen muss ihn ein Mensch nach Prüfung am Gerät bzw. beim
  Anbieter.

Einen eigenen Abgleich in MiniStore braucht ihr dafür nicht. Habt ihr schon einen, gilt „genau einmal“ trotzdem;
er fragt dann nur doppelt.

### F3 — Agenten-Weg: Agent getrennt

- Stürzt der Agent mitten in der Zahlung ab, bleibt der Verkauf `Pending`. Der Abgleich fragt jede Minute; die
  Fragen scheitern, solange der Agent fehlt. **Die erste Frage nach dem Wiederverbinden** holt das Ergebnis vom
  Gerät und bucht es. Kein Mensch muss eingreifen, solange das Gerät das Ergebnis noch kennt.
- **Ja, nur über denselben Agenten:** Die Route ist `TenantPaymentTerminal.Route` = `ClientKey` (plus
  `ConfigurationJson`). Ein anderer Agent kann den Vorgang nicht beantworten.
- Einschränkungen:
  - **wallee LTI** kann nur nach der **letzten** Transaktion des Geräts fragen. Lief inzwischen eine neue Zahlung am
    selben Gerät, bleibt der alte Verkauf `Unknown` → nach der Frist von Hand.
  - Wird das Gerät inzwischen **abgeschaltet**, wird trotzdem weiter nachgefragt (neu, §86.3).
  - Wird es auf einen **anderen Anbieter umgestellt**, kann ein noch offener Verkauf nicht mehr nachgefragt werden.
    Erst umstellen, wenn nichts mehr offen ist.

### F4 — Mandantenweite Sicht

**Ja, ab jetzt:** `/Account/Manage/Payments/Sales` hat eine Spalte „Terminal“, die Filter „Nur Terminal“ und „Offen
seit (Min.)“ (nur `Pending`, mindestens so lange gestartet) und für offene Terminalverkäufe in der Detailansicht
„Jetzt nachfragen“ (Recht `TenantPayments.Manage`). Euer `PendingCardPaymentsDialog` an der Kasse bleibt sinnvoll
für die Kasse selbst. Eine eigene mandantenweite Sicht müsst ihr nicht bauen.

Ebenfalls im Toolkit: Ein offener Terminalverkauf lässt sich in der Detailansicht **von Hand** als bezahlt oder
nicht bezahlt abschliessen, mit Pflicht-Begründung und eigenem Recht **`TenantPayments.Settle`**. Das Recht steckt
bewusst nicht in `TenantAdmin` und wird gezielt zugewiesen. Bezahlt benachrichtigt `OnSaleCompletedAsync` genau
einmal (ohne Beleg); nicht bezahlt wird `Canceled`. Hat inzwischen ein anderer Weg entschieden, gilt dessen Ergebnis.
Die Spur (wer, warum, Kennung) steht im Protokoll (Leitfaden §86.4).

### F5 — Genau einmal zwischen Webhook und Nachfragen

**Ja, ab jetzt in jeder Reihenfolge** (siehe Punkt 3 oben). Auch der Fall „Terminal antwortet zuerst, der Verkauf
hatte nie `CompletionPendingSince`“ ist abgedeckt: Der Übergang auf bezahlt gelingt genau einem Weg. Wer ihn
verliert, legt seine Angaben (Beleg, Kennung, E-Mail) auf den Stand des Gewinners und meldet nicht. Bis PRE262
konnte dieser Fall doppelt melden, und ein verspäteter Webhook konnte sogar eine dritte Meldung durch den Sweeper
auslösen.

### Nebenbei behoben (betrifft euch, wenn ihr Stripe Terminal nutzt)

Bei Stripe-Terminalverkäufen stand die Zahlungsabsicht statt der Belastung auf dem Verkauf. Erstattungen und das
Spiegeln von `charge.refunded` liefen dadurch ins Leere. Behoben (§86.5), bestehende Verkäufe werden über die
Zahlungsabsicht erstattet.

### Für den Plan-Entscheid E10, Teil 1

Verlässlich ohne die Kasse ist jetzt **jeder** Weg, über den Hintergrund-Abgleich. Wallee und SumUp sind
zusätzlich schneller über ihren Rückkanal. Das Nachfragen an der Kasse ist damit nur noch der schnelle Weg für die
Anzeige. Offen bleiben nur Fälle, in denen das Gerät oder der Anbieter das Ergebnis selbst nicht mehr liefern kann:
Payrexx ohne Antwort schon auf den Start, wallee LTI nach einer neueren Zahlung am selben Gerät. Diese erscheinen
nach der Frist als Fehler im Log und in der Übersicht und werden dort von Hand abgeschlossen (F4).
