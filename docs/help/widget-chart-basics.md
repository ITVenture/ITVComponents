# Diagramm-Kacheln: Grundlagen

Eine Diagramm-Kachel besteht aus **zwei** Teilen, die man getrennt einstellt:

1. die **Diagnose-Abfrage** — sie liefert die Zeilen;
2. die **Konfiguration** — sie sagt, was aus diesen Zeilen gezeichnet wird.

Womit die Konfiguration geschrieben wird, entscheidet der **Renderer** in der Widget-Verwaltung:

| Renderer | Konfiguration ist … |
|---|---|
| `chart.scriban` | ein Scriban-Template, das eine **JSON**-Deklaration erzeugt |
| `chart.cscript` | ein **CScript**-Objektliteral |

Beide enden bei derselben Deklaration; die Wahl ist Geschmackssache.

## Die Deklaration

Drei Felder werden aus den Daten gebaut:

| Feld | Pflicht | Bedeutung |
|---|---|---|
| `type` | ja | `pie`, `donut`, `bar`, `stackedBar`, `line`, … |
| `labels` | ausser bei `line` | die Kategorien — Text oder `{ "text": …, "navigateTo": … }` |
| `series` | ja | Liste aus `{ "name": …, "data": [ … ] }` |

Drei weitere Felder gehören dem Rahmen und werden **nicht** an die Diagramm-Komponente durchgereicht:

| Feld | Vorgabe | Bedeutung |
|---|---|---|
| `title` | — | Überschrift über diesem Diagramm. Darf ein Kultur-Datensatz sein. |
| `minWidth` | `280` | Breite in Pixeln, unter der dieses Diagramm auf eine eigene Zeile rutscht. `0` = immer nebeneinander. Ohne Wirkung, wenn `width` eine absolute Länge ist — dann steht die Breite schon fest. |
| `action` | `select` | Name der Aktion, die ein Klick ohne `navigateTo` meldet. |

Alles Weitere wird **an die Diagramm-Komponente durchgereicht** (`width`, `height`, `legendPosition`,
`chartOptions`, …). Welche Namen es gibt und was sie erwarten, zeigt im Editor der Knopf
**„Insert parameters"** — er hängt die Liste als Kommentar unter die Konfiguration. Diese Liste kommt aus
der Komponente selbst und ist damit immer der Stand der eingesetzten Fassung.

## Eigene Größe

`width` und `height` gehören zu diesen durchgereichten Parametern — mit einer Besonderheit: eine
**absolute** `width` (`"150px"`, `"12rem"`, `150`) bestimmt nicht nur das Diagramm, sondern auch den
**Platz**, den es einnimmt. Der Platz richtet sich dann nach dem Diagramm statt nach `minWidth` und dem
freien Restplatz; eine Überschrift steht dadurch mittig über dem Diagramm und nicht über einem viel
breiteren Kasten.

Eine **relative** Angabe (`"80%"` — das ist die Vorgabe) bleibt eine Angabe über das Diagramm allein: sie
rechnet gegen den Platz, und der kann sich nicht umgekehrt nach ihr richten. Dort entscheiden weiter
`minWidth` und der verfügbare Platz.

`height` gehört immer dem Diagramm allein: wie hoch ein Platz ist, ergibt sich aus seiner **Zeile**.

## Überschriften unterschiedlicher Länge

Bricht die Überschrift eines Diagramms um und die des Nachbarn nicht, beginnen die beiden Diagramme
trotzdem auf derselben Höhe: die zusätzliche Zeile wird **über** der kürzeren Überschrift eingefügt, nicht
zwischen Überschrift und Diagramm. Dafür ist nichts einzustellen, und es gibt auch keine reservierte
Titelhöhe, die bei einzeiligen Überschriften Platz verschenken würde.

Sind die Diagramme einer Zeile **unterschiedlich hoch**, richten sich ihre Unterkanten aus — beides
zugleich geht nicht.

`labels` und die Werte einer Serie müssen **gleich lang** sein. Passt etwas nicht — ein unbekannter Typ,
ein Wert, der keine Zahl ist, ein Parameter, den es nicht gibt —, sagt die Kachel es und zeichnet nicht
irgendetwas Halbes.

**Beim Speichern wird nur die Syntax geprüft.** Ob die Konfiguration die richtigen Zahlen liefert, zeigt
sich erst mit echten Daten — deshalb blockiert der Editor nichts, was er nicht sicher beurteilen kann.
Alles Weitere steht in der Kachel selbst und im Log.

## Mehrere Diagramme aus einer Abfrage

Statt **einer** Deklaration darf dort auch eine **Liste** stehen. Die Abfrage läuft dann trotzdem nur
einmal — aus denselben Zeilen entstehen mehrere Grafiken:

```jsonc
// Scriban / JSON
[ { "title": "Nach Status", "type": "pie",  "labels": …, "series": … },
  { "title": "Verlauf",     "type": "line", "minWidth": 400, "series": … } ]
```

```csharp
// CScript
[ { title: "Nach Status", type: ChartType.Pie,  labels: column(Rows, "Status"), series: [ … ] },
  { title: "Verlauf",     type: ChartType.Line, minWidth: 400, series: [ … ] } ]
```

Wie sie sich anordnen, entscheidet der **Platz**: nebeneinander, solange jedes noch seine `minWidth`
bekommt, sonst untereinander. Das richtet sich nach der tatsächlichen Breite der Kachel — die hängt an
ihrer eingestellten Breite *und* am Fenster, ein Diagramm auf dem Telefon steht also von selbst unter dem
anderen.

Ein Fehler in **einer** dieser Deklarationen betrifft auch nur sie: an ihrem Platz steht die Meldung, die
übrigen Diagramme werden gezeichnet. Nur was die ganze Konfiguration unlesbar macht (kaputtes JSON, ein
Skript, das abbricht), ersetzt die ganze Kachel.

Bei mehreren Diagrammen lohnt sich `action`: `WidgetAction` trägt nur Name und Argument, sonst wüsste die
Anwendung beim Klick nicht, **welches** Diagramm gemeint war.

## Die Abfrage

Das Übliche ist eine Abfrage mit einer Beschriftungs- und einer Wertespalte:

```sql
SELECT Topic, COUNT(*) AS Anzahl FROM Tickets GROUP BY Topic
```

Was in den Zeilen liegt, hängt an der Datenquelle: über eine SQL-Verbindung sind es Wörterbücher, über
einen DbContext das, was die Abfrage zurückgibt. **`column(Rows, "Topic")` nimmt beides** — deshalb ist es
in beiden Sprachen der einfachste Weg an eine Spalte.

## Klick auf ein Segment

Trägt eine Beschriftung ein `navigateTo`, springt ein Klick darauf (im Diagramm oder in der Legende) auf
diese Seite:

```jsonc
"labels": [ { "text": "Offen", "navigateTo": "Tickets?status=open" }, "Erledigt" ]
```

**Ziele relativ angeben** (`Tickets?…`, nicht `/Tickets`): relative Ziele werden gegen die Basis der
Anwendung aufgelöst, und die trägt den Mandanten-Präfix. Ein Ziel, das mit `/` beginnt, geht daran vorbei.

Ohne `navigateTo` meldet die Kachel den Klick der Anwendung — was sie damit tut, entscheidet sie selbst.

## Mehrsprachige Beschriftungen

`translate(…)` löst einen Kultur-Datensatz auf, in beiden Sprachen:

```jsonc
{ "text": {{ json (translate Row.Caption) }} }   // Scriban
{ text: translate(Row.Caption) }                 // CScript
```

## Weiter

- `widget-chart-pie.md` — Torte und Ring
- `widget-chart-bar.md` — Balken, gestapelt
- `widget-chart-line.md` — Linien
- `widget-chart-cscript.md` — die Eigenheiten von CScript (Anführungszeichen, LINQ, Ausdruck vs. Block)
