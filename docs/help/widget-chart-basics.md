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

Alles Weitere wird **an die Diagramm-Komponente durchgereicht** (`width`, `height`, `legendPosition`,
`chartOptions`, …). Welche Namen es gibt und was sie erwarten, zeigt im Editor der Knopf
**„Insert parameters"** — er hängt die Liste als Kommentar unter die Konfiguration. Diese Liste kommt aus
der Komponente selbst und ist damit immer der Stand der eingesetzten Fassung.

`labels` und die Werte einer Serie müssen **gleich lang** sein. Passt etwas nicht — ein unbekannter Typ,
ein Wert, der keine Zahl ist, ein Parameter, den es nicht gibt —, sagt die Kachel es und zeichnet nicht
irgendetwas Halbes.

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
