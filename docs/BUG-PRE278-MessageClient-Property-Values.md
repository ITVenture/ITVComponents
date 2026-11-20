# BUG PRE278: `GetProperty` liefert den verpackten TypedArray-String, `SetProperty` übergibt den Wert untypisiert

**Status:** behoben (2026-11-20) · **Datum:** 2026-11-20 · **Quelle:** Prototyp der .NET-4.8-Kompatibilitätsschicht (Legacy4.x), Aufrufe .NET 10 → Service

## Fall 1: `GetProperty` (nachgewiesen)

Ein .NET-10-`InMemoryClient` (lokal am Hub) liest eine Property eines Service:

```csharp
client.GetProperty("calc48", "Counter", null)
```

Erwartet wird `48`. Geliefert wird der rohe String:

```
{"$$Type":"TypedArray","Data":[{"PropertyName":"_","TypeName":"int","Data":48}]}
```

### Ursache

| Stelle | Was passiert |
|---|---|
| `MessagingShared/Server/MessageServer.cs`, Z. 362–369 | verpackt das Ergebnis: `Result = new []{retVal}.PackArguments()` |
| `MessagingShared/Client/MessageClient.cs`, Z. 135–147 | gibt `GetPropertyResponseMessage.Result` **unverändert** zurück und ruft kein `UnpackArguments().FirstOrDefault()` auf |

`ExecuteMethod` macht es richtig (`Result = ret.Result.UnpackArguments().FirstOrDefault()`, Z. 205 bzw. 245).
`GetPropertyValue` fehlt derselbe Schritt. Das betrifft auch .NET 10 untereinander, nicht nur die Anbindung
von 4.8. Die 4.8-Kompatibilitätsschicht verpackt beim Antworten genau wie der .NET-10-`MessageServer`.

### Vorschlag

```csharp
return tk.TestServerMessage<GetPropertyResponseMessage>(CheckConnected, CheckConnected)
         .ConfigureAwait(false).GetAwaiter().GetResult().Result.UnpackArguments()?.FirstOrDefault();
```

## Fall 2: `SetProperty` (aus dem Code abgeleitet, nicht reproduziert)

`SetPropertyRequestMessage.Value` ist als `object` deklariert und wird **nicht** in ein TypedArray verpackt:

| Stelle | Was passiert |
|---|---|
| `MessagingShared/Messages/SetPropertyRequestMessage.cs` | `public object Value { get; set; }` |
| `MessageClient.SetPropertyValue`, Z. ~157–164 | `Value = value`: STJ schreibt den Laufzeitwert ohne Typangabe |
| `MessageServer`, Z. ~356 | `SetProperty(…, sprm.Value, …)`: beim Lesen ist `Value` ein `JsonElement` bzw. `JsonNode`, kein `int`, `string` usw. |
| `BaseService.SetProperty` | `pfo.SetValue(target, value, index)` scheitert damit an jeder Property, die nicht `object` ist |

### Vorschlag

Entweder `Value` wie die Argumente als TypedArray-String übertragen. Das wäre eine Formatänderung, die die
4.8-Schicht nachziehen müsste. Oder serverseitig vor dem `SetValue` den `JsonElement` in den Typ der Property
umwandeln. So macht es die 4.8-Schicht in `Net10MessageServer.ConvertPropertyValue`, und das Drahtformat
bleibt unverändert. Die zweite Variante ist rückwärtskompatibel.

## Test

Im neuen InMemory- bzw. NamedPipe-Test: `GetProperty` und `SetProperty` + `GetProperty` auf einer
`int`-Property über einen echten Client, nicht nur über den lokalen Broker.

## Behebung

- **`GetProperty`:** `MessageClient.GetPropertyValue` entpackt das Ergebnis jetzt
  (`Result.UnpackArguments()?.FirstOrDefault()`). Das Drahtformat bleibt gleich.
- **`SetProperty`: `Value` bleibt, dazu kommt das neue Feld `PackedValue`.**
  - `SetPropertyRequestMessage.PackedValue` (string) enthält den Wert verpackt wie die Argumente einer Methode, also
    `new[] { value }.PackArguments()`, ein TypedArray mit einem Element. So behält der Wert seinen Typ, auch als Proxy
    (`ProxyResult`).
  - Der .NET-10-`MessageClient` schickt beides: `Value` wie bisher und `PackedValue`.
  - Der `MessageServer` nimmt `PackedValue`, wenn es gesetzt ist. Sonst nimmt er `Value`.
  - `BaseService.SetProperty` wandelt einen `JsonElement` in den Typ der Property um (`ConvertPropertyValue`, ausser
    bei `object`/`JsonElement`). Kommt der Wert also nur als `Value` an, funktioniert es trotzdem für Werte, die sich
    aus JSON lesen lassen. Proxys gehen auf diesem Weg nicht.
  - **Für die 4.8-Seite:** `Net10MessageClient.SetPropertyValue` sollte `PackedValue` mitschicken, und
    `Net10MessageServer` sollte `PackedValue` bevorzugen und `ConvertPropertyValue` als Rückfall behalten. Ohne
    `PackedValue` bleibt sie kompatibel, nur ohne Typtreue.
- **Anleitung für die 4.8-Seite, Drahtformat:**
  - `SetPropertyRequest` hat das neue, optionale Feld `PackedValue` (string). Es enthält denselben TypedArray-String
    wie `MethodArguments`, nur mit einem Element, z.B.
    `{"$$Type":"TypedArray","Data":[{"PropertyName":"_","TypeName":"int","Data":7}]}`.
  - `Net10MessageClient.SetPropertyValue`: `PackedValue` mitschicken, `Value` wie bisher daneben.
  - `Net10MessageServer`: `PackedValue` bevorzugen (entpacken, erstes Element). Ist es leer, `Value` über
    `ConvertPropertyValue` umwandeln wie bisher.
  - `Net10MessageClient.GetPropertyValue`: `GetPropertyResponse.Result` entpacken (`UnpackArguments`, erstes
    Element). Das gilt schon immer, die 4.8-Seite hat es vermutlich bereits.
- Test: `InMemoryHubTest.A_Property_Is_Read_And_Written_With_Its_Type`. Ein echter `InMemoryClient` liest und schreibt
  über die Hub-Adresse eine `int`- und eine `string`-Property.
