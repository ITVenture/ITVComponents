# Diagnostics 01: Dictionary-Ergebnis einer Diagnostics-Query wird als `[{Key,Value}]` statt als Objekt geliefert

**Typ:** Bug (Regression 5.x)
**Betrifft:**
- `ITVComponents.WebCoreToolkit.ServiceShared\Diagnostics\DiagnosticsQueryService.cs` (`Execute`, `EnumerateAndDispose`)
- indirekt: `ITVComponents.WebCoreToolkit.Net\Handlers\DiagnosticsHandler.cs` (`Results.Json(result, …)`, Zeile ~58)
- zu prüfen: `ITVComponents.WebCoreToolkit.Blazor.MudBlazor\SharedComponents\Widgets\DashboardWidgetRunner.cs` (`Load`, zählt das Ergebnis in `rows` auf)

## Problem

Eine Diagnostics-Query kann statt einer Zeilenfolge ein `IDictionary` zurückgeben, z.B. eine Widget-/Grid-Konfiguration:

```csharp
var retVal = new Dictionary<string, Dictionary<string, object>[]>
{
    {"Model", model},
    {"Columns", columns}
};
return retVal;
```

Bis 4.x wurde das von `/Diagnostics/{query}` als JSON-**Objekt** geliefert (`{"Model":[…],"Columns":[…]}`), und Konsumenten
greifen entsprechend zu (`cfg.Model[0]`, `cfg.Columns`).

Seit 5.x reicht `DiagnosticsQueryService.Execute` jedes Ergebnis durch `EnumerateAndDispose`:

```csharp
return EnumerateAndDispose(dataSource, dataSource.RunDiagnosticsQuery(...));

private static IEnumerable EnumerateAndDispose(IDisposable owner, IEnumerable source)
{
    try { foreach (var item in source) yield return item; }
    finally { owner.Dispose(); }
}
```

Ein `Dictionary<,>` ist selbst `IEnumerable`. Der Iterator macht daraus eine Folge von `KeyValuePair<,>`, die
Dictionary-Form geht verloren, und System.Text.Json serialisiert das als Array:

```json
[{"Key":"Model","Value":[…]},{"Key":"Columns","Value":[…]}]
```

Die verschachtelten Dictionaries bleiben unverändert und werden weiterhin korrekt als Objekte geschrieben. Deshalb
sieht der Fehler auf den ersten Blick nach einer falschen Serializer-Konfiguration aus, ist aber keine.

**Beobachtet in:** ESAG-PartnerPortal (Port auf .NET 10 / ITVComponents 5.x), Dashboard-Widget `orderHistory`. Das
Template erwartet `cfg.Model[0]` und bricht ab.

## Vorschlag

In `DiagnosticsQueryService.Execute` Ergebnisse, die keine Zeilenfolge sind, **nicht** durch den Iterator schicken:

- Ist das Ergebnis ein `IDictionary` (nicht-generisch, deckt `Dictionary<,>`, `ConcurrentDictionary<,>` usw. ab), wird es
  **sofort** in ein neues `Dictionary<string, object>` kopiert (Schlüssel per `ToString()`). Danach wird die DataSource
  sofort disposed und die Kopie zurückgegeben. Damit bleibt die Objekt-Form erhalten, und die Freigabe der DataSource ist
  deterministisch.
- Alle anderen Ergebnisse laufen wie bisher über `EnumerateAndDispose`.

Skizze:

```csharp
var result = dataSource.RunDiagnosticsQuery(query, context.User, context.Services, arguments);
if (result is IDictionary dict)
{
    try
    {
        var copy = new Dictionary<string, object>(dict.Count);
        foreach (DictionaryEntry e in dict)
        {
            copy[e.Key?.ToString() ?? string.Empty] = e.Value;
        }

        return copy;
    }
    finally
    {
        dataSource.Dispose();
    }
}

return EnumerateAndDispose(dataSource, result);
```

**Bewusste Einschränkung:** Die Kopie ist flach. Werte, die selbst lazy auf den Kontext der DataSource zeigen
(`IQueryable`, nicht materialisierte LINQ-Ausdrücke), sind nach dem Dispose ungültig. Für Config-artige Ergebnisse (Arrays,
anonyme Objekte, Strings) spielt das keine Rolle. Das gehört in die XML-Doku von `Execute` bzw. `IDiagnosticsQueryService`:
Wer ein Dictionary zurückgibt, muss die Werte materialisieren.

## Mitzuprüfen

- **Blazor-Pfad:** `DashboardWidgetRunner.Load` zählt das Ergebnis in eine `List<object?> rows` auf. Bei einem
  Dictionary-Ergebnis entstehen dort ebenfalls `KeyValuePair`-Zeilen. Prüfen, ob das Widget-Template-Modell das
  Dictionary als Ganzes (z.B. als einzelnes Element/`Model`) bekommen sollte, damit MVC und Blazor gleich sehen.
- **`string` als Ergebnis:** `string` ist ebenfalls `IEnumerable` und würde durch denselben Iterator zu einer
  Zeichenfolge. Gleich mitbehandeln (wie Dictionary: direkt zurückgeben, sofort disposen).

## Akzeptanzkriterien

- Eine Diagnostics-Query, die `Dictionary<string, X>` zurückgibt, liefert über `/Diagnostics/{query}` ein JSON-**Objekt**
  mit den Dictionary-Schlüsseln als Properties, wie in 4.x.
- Die DataSource wird in diesem Fall genau einmal disposed (Test mit Fake-DataSource, die Dispose zählt).
- Zeilenfolgen (LINQ-Query, Arrays, Listen) verhalten sich unverändert: Sie werden lazy gestreamt und die DataSource
  wird erst am Ende der Enumeration disposed.
- Unit-Test für `DiagnosticsQueryService` mit Dictionary-Ergebnis, das per System.Text.Json als Objekt serialisiert wird.
