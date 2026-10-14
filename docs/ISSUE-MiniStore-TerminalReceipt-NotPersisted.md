# ISSUE: Achse C — die Belegangaben des Terminals erreichen den Beobachter nie

**Status:** umgesetzt in `5.0.0-PRE259` (`07a2d07d`) — Spalten `TerminalReceiptJson`, `TipMinor`, `CompletionPendingSince` am `TenantSale`; `TerminalReceiptStore`; zurückgehaltene Meldung in `TenantSaleWebhookSink` (`MarkTerminalPaidAsync`, `ReleasePendingCompletionAsync`); `TerminalCompletionSweeper`; wallee Cloud mit Beleg und Trinkgeld, Stripe mit Trinkgeld; Schalter `tipOnReceipt`. Leitfaden §75 · **Datum:** 2026-10-14 · **Version:** 5.0.0-PRE258 · **Quelle:** MiniStore, Kartendaten auf dem Kassenbon

## Der Fall

Ein Terminal meldet zu einer erfolgreichen Zahlung die Angaben, die auf den Händlerbeleg gehören:
`TerminalPaymentOutcome.Receipt` (`TerminalReceipt`, also Marke, maskierte Kartennummer, Autorisierung,
AID, Bestätigung, Terminal-Id). Diese Angaben kommen aber nur bis in den Rückgabewert
(`TerminalSaleResult.Receipt`). Gespeichert werden sie nirgends.

Der Host bucht seinen Beleg dagegen im `ITenantSaleObserver`. Gerufen wird er aus
`TenantSaleWebhookSink.MarkPaidAsync` → `TenantSaleNotifier.NotifyCompletedAsync`, also **bevor**
`TerminalPaymentServiceBase.ApplyAsync` das Ergebnis samt `Receipt` an den Aufrufer zurückgibt. Der
Beobachter sieht nur den `TenantSale`, und der trägt keine der Angaben.

Die Folgen:

1. **Der gebuchte Beleg kann die Kartendaten nicht enthalten.** Wenn die Kasse sie kennt, ist er
   schon geschrieben, und einen gebuchten Beleg zu ändern verbietet die Journal-Regel.
2. **Ein späteres Nachfragen findet sie auch nicht mehr.** `GetPaymentAsync` eines entschiedenen
   Verkaufs geht über `DescribeAsync` und baut das Ergebnis aus dem `TenantSale`, ohne `Receipt`.
3. **Der Weg über einen Webhook** (Stripe Terminal) hat dasselbe Problem: Auch dort bucht der
   Beobachter, und die Angaben fehlen.
   *Korrektur (Review):* Für Stripe trifft das nicht zu. `StripeConnectWebhookHandler` verbucht nur
   `checkout.session.completed`, ein Terminal-Verkauf wird dort nie bezahlt gemeldet. Betroffen sind
   die Webhooks von **Payrexx und wallee** — siehe Ergänzung 1.

## Vorschlag

Die Angaben **am `TenantSale` ablegen, bevor der Beobachter gerufen wird**:

- Eine nullbare Spalte `TerminalReceiptJson` am `TenantSale` (oder eine eigene 1:1-Tabelle, falls der
  Verkauf schlank bleiben soll). Sie wird in `ApplyAsync` bei `Succeeded` gesetzt, **vor**
  `sink.MarkPaidAsync`.
- Eine typisierte Lesehilfe, z.B. `TenantSale.GetTerminalReceipt()` oder
  `TerminalReceipts.Of(TenantSale)`. Dann muss kein Host das JSON selbst deuten.
- `DescribeAsync` füllt `Receipt` aus der Spalte. Damit liefert auch ein späteres Nachfragen die
  Angaben, etwa nach einem „Später klären“ an der Kasse.
- Beim Webhook-Weg (Stripe Terminal) dasselbe aus dem `PaymentIntent`/`Charge`
  (`payment_method_details.card_present`), sofern der Handler sie hat.

**Nie die volle Kartennummer** in diese Spalte. Das Terminal liefert sie ohnehin maskiert, und das
sollte so dokumentiert sein.

## Was MiniStore schon vorbereitet hat

- Der Bon kennt einen Kartenblock (`ReceiptPayment.Card`), und `CheckoutService.BookStripePaymentAsync`
  nimmt die Angaben entgegen.
- Die Abbildung `TerminalReceipt` → Bon steht an einer Stelle (`TerminalReceiptMapping`).
- Heute kommen die Angaben nur auf den Bon, wenn die Kasse selbst nachbucht, also wenn der Beobachter
  gescheitert ist. Sobald der `TenantSale` sie trägt, übergibt sie der `OrderSaleObserver` beim
  Buchen. Das ist eine Zeile.

## Ergänzungen (Review 2026-10-14)

### 1. Abgeschlossen ist ein Verkauf erst, wenn auch der Beleg da ist

**Grundsatz:** Der Beobachter wird erst gerufen, wenn der Vorgang wirklich vollständig ist — bezahlt
**und** mit Belegangaben. Heute kann der Webhook von Payrexx oder wallee vor der Antwort des Terminals
eintreffen: `MarkPaidAsync` setzt dann `Paid` und ruft den Beobachter ohne Beleg. Die spätere Antwort
in `ApplyAsync` trifft auf einen bereits bezahlten Verkauf, und der Beobachter wird nicht noch einmal
gerufen.

**Zu trennen sind zwei Dinge:**

- **Der Status** `Paid` wird gesetzt, sobald das Geld sicher ist — egal, wer es zuerst weiss. Das
  bleibt so: Erstattung, Abgleich und Nachfragen hängen am Status.
- **Die Meldung** an den Beobachter (`NotifyCompletedAsync`) wird zurückgehalten, bis die
  Belegangaben vorliegen.

**Woher der Beleg kommt, wenn der Webhook zuerst da ist:**

1. Die Antwort des Terminals (`ApplyAsync`) holt die Meldung nach — der Normalfall, meist Sekunden später.
2. Bleibt sie aus, fragt der Hintergrund-Durchlauf (alle `ReceiptPollSeconds`) über die Weiche das Gerät
   bzw. den Anbieter nach dem Beleg.

*Umsetzung:* Der Webhook holt den Beleg **nicht** selbst. Das hätte die Abfrage je Anbieter-Webhook ein
zweites Mal gebaut; der Durchlauf erledigt dasselbe anbindungsneutral innerhalb von `ReceiptPollSeconds`.

**Der Haken am reinen Warten:** Der Request, der die Zahlung ausführt, kann ausbleiben — die Kasse
startet mitten in der Zahlung neu, das Netz bricht weg (`StartPaymentAsync` lässt den Verkauf dann
bewusst offen). Ohne Rückfallebene würde der Beobachter **nie** gerufen, und der Verkauf wäre bezahlt,
aber nie gebucht. Darum braucht es:

- ein Merkmal am Verkauf, dass die Meldung noch aussteht (z.B. `CompletionNotifiedUtc`, null = offen),
- eine Frist, nach der die Meldung auch **ohne** Beleg erfolgt (protokolliert als Warnung), und
- jemanden, der die Frist prüft: das nächste Nachfragen der Kasse (`GetPaymentAsync`) und ein
  Hintergrund-Durchlauf für den Fall, dass niemand mehr nachfragt.

`GetPaymentAsync` auf einen bezahlten Verkauf mit ausstehender Meldung fragt Gerät bzw. Anbieter
**einmal** nach dem Beleg, statt nur aus dem `TenantSale` zu beschreiben.

**Entschieden — Hintergrund-Durchlauf als `IHostedService` im Billing-Paket:**

- Er kennt **keine** Anbindung. Für jeden bezahlten Verkauf mit ausstehender Meldung ruft er
  `GetPaymentAsync` über die Weiche (`RoutingTerminalPaymentService`); die leitet nach dem Weg des
  Geräts weiter — beim Agenten über den `IAgentConnector` zur Kasse. Damit deckt er jeden
  Terminal-Typ ab, auch künftige.
- Liefert die Abfrage einen Beleg, wird mit Beleg gemeldet. Ist die Frist abgelaufen, wird **ohne**
  Beleg gemeldet und eine Warnung protokolliert (Agent offline, Anbieter liefert nichts).
- **Mandant:** Der Dienst läuft ohne Benutzer und ohne Mandanten im Bereich. Er sucht die offenen
  Verkäufe **ohne** Mandantenfilter und arbeitet jeden im Namen seines Mandanten ab (dieselbe Falle wie
  beim Workflow-Runner: `useTenantFilter:false` ist nicht automatisch filterfrei).
- **Mehrere Instanzen:** Die Meldung geht genau einmal raus. `CompletionNotifiedUtc` wird atomar gesetzt
  (`UPDATE … WHERE CompletionNotifiedUtc IS NULL`), und nur wer die Zeile bekommt, ruft den Beobachter.
  Dasselbe gilt für `ApplyAsync` und den Webhook — alle drei Wege melden über dieselbe Stelle.

**Einstellungen** (in `TenantPaymentsOptions`):

| Einstellung | Vorgabe | Bedeutung |
|---|---|---|
| `ReceiptWaitSeconds` | 60 (höchstens) | Frist ab „bezahlt“, nach der auch ohne Beleg gemeldet wird |
| `ReceiptPollSeconds` | 5 | Abstand, in dem der Dienst die offenen Meldungen prüft |

Beim Agent-Weg tritt der Fall kaum auf: ohne Webhook gibt es keinen Wettlauf, der Verkauf wird erst mit
der Antwort der Kasse bezahlt. Der Dienst deckt ihn trotzdem mit ab.

### 2. wallee liefert heute keinen Beleg — das ist ein Fehler

`WalleeTerminalPaymentService` setzt `TerminalPaymentOutcome.Receipt` nirgends. Payrexx, Stripe und
der lokale wallee-Weg über den Agenten (`WalleeLtiTerminalDevice`) liefern einen. Die wallee-SDK
(10.4.0) bietet `GetPaymentTransactionsIdTerminalReceipts` (gerenderte Terminal-Belege) und
`GetPaymentTerminalsTransactionSummariesIdReceipt`. Das ist nachzuziehen.

**Grundsatz:** Belegangaben werden **immer** gespeichert, wenn der Anbieter sie liefern kann. Ein Weg
ohne Beleg ist ein Fehler, nicht eine Eigenheit.

**Entschieden:** Liefert ein Anbieter den Beleg schon gerendert, wird er **so** abgelegt und nicht in
Felder zerlegt. `TerminalReceipt` bekommt dafür ein Feld für den gerenderten Beleg (Text bzw. Format
laut `TerminalReceiptFormat`); die Einzelfelder bleiben dann leer.

### 3. Trinkgeld: ob es auf den Beleg gehört, ist eine Einstellung

`TipMinor` geht heute auf demselben Weg verloren wie `Receipt` (`DescribeAsync` liefert es nicht).
Gespeichert wird es mit den Belegangaben.

**Entschieden:** Ob es auf dem Beleg erscheint, ist eine Einstellung **am Gerät**. Eine Anbindung, die
Trinkgeld kennt, deklariert dafür einen Schalter (`SettingKind.Boolean`, z.B. `tipOnReceipt`) — Cloud-Wege
in `DescribeSettings`, Agent-Geräte in ihren Gerätefeldern. Der Assistent zeigt ihn damit von selbst;
eine Anbindung ohne Trinkgeld hat keinen Schalter. Die Anbindung liest ihn beim Aufbau des
`TerminalReceipt` und setzt ihn um.

Stand heute:

| Anbindung | Trinkgeld |
|---|---|
| Payrexx | ja (`TipAmount`) |
| wallee über den Agenten (`WalleeLtiTerminalDevice`) | ja |
| Stripe Terminal | nicht ausgewertet — Stripe kennt Trinkgeld am Lesegerät; beim Umsetzen nachziehen |
| wallee Cloud | nicht ausgewertet — beim Umsetzen prüfen, ob wallee es dort meldet |

### 4. Schema-Änderung beim Host

`TenantSale` liegt im Kontext des Hosts. Die neue Spalte (bzw. 1:1-Tabelle) und das Merkmal aus
Ergänzung 1 brauchen in **jedem** Host eine Migration — als Breaking-Hinweis in den Leitfaden.

### 5. Was gespeichert werden darf

Abschliessend die Felder von `TerminalReceipt` (Marke, maskierte Kartennummer, Autorisierung, AID,
Anwendungsname, Verifikationsart, Händler- und Terminal-Id, Referenz, Zeitpunkt) plus Trinkgeld.
**Nie** die volle Kartennummer, Spurdaten oder Prüfnummern. Die Anbindungen liefern die Nummer schon
maskiert; die Ablage verlässt sich nicht darauf, sondern verwirft eine Nummer, die nicht maskiert
aussieht, und protokolliert das als Fehler.
