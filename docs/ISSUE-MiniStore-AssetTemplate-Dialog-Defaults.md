> **AUFGENOMMEN in PRE233**, Leitfaden §54.5a. **Pflicht-Migration: `AssetTemplates +
> ShareDialogConfig`** (nullable). Eine Vorlage ohne die Angabe verhält sich unverändert.
>
> Umgesetzt in genau der von euch vorgeschlagenen Form — dieselben Feldnamen, dieselbe
> `{ "Value": …, "Visible": … }`-Struktur, dieselbe Semantik für fehlende Angaben. Gepflegt wird es
> nicht als JSON-Text, sondern in der Vorlagen-Maske: je Feld eine Zeile mit „wird gezeigt" und
> „Vorbelegung" nebeneinander, weil die beiden nur zusammen eine Aussage ergeben.
>
> **Eure zwei offenen Fragen, beide so entschieden, wie ihr es vorgeschlagen habt:**
>
> * Ein ausgeblendetes Pflichtfeld ohne Vorbelegung ist ein **Konfigurationsfehler**. Angezeigt wird er
>   in **beiden** Masken — beim Bearbeiten der Vorlage, wo er entsteht, und beim Öffnen der Teilen-Maske,
>   wo er wirkt.
> * **`Visible: false` heisst fest**, nicht eingeklappt. Ein drittes Verhalten („vorbelegt, aber
>   aufklappbar") gibt es bewusst noch nicht; es wäre ein zweiter Begriff für einen Fall, den bisher
>   niemand hatte. Sagt Bescheid, wenn ihr ihn doch braucht.
>
> **Eine Ergänzung, die ihr nicht verlangt habt:** unlesbares JSON verhindert nichts. Es steuert die
> Bedienung und nicht den Zugriff — es gilt dann der heutige Dialog, und der Grund steht im Log. Fail
> closed wäre hier die falsche Richtung: wer wegen eines Tippfehlers gar nicht mehr teilen kann, hat ein
> grösseres Problem als eine fehlende Vorbelegung.
>
> Zu eurer Beobachtung, dass „ein Teil dieser Information schon an der Vorlage steht": `AllowAdHoc`
> bleibt die **Grenze** (ohne sie gibt es keine Tickets, egal was hier steht), `AdHoc` in dieser Angabe
> ist die **Vorbelegung**. Die beiden fallen bewusst nicht zusammen — eine Vorlage kann Tickets erlauben
> und trotzdem gespeicherte Freigaben vorschlagen.

# Issue: Die Vorlage soll den Teilen-Dialog bestimmen — sichtbare Felder und Vorgabewerte (Anstoß aus MiniStore)

**Status:** OFFEN — Wunsch, kein Fehler
**Datum:** 2026-09-12
**Quelle:** MiniStore (Kassensystem, Self-Checkout über SharedAssets)

## Der Fall

An der Kasse hängt ein `<ShareButton Path="/checkout/start" />`. Er gibt den QR-Code aus, der
dauerhaft am Ladeneingang aushängt. Für diese Freigabe ist **alles vorher bekannt**: die Vorlage
(`pos-start`), dass sie anonym ist, dass sie gespeichert wird, dass sie kein Argument führt. Übrig
bleibt genau eine Entscheidung — „ja, jetzt ausstellen".

Der Dialog verlangt trotzdem einen Titel (`Required`) und zeigt Schalter für Ad-hoc, Anonymität,
Reichweite, Empfänger und Laufzeit. Ein Ladenbesitzer, der einmal im Jahr einen neuen Aushang
druckt, muss sich dafür eine Meinung über „Temporary — store nothing" bilden.

Das ist nicht nur unbequem, es ist auch eine **Fehlerquelle**: wer versehentlich „Temporary"
ankreuzt, bekommt einen Aushang, der nach 60 Minuten tot ist — und merkt es erst, wenn Kunden
davorstehen.

## Der Wunsch

Zwei Angaben an `AssetTemplates`:

1. **Welche Bedienelemente der Dialog überhaupt zeigt.**
2. **Welchen Vorgabewert jedes davon hat** — auch für die ausgeblendeten, denn ausgeblendet heisst
   ja nicht „ohne Wert".

Damit wird aus dem Kassen-Fall: Knopf drücken → Dialog mit einem Satz und einem Knopf „Jetzt
teilen" → QR-Code. Und aus dem allgemeinen Fall (Vorlage sagt nichts) bleibt genau der heutige
Dialog.

## Welche Felder es betrifft

Aus `ShareDialog.razor`, in der Reihenfolge des Dialogs:

| Feld | heute | sinnvoller Vorgabewert an der Vorlage |
|---|---|---|
| Vorlagenauswahl | nur wenn mehrere passen | — (schon so) |
| `Title` | Pflichtfeld | Vorgabe, etwa `"Self-Checkout {Datum}"`; ausblendbar |
| `RecipientLabel` | frei | leer, meist ausblendbar |
| `AdHoc` | Schalter | folgt aus `AllowAdHoc`; wo die Vorlage es nicht erlaubt, ist der Schalter ohnehin sinnlos |
| `LifetimeMinutes` | nur bei Ad-hoc | Vorgabe; `MaxAdHocMinutes` ist bereits die Obergrenze |
| `Anonymous` | Schalter | Vorgabe; für `pos-start` immer `true` |
| `Reach` + Filter | nur wenn nicht anonym/ad-hoc | Vorgabe |

Auffällig dabei: **ein Teil dieser Information steht schon an der Vorlage** (`AllowAdHoc`,
`MaxAdHocMinutes`), wird im Dialog aber nur als Grenze verwendet, nicht als Vorgabe. Der Schritt
wäre also kleiner, als die Liste vermuten lässt.

## Vorschlag zur Form

Eine Spalte an `AssetTemplates`, JSON, nullable — wie `TrustLevelConfig` bei den
Vertrauensstellungen und `Markup` bei den Mandanten-Vorlagen. Kein Schema-Zwang für Hosts, die
nichts davon brauchen:

```json
{
  "Title":       { "Value": "Self-Checkout", "Visible": false },
  "AdHoc":       { "Value": false,           "Visible": false },
  "Anonymous":   { "Value": true,            "Visible": false },
  "Recipient":   { "Visible": false },
  "Lifetime":    { "Value": 60,              "Visible": false },
  "Reach":       { "Visible": false }
}
```

`Visible` fehlt → sichtbar wie heute. `Value` fehlt → Vorgabe wie heute. Eine Vorlage ohne die
Spalte verhält sich unverändert.

**Zwei Punkte, die wir nicht entscheiden können:**

* Ob ein ausgeblendetes Pflichtfeld ohne Vorgabewert ein Konfigurationsfehler sein soll (unsere
  Meinung: ja, und zwar beim Öffnen des Dialogs sichtbar — nicht erst beim Ausstellen).
* Ob `Visible: false` auch heissen soll **„nicht überschreibbar"**. Für den Kassenfall wäre das
  erwünscht; für andere Fälle ist „vorbelegt, aber aufklappbar" vielleicht das bessere Angebot.

## Warum das ins Toolkit gehört und nicht in den Host

Ein Host könnte einen eigenen Knopf mit eigenem Dialog bauen. Dann hätte er aber die Vorlagenwahl,
die Argumentauflösung aus dem Pfad, die Rechte- und Feature-Prüfung und den Linkbau nachgebaut —
also den halben `ShareButton`. Und beim nächsten Toolkit-Sprung stimmt die Kopie nicht mehr.

Die Angabe, wie eine bestimmte Freigabe auszustellen ist, gehört ausserdem fachlich dorthin, wo
auch `AllowAdHoc`, `MaxAdHocMinutes` und `ArgumentEnforcement` stehen: an die Vorlage.

## Umgebung

| | |
|---|---|
| Toolkit | `5.0.0-PRE232` |
| Host | .NET 10, Blazor Web App, MudBlazor 9 |
| Vorlagen | `pos-start` (gespeichert, anonym, ohne Argument), `pos-checkout` (ad hoc, 60 Min., Argument `orderId` unter `Strict`) |
