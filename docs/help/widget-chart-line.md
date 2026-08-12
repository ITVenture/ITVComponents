# Liniendiagramm (`line`)

Für Verläufe. Aufbau wie beim Balkendiagramm: `labels` sind die Punkte auf der Zeitachse, jede Serie eine
Linie.

```sql
SELECT Tag, SUM(Menge) AS Menge FROM Bewegungen GROUP BY Tag ORDER BY Tag
```

```jsonc
{
  "type": "line",
  "labels": {{ json (column Rows "Tag") }},
  "series": [ { "name": "Menge", "data": {{ json (column Rows "Menge") }} } ],
  "height": "280px"
}
```

Mehrere Linien = mehrere Einträge in `series`, wie beim Balkendiagramm.

## Datum als Beschriftung

Die Beschriftungen sind **Text** — wie ein Datum aussieht, entscheidet also die Abfrage. Das ist Absicht:
so bestimmt derjenige das Format, der die Daten kennt.

```sql
SELECT FORMAT(Tag, 'dd.MM.') AS Tag, SUM(Menge) AS Menge
FROM Bewegungen GROUP BY FORMAT(Tag, 'dd.MM.'), Tag ORDER BY Tag
```

Bei vielen Punkten lohnt es sich, nur jede n-te Beschriftung zu setzen (leerer Text für die übrigen) —
sonst überlagern sie sich.

## Lücken

Fehlt für einen Tag eine Zeile, fehlt der Punkt nicht, sondern **verschiebt** die Linie: gezeichnet wird
Wert für Wert entlang der Beschriftungen. Wenn die Reihe lückenlos sein soll, muss die Abfrage die Lücken
füllen (Kalendertabelle o. ä.).

## Nicht geprüfte Diagrammarten

`timeseries`, `heatMap`, `sankey`, `scatterPlot`, `radar` und `rose` nimmt die Deklaration an, weil die
Diagramm-Komponente sie kennt — sie sind mit diesem Aufbau aber **nicht erprobt** und erwarten teilweise
anders geformte Daten. Wer sie braucht, sollte mit einem kleinen Beispiel anfangen.
