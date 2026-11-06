# Issue: Diagramm-Kacheln können das Zahlenformat im Hover nicht setzen

**Status:** umgesetzt — `ChartWidgetDeclaration.ReadSeries` liest `tooltipYValueFormat`, `tooltipTitleFormat`, `tooltipSubtitleFormat` und `visible`, meldet unbekannte Serienfelder und ungültige Formate; `tooltipYValueFormat` auf `pie`, `donut`, `rose`, `radar` wird ebenfalls gemeldet. Leitfaden §97
**Datum:** 2026-11-06
**Quelle:** MiniStore-Session (Konsument), beim Bau des Verkaufs-Dashboards.
**Toolkit-Stand:** `5.0.0-PRE266`, MudBlazor `9.9.0`.

## Der Fall

Eine Balken-Kachel zeigt Geldbeträge. Beim Überfahren steht `9.2` statt `9.20` und `3.5` statt `3.50`.

MudChart kennt dafür die Eigenschaft `ChartSeries<T>.TooltipYValueFormat`. Die Deklaration einer Kachel
kann sie aber nicht setzen:

```jsonc
{
  "type": "bar",
  "labels": {{ json (column Rows "Hour") }},
  "series": [ { "name": "Umsatz CHF", "data": {{ json (column Rows "Revenue") }}, "tooltipYValueFormat": "N2" } ]
}
```

Das Feld wird heute **ohne Meldung ignoriert**, obwohl die Hilfe sagt, dass ein unbekannter Parameter in
der Kachel gemeldet wird. Das gilt aber nur für die Parameter der Diagramm-Komponente, nicht für die
Felder einer Serie.

## Die betroffene Stelle

`ChartWidgetDeclaration.ReadSeries`
(`ITVComponents.WebCoreToolkit.Blazor.MudBlazor/SharedComponents/Widgets/Charts/ChartWidgetDeclaration.cs`)
liest aus jedem Serien-Eintrag nur `name` und `data`:

```csharp
series.Add(new ChartSeries<double>
{
    Name = Convert.ToString(name, CultureInfo.InvariantCulture) ?? string.Empty,
    Data = numbers
});
```

Beide Renderer (`chart.scriban`, CScript) gehen durch diese Stelle, auf beiden Wegen fehlt das Feld also.

## Warum es in der Kachel keinen anderen Weg gibt

| Weg | Warum er nicht trägt |
|---|---|
| `chartOptions.tooltipTitleFormat` | Kennt nur die Marken `{{SERIES_NAME}}`, `{{X_VALUE}}`, `{{Y_VALUE}}`, kein Zahlenformat |
| `TooltipTemplate` | Steht im `ChartParameterBinder` auf der Sperrliste, in JSON auch nicht ausdrückbar |
| Werte in der Abfrage runden | Hilft nicht: `9.20` und `9.2` sind als `double` derselbe Wert |
| Betrag in die Beschriftung schreiben | Bei Balken steht er dann auf der Achse |

## Vorschlag

`ReadSeries` übernimmt zusätzlich mindestens `tooltipYValueFormat`, besser auch `tooltipTitleFormat` und
`tooltipSubtitleFormat` (alle drei gibt es an `ChartSeries<T>`):

```csharp
entry.TryGetValue("tooltipYValueFormat", out object? yFormat);
…
TooltipYValueFormat = Convert.ToString(yFormat, CultureInfo.InvariantCulture)
```

MudBlazor wendet das Format mit der aktuellen Kultur an (`ToString(format, null)`). `N2` ergibt damit in
MiniStore `1’234.50`, weil dort `CurrentCulture` auf `-CH` abgebildet ist.

Wünschenswert wäre ausserdem, dass ein unbekanntes Feld in einer Serie gemeldet wird wie ein unbekannter
Parameter. Sonst sieht ein Tippfehler aus wie eine fehlende Funktion.

Bitte in `docs/help/widget-chart-bar.md` und `widget-chart-line.md` mit einem Beispiel für Geldbeträge
aufnehmen.

## Ring- und Kreisdiagramme: hier hilft das Feld nicht

`Donut<T>` und `Pie<T>` in MudBlazor 9.9.0 ignorieren `TooltipYValueFormat`. Sie setzen den Wert fest mit
`ToString(null, CultureInfo.InvariantCulture)`. Das Toolkit kann daran nichts ändern, das wäre ein Thema
für MudBlazor.

Falls `docs/help/widget-chart-pie.md` das Feld erwähnt, gehört diese Einschränkung dazu.

## Umgehung in MiniStore

- **Ringdiagramme:** Der Betrag steht in der Beschriftung (`Karte 9.20`), und `chartOptions.tooltipTitleFormat`
  ist `{{Y_VALUE}}`. Bei Ring und Kreis trägt `{{Y_VALUE}}` die Beschriftung. Nachteil: Der Betrag steht
  auch in der Legende.
- **Balkendiagramme:** keine. Sie zeigen bis zur Änderung `9.2`.

Sobald das Feld kommt, ergänzt MiniStore in den Kacheln `TodayByHour`, `Last30Days` und `CategoriesMonth`
je Serie `"tooltipYValueFormat": "N2"`.
