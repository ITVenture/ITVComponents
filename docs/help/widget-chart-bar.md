# Balkendiagramm (`bar`, `stackedBar`)

Für Vergleiche über Kategorien — und, anders als beim Tortendiagramm, mit **mehreren Serien**.

## Eine Serie

```sql
SELECT Monat, SUM(Umsatz) AS Umsatz FROM Auftraege GROUP BY Monat ORDER BY Monat
```

```jsonc
{
  "type": "bar",
  "labels": {{ json (column Rows "Monat") }},
  "series": [ { "name": "Umsatz", "data": {{ json (column Rows "Umsatz") }} } ],
  "height": "300px"
}
```

## Mehrere Serien

Die Abfrage liefert je Kategorie eine Zeile und je Serie eine Spalte:

```sql
SELECT Monat, SUM(Offen) AS Offen, SUM(Erledigt) AS Erledigt
FROM Auftraege GROUP BY Monat ORDER BY Monat
```

```jsonc
{
  "type": "bar",
  "labels": {{ json (column Rows "Monat") }},
  "series": [
    { "name": "Offen",    "data": {{ json (column Rows "Offen") }} },
    { "name": "Erledigt", "data": {{ json (column Rows "Erledigt") }} }
  ],
  "chartOptions": { "chartPalette": ["#ff7043", "#66bb6a"] },
  "canHideSeries": true
}
```

`canHideSeries` macht die Legende klickbar: eine Serie lässt sich damit aus- und wieder einblenden.

In CScript dasselbe:

```csharp
{
  type: ChartType.Bar,
  labels: column(Rows, "Monat"),
  series: [ { name: "Offen",    data: column(Rows, "Offen") },
            { name: "Erledigt", data: column(Rows, "Erledigt") } ],
  canHideSeries: true
}
```

## Gestapelt

`"type": "stackedBar"` — dieselbe Deklaration, die Serien werden übereinander statt nebeneinander gezeichnet.
Sinnvoll, wenn die Summe eine Bedeutung hat; sonst bleibt `bar` besser lesbar.

## Achsen und Zwischenwerte

Feineinstellungen wie Achsenbeschriftung oder Gitterlinien liegen in `chartOptions`. Welche es in der
eingesetzten Fassung gibt, zeigt im Editor der Knopf **„Insert parameters"** — er schreibt die Felder mit
ihren Typen als Kommentar unter die Konfiguration.

## Hinweis zur Länge

Jede Serie muss so viele Werte haben, wie es Beschriftungen gibt. Fehlt in einer Zeile die Spalte, steht
dort `null` und wird als 0 gezeichnet — die Kategorien verschieben sich also nicht.
