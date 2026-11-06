# Torten- und Ringdiagramm (`pie`, `donut`)

Für Anteile an einem Ganzen: eine Kategorie je Zeile, **eine** Serie. Mehr als eine Serie zeichnet ein
Tortendiagramm nicht.

## Abfrage

```sql
SELECT Topic, COUNT(*) AS Anzahl FROM Tickets GROUP BY Topic
```

## Konfiguration — Scriban (`chart.scriban`)

```jsonc
{
  "type": "pie",
  "labels": {{ json (column Rows "Topic") }},
  "series": [ { "name": "Anzahl", "data": {{ json (column Rows "Anzahl") }} } ],
  "chartOptions": { "chartPalette": ["#2979ff", "#00acc1", "#ff7043", "#66bb6a"] },
  "legendPosition": "Bottom",
  "width": "100%",
  "height": "260px"
}
```

## Konfiguration — CScript (`chart.cscript`)

```csharp
{
  type: ChartType.Pie,
  labels: column(Rows, "Topic"),
  series: [ { name: "Anzahl", data: column(Rows, "Anzahl") } ],
  legendPosition: "Bottom",
  height: "260px"
}
```

## Ring statt Torte

`"type": "donut"` bzw. `ChartType.Donut` — sonst alles gleich.

## Mit Sprung auf eine gefilterte Liste

Statt der reinen Beschriftungsliste ein Objekt je Kategorie. In Scriban baut man das mit einer Schleife:

```jsonc
{
  "type": "pie",
  "labels": [
    {{ for row in Rows }}
      { "text": {{ json row.Topic }}, "navigateTo": {{ json ("Tickets?topic=" + row.Topic) }} }{{ if !for.last }},{{ end }}
    {{ end }}
  ],
  "series": [ { "name": "Anzahl", "data": {{ json (column Rows "Anzahl") }} } ]
}
```

In CScript geht dasselbe über die native Einbettung (siehe `widget-chart-cscript.md`); solange kein Sprung
nötig ist, reicht `column(…)`.

## Zahlenformat beim Überfahren

`tooltipYValueFormat` wirkt hier nicht: MudBlazor formatiert den Wert bei Torte und Ring selbst und ohne
Kultur. Steht das Feld trotzdem an der Serie, meldet die Kachel einen Fehler und zeichnet nichts.

Wenn der Betrag genau so erscheinen soll, muss er in die Beschriftung, z. B. `Karte 9.20`. Mit
`"chartOptions": { "tooltipTitleFormat": "{{Y_VALUE}}" }` zeigt der Hover dann nur diesen Text, denn bei Torte
und Ring trägt `{{Y_VALUE}}` die Beschriftung. Der Betrag steht damit auch in der Legende.

`tooltipTitleFormat` und `tooltipSubtitleFormat` an der **Serie** wirken hier nur mit
`AggregationOption.GroupByDataSet`. Sonst gelten die Werte aus `chartOptions`.

## Wenn nichts erscheint

- **Alle Werte 0** — die Wertespalte ist Text statt Zahl. Die Kachel sagt das („is not a number").
- **Eine Kategorie fehlt** — `labels` und `data` sind unterschiedlich lang; auch das steht in der Meldung.
- **Farben stimmen nicht** — `chartPalette` wird der Reihe nach vergeben, nicht je Kategorie.
