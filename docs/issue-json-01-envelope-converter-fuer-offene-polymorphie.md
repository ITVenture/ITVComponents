# Json 01: Envelope-Polymorphie auf jeder Ebene – Ablösung von `AssistedPolymorphism`

**Typ:** Verbesserung / Breaking Change (PRE-Stadium, kurze Datenmigration bei Konsumenten)
**Status:** Alle Entscheide gefallen

**Betrifft:**
- Kern (`ITVComponents\Json`):
  - `Strategy\Impl\DefaultJsonStrategy.cs`: `ToJson`/`FromJsonString`/`ReadObject`/`WriteObject`, `GetSerializer`,
    `BuildSerializerOptions`, private Einstiege `Serialize<T>`/`DeserializeObject<T>`
  - `JsonHelper.cs`: `SerializationTypingMode`
  - `Contracts\ManualTypeRegistry.cs`: additive Erweiterung um generische Typnamen (Entscheid 5)
  - `Contracts\SimpleContract.cs`, `ManualSerializationData.cs`, `DynamicContractResolver.cs`, `IManualSerializer.cs`
- IPC:
  - `ITVComponents.InterProcessCommunication.MessagingShared\Messages\ProtocolHelper\TypedArrayHelper.cs`,
    `TypedArray.cs`, `MessageTranslator.cs`
  - `ITVComponents.InterProcessCommunication.Shared\Helpers\TypedParam.cs` (inkl. `ResolvableArray<T>`),
    `TypeDescriptor.cs`, `ContractRegistration.cs`
  - `ITVComponents.InterProcessCommunication.Shared\Base\BaseService.cs` (Prüfungen `is not IManualSerializer`)
- Workflow:
  - `ITVComponents.Workflow\Serialization\WorkflowJson.cs`, `WorkflowVariableBag.cs`
  - `ITVComponents.Workflow\Model\IterationFailure.cs`
- Weitere `IManualSerializer`, die über die Brücke weiterlaufen (Entscheid 6):
  - `ITVComponents.DataAccess`: `DynamicResult`, `Linq\LinqParameter`
  - `ITVComponents.DataAccess.Remote`: `ProxyObjects\RemoteDataParameter`
  - `ITVComponents.ParallelProcessing`: `TaskBase`, `TaskScheduler.ScheduleRequest`, `DeferredScheduleRequest`,
    `PeriodScheduleRequest`
  - `ITVComponents.InterProcessCommunication.ParallelProcessing`: `ProcessPackageBase`2`, `ProcessTaskBase`2`
  - `ITVComponents.Logging.DefaultLoggers`: `LogMessageEventArgs`
- `ITVComponents.NewtonsoftJson\NewtonsoftStrategy.cs`
- `ITVComponents.Tests\BasicTests.cs` (bestehende Assisted-Tests)

**Anlass:** Port eines 3.x-Konsumenten (Archibald) auf 5.x. Dort wird mit Newtonsoft `TypeNameHandling.All` bzw.
`ToJsonStrongTyped` serialisiert. Der 5.x-Ersatz `AssistedPolymorphism` deckt davon nur die oberste Ebene ab. Dieselbe
Schwäche tragen IPC (Argumente) und Workflow (Variablen) mit sich, beide behelfen sich heute mit eigenen
`IManualSerializer`-Containern.

## Ist-Zustand

Im Modus `AssistedPolymorphism` wird ein Wert, der **kein** `IManualSerializer` ist, in einen `SimpleContract`
gesteckt (`DefaultJsonStrategy.ToJson<TProtocol>` und `ToJson(object, …)`):

```json
{
  "$$Type": "SimpleContract",
  "Data": [
    { "PropertyName": "_", "TypeName": "<Kurzname oder AssemblyQualifiedName>", "Data": { "…": "…" } }
  ]
}
```

Beim Lesen wird zuerst polymorph auf `IManualSerializer` deserialisiert. `ManualSerializationData.Data` kommt dabei als
`JsonNode` an und wird in einer zweiten Phase (`ManualSerializationData.ReadValues`, ausgelöst über
`DynamicContractResolver` → `OnDeserialized`) gegen den aufgelösten Typ deserialisiert.

Die Verwender im Einzelnen:
- **IPC:** Die Nachrichtenhülle läuft über `NativePolymorphism` mit `preserveReferences: true`
  (`MessageTranslator.TestProtocolMessage`). Die Methodenargumente werden separat als eingebetteter String verpackt
  (`TypedArrayHelper.PackArguments`: `TypedArray` über `AssistedPolymorphism`, ohne Preserve). Darin stecken
  `TypedParam`, `TypeDescriptor` und `ProxyResult` als verschachtelte `IManualSerializer`.
  `MessageServer.RestrictArgumentTypes` schränkt beim Lesen auf registrierte Typen ein.
- **Workflow:** Variablen werden als `WorkflowVariableBag` (Wurzel `"$$Type": "wf-vars"`) gespeichert und immer unter
  `RestrictManualTypesToRegistered` gelesen. Kompensationen betten jeden Variablenstand als String ein.
  `IterationFailure` ist ein eigener `IManualSerializer` wegen seines `object`-Feldes `Item`.

Die Kurznamen (`ManualTypeRegistry.Register`) und die Positivliste (`RestrictManualTypesToRegistered`, `Allow`) sind
gut und bleiben die Grundlage.

## Problem

1. **Polymorph ist nur die Wurzel bzw. der `ManualSerializationData`-Eintrag.** Darunter serialisiert System.Text.Json
   nach dem *deklarierten* Typ:
   - Eine Property `BaseFilter Filter` mit einer `DateFilter`-Instanz verliert beim Schreiben die Felder der Ableitung
     und kommt als `BaseFilter` zurück.
   - Eine Property vom Typ `object` wird mit dem Laufzeittyp geschrieben. Wegen
     `UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode` kommt sie aber als `JsonNode` zurück.
   - Gleiches gilt für Elemente von `List<BaseType>`, `object[]` und `Dictionary<string, object>`.

   Es gibt keinen Fehler, nur fehlende Felder oder `JsonNode` statt des erwarteten Typs. Newtonsoft
   (`TypeNameHandling.All`/`Auto`) hat jede Ebene typisiert.
2. **Sammlungen verlieren ihre Form.** `ManualTypeRegistry.NameOf` macht aus `List<X>` mit registriertem `X` den Namen
   `"x[]"`, und gelesen wird ein `X[]`. Im Workflow ist das dokumentiert. Bei IPC passt das nicht zu einem Parameter vom
   Typ `List<X>`.
3. **Das Format bildet den Mechanismus ab, nicht die Daten.** Abfragen per `JSON_VALUE`/`OPENJSON` oder das Lesen mit
   fremden Tools werden umständlich.
4. **Zwei Phasen mit Zwischenzustand.** Der `JsonNode`-Umweg parst und materialisiert die Nutzlast zweimal.
   `IManualSerializer` legt seinen Zustand (`Data`) während der Serialisierung **am Objekt selbst** ab
   (`OnSerializing` → `GetObjectData`). Wird dieselbe Instanz parallel serialisiert, überschreibt sie sich selbst.
5. **Referenzen über Typgrenzen gehen verloren.** Wo Preserve gebraucht würde, wurden Referenzen bisher nachträglich
   von Hand zusammengeführt.

## Entscheide

| # | Frage | Entscheid |
|---|---|---|
| 1 | Primitive in offenen Stellen | Ohne Hülle nur `string`, `bool`, `null`. **Alles andere wird umhüllt**: Zahlen, `char`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`, Enums. Das JSON wird grösser, die Typen sind exakt. |
| 2 | `ReferenceHandler.Preserve` | **Wird unterstützt**, mit einem Resolver, der über alle Hüllgrenzen eines Aufrufs geteilt wird (siehe C). |
| 3 | Positivliste als Vorgabe | Vorgabe bleibt **offen**. Die Grenzen schalten selbst ein: Workflow immer, IPC über `RestrictArgumentTypes`, Importe. Der Abgleich mit dem deklarierten Typ ist immer aktiv. |
| 4 | Namen der Hülle | **`$type` / `$value`**, siehe unten. |
| 5 | Sammlungen in offenen Stellen | Der **exakte** Typ wird geschrieben. Die Registry bekommt generische Namen (siehe D). |
| 6 | `IManualSerializer` im neuen Modus | **Brücke** für bestehende Implementierungen. IPC- und Workflow-Container werden zu POCOs umgebaut. Danach `[Obsolete]`. |
| 7 | Altdaten / gemischte Versionen | **Kein** Lesen des alten Formats, keine Format-Aushandlung. Es wird immer alles gleichzeitig aktualisiert. Konsumenten migrieren ihre Daten einmalig. |

Zusätzlich festgelegt:
- **Name des Modus:** neuer Wert `SerializationTypingMode.EnvelopePolymorphism`. `AssistedPolymorphism` wird
  **entfernt**, nicht umgedeutet. Der Compile-Fehler zeigt jedem Konsumenten, dass eine Datenmigration ansteht. Unter
  dem alten Namen fiele der Formatwechsel erst zur Laufzeit beim Lesen auf.
- **Altes Format wird laut abgelehnt.** Liegt an der Wurzel ein `$$Type` mit einem `Data`-Array, also das alte
  Assisted-Format, wirft das Lesen eine `JsonException` mit dem Hinweis auf die Migration. Es wird nicht still falsch
  gelesen.

### Entscheid 4: Namen der Hülle – `$type` / `$value`

- **Ein Name im ganzen Modus.** Baustein A nutzt mit STJs Vorgabe `$type` bereits denselben Namen
  (`ExtendNativeProtocolType`, IPC-Nachrichtenhülle). Welcher Mechanismus greift, bestimmt immer der deklarierte Typ,
  nie beide zugleich.
- **Kein Konflikt mit Preserve.** Die Hülle liest der Converter selbst. STJ sieht `$type`/`$value` nie als
  Properties eines Typs und prüft sie deshalb nicht. `$id`/`$ref`/`$values` stehen nur **innerhalb** von `$value`, wo
  STJ den Laufzeittyp normal verarbeitet. Der Spike (C) bestätigt das.
- Verworfen:
  - `$$Type`/`$$Value`: lang, und `$$` ist eine Eigenheit von uns.
  - `$t`/`$v`: schlecht lesbar in SQL und von Hand.
- SQL Server braucht im JSON-Pfad Quoting: `$."$type"`.

## Vorschlag

### A. Geschlossene Hierarchien: native Polymorphie (Ausbau des Bestehenden)

Wo die Ableitungen bekannt sind, bleibt es bei `ExtendNativeProtocolType` und `DynamicContractResolver.ConfigureType`
(`JsonDerivedType` mit Discriminator).

Neu: `BuildSerializerOptions` setzt `AllowOutOfOrderMetadataProperties = true` (ab .NET 9). Damit muss der
Discriminator nicht mehr die **erste** Property sein. Das hilft bei JSON von Hand, aus der Datenbank oder von einem
anderen Serializer. Die Option gilt global, also auch für die IPC-Nachrichtenhülle (`NativePolymorphism` + Preserve).
Sie ist dort nur toleranter. Zu prüfen ist das Zusammenspiel mit `$id`/`$ref` und die Kosten, weil Out-of-order-Lesen
das Objekt puffert.

### B. Offene Typen: Envelope-Converter (sinngemäss `TypeNameHandling.Auto`)

Gilt für alle Stellen, deren **deklarierter** Typ offen ist: `object`, abstrakte Klassen und Interfaces, die keine
Collection-Interfaces sind. Nur dort wird umhüllt. Die Wurzel bleibt das eigentliche Objekt, wenn der Protokolltyp
konkret ist:

```json
{
  "Name": "Monatsstatistik",
  "Filter": { "$type": "ssv.dateFilter", "$value": { "From": "2026-01-01", "To": "2026-01-31" } },
  "Args":   [ { "$type": "long", "$value": 42 }, "Text", true, { "$type": "list<ssv.item>", "$value": [ … ] } ]
}
```

Umsetzung als `JsonConverterFactory` in `Options.Converters` des neuen Modus. So wirkt sie auch auf Collection-Elemente
und die Wurzel:

```csharp
internal sealed class EnvelopeConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type t) =>
        (t == typeof(object) || t.IsInterface || t.IsAbstract)
        && !IsCollectionInterface(t)                 // IEnumerable<>, IList<>, IDictionary<,>, IReadOnly*, …
        && !typeof(JsonNode).IsAssignableFrom(t)     // System.Text.Json-eigene Typen nie umhüllen
        && !HasNativePolymorphism(t);                // Baustein A hat Vorrang

    public override JsonConverter CreateConverter(Type t, JsonSerializerOptions o) =>
        (JsonConverter)Activator.CreateInstance(typeof(EnvelopeConverter<>).MakeGenericType(t));
}
```

`EnvelopeConverter<TDeclared>`:

- **Schreiben**
  - `null` → `null`. `string`/`bool` → ohne Hülle (Entscheid 1).
  - Laufzeittyp genau `typeof(object)` (`new object()`): `{}` in Hülle, **ohne** erneuten Aufruf von `Serialize`.
    Sonst gibt es eine Endlosrekursion über die Factory.
  - `IManualSerializer` → Brücke (siehe E).
  - Sonst: `$type` = `ManualTypeRegistry.NameOf(rt)`, `$value` = `JsonSerializer.Serialize(w, value, rt, o)`.
    Darunter greift die Factory erneut.
- **Lesen**
  - `null` → `default`.
  - `String`/`True`/`False`: nur wenn `TDeclared` `string` bzw. `bool` aufnehmen kann, sonst `JsonException`. Kein
    `InvalidCastException`.
  - Nackte Zahl (JSON von Hand): Reihenfolge int32 → int64 → decimal → double, wie `WorkflowJson.ObjectValueConverter`.
  - Nacktes Objekt oder Array ohne Hülle: bei `object` ein `JsonNode`, sonst `JsonException`.
  - Hülle: `$type` muss nicht zuerst stehen. STJ puffert den ganzen Wert für den Converter, deshalb kann eine
    Reader-Kopie `$type` zuerst suchen. `$value` wird **genau einmal** deserialisiert, sonst wird unter Preserve
    dieselbe `$id` doppelt registriert.
  - Typ über `ManualTypeRegistry.Resolve(name, out refused)`. Kurznamen, `Allow` und Positivliste gelten unverändert.
  - Unauflösbar: bei `object` ein `JsonNode`, sonst `JsonException`. Geloggt wird immer, mit derselben Unterscheidung
    wie heute in `ManualSerializationData.ReadValues`: `refused` → Report, sonst Error (umbenannt oder entfernt).
  - Aufgelöster Typ nicht `TDeclared`-zuweisbar → `JsonException`.

Wer eine polymorphe **Wurzel** braucht (das alte `ToJsonStrongTyped`), serialisiert mit `typeof(object)` bzw. dem
Basistyp als Protokolltyp. Gelesen wird mit `FromJsonString<object>` / `FromJsonString<TBase>`.

### B2. Hülle trifft auf einen konkreten Zieltyp

Das hat der Spike gezeigt: Liest STJ eine Hülle selbst, weil der Zieltyp konkret ist und die Factory deshalb nicht
greift, gibt es zwei Fälle:
- **mit Preserve:** `JsonException` („metadata property … not supported"), also laut.
- **ohne Preserve:** ein **leeres** Objekt, still, weil unbekannte Properties übersprungen werden
  (`UnmappedMemberHandling.Skip`).

Typischer Auslöser ist `ToJson<object>(item)` gefolgt von `FromJsonString<Item>(json)`. Massnahmen:
- **Wurzel:** `FromJsonString<T>`/`ReadObject<T>` mit konkretem `T` prüfen die Wurzel. Hat sie genau die Properties
  `$type` und `$value`, wird ausgepackt. Der aufgelöste Typ muss `T`-zuweisbar sein, sonst gibt es eine
  `JsonException`.
- **Verschachtelt:** Das betrifft nur einen Wechsel des deklarierten Typs zwischen Schreiben und Lesen (Property war
  `object`, ist jetzt `Item`), also eine Schemaänderung und damit ein Fall für eine Datenmigration. Es wird
  dokumentiert und nicht abgefangen.

### C. Preserve über Hüllgrenzen (Entscheid 2)

Ein `Serialize`/`Deserialize` innerhalb eines Converters bekommt einen eigenen Stack und damit einen eigenen
Reference-Resolver. Lösung:

- **`ScopedReferenceHandler : ReferenceHandler`.** `CreateResolver()` liefert den Resolver des laufenden Aufrufs aus
  einem `AsyncLocal`. Dazu kommt ein eigener `ReferenceResolver`: Write-Map mit `ReferenceEqualityComparer` und
  Zähler, Read-Map `string → object`.
- **Scope.** `DefaultJsonStrategy` setzt ihn an den privaten Einstiegen `Serialize<T>` und `DeserializeObject<T>`
  (String und Stream). Alle öffentlichen Pfade laufen dort durch.
- **Ohne Scope → `InvalidOperationException`.** Das betrifft Options, die ausserhalb der Strategie benutzt werden. Ein
  frischer Resolver pro Aufruf würde beim Schreiben **still** Kopien statt Referenzen erzeugen.
- **Ergebnis.** Erstes Auftreten bekommt `$id`, auch innerhalb von `$value`. Spätere Vorkommen über Hüllgrenzen hinweg
  werden zu `$ref`. Zyklen funktionieren.
- **Spike (2026-11-24, eigenständige Konsolen-App, net10.0): bestätigt.**
  - Geteilte Objekte in zwei offenen Stellen, Zyklen über Hüllen, Referenzen von innerhalb einer Hülle auf die Wurzel,
    `$value` vor `$type` und Sammlungen mit `$values` funktionieren. Ebenso abstrakte Stellen und native Polymorphie
    zusammen mit der Hülle. Ohne Scope gibt es die erwartete Exception, und 500 parallele Läufe teilen keinen
    Resolver.
  - `ReferenceHandler.Preserve.CreateResolver()` wirft `InvalidOperationException`. Der eingebaute Resolver ist nicht
    wiederverwendbar, ein eigener ist **nötig**.
  - Ohne `AllowOutOfOrderMetadataProperties` scheitert ein `$type`, der nicht an erster Stelle steht. Mit der Option
    geht es, auch zusammen mit dem eigenen Handler.
  - `$ref` innerhalb von `$value` (`{"$type":"x","$value":{"$ref":"2"}}`) wird von STJ selbst aufgelöst. Die Hülle muss
    Referenzen **nicht** selbst schreiben.
  - `$type`/`$value` sind unkritisch, solange der Converter die Hülle liest. STJs eigene Objektverarbeitung reagiert
    darauf (siehe B2).
- **Folgerungen für den Converter:**
  - Ein `$ref` auf ein Objekt anderen Typs wirft in STJ eine `InvalidOperationException`, keine `JsonException`. Der
    Converter fängt sie um das `Deserialize` von `$value` und wirft sie als `JsonException` (mit innerer Exception)
    weiter.
  - Unbekannter Typ in einer `object`-Stelle → `JsonNode`: Ein `$id` innerhalb dieser Nutzlast wird dann nicht
    registriert. Ein späterer `$ref` darauf scheitert laut (`Unresolved $ref`). Das wird so akzeptiert und
    dokumentiert.

### D. Generische Typnamen in der Registry (Entscheid 5)

Additive Erweiterung von `ManualTypeRegistry`:
- Generische Definitionen lassen sich anmelden. Ab Werk sind angemeldet: `List<>` → `list`, `HashSet<>` → `set`,
  `Dictionary<,>` → `dict`. Die Liste ist erweiterbar.
- `NameOf` setzt den Namen aus Definition und Argumenten zusammen: `list<ssv.item>`, `dict<string,ssv.item>`.
  `x[]` bleibt **nur** für echte Arrays.
- `Resolve` zerlegt den Namen wieder. Unter Positivliste müssen die Definition **und** jedes Argument bekannt sein.
- Ist ein Teil nicht registriert, wird der AQN des ganzen Typs geschrieben, wie heute.

### D2. Versionstolerante Typnamen (AQN wie bei Newtonsoft)

Ist-Zustand: Ohne Positivliste löst `ManualTypeRegistry.Resolve` über ein schlichtes `Type.GetType(aqn)` auf.
- Neuere Version zur Laufzeit als gespeichert: funktioniert unter .NET (Core). Der Default-Loader bindet bei vorhandener
  Version ≥ angeforderter, und `AssemblyResolver` lädt Plugin-Assemblies über das `Resolving`-Event ohne
  Versionsprüfung.
- Ältere Version zur Laufzeit (Rollback), geänderter PublicKeyToken oder ein Typ in einem anderen `AssemblyLoadContext`:
  `null`. Das wird als „umbenannt/entfernt" geloggt, der Wert bleibt untypisiert.
- Newtonsofts Binder war toleranter: Er sucht notfalls alle geladenen Assemblies nach dem **einfachen Namen** ab. Der
  Pfad mit Positivliste (`FindKnown`) vergleicht bei uns bereits ohne Version.

Änderung:
- **Lesen:** `Type.GetType(name, assemblyResolver, typeResolver)`.
  - Der Assembly-Resolver sucht zuerst die geladenen Assemblies über alle `AssemblyLoadContext.All` nach einfachem
    Namen ab. Version, Culture und Token werden ignoriert.
  - Danach fällt er auf `AssemblyResolver.FindAssemblyByName` zurück.
  - Generische Argumente zerlegt die Überladung selbst.
- **Schreiben:** `Namespace.Typ, Assembly` ohne Version, Culture und Token, rekursiv auch für generische Argumente.
  Kürzer und über Versionen hinweg identisch. Es ist dieselbe Form, die `FindKnown` vergleicht.
- AQNs in gespeicherten Daten sind zulässig. Für langlebige Daten bleiben **Kurznamen empfohlen**, weil ein AQN beim
  Umbenennen oder Verschieben von Klasse, Namespace oder **Assembly** bricht, bei Newtonsoft genauso. Das betrifft
  insbesondere die Paket-Konsolidierung, bei der Typen in andere Assemblies umziehen.

### E. Brücke für `IManualSerializer` (Entscheid 6)

Ein `IManualSerializer` in einer offenen Stelle oder an der Wurzel wird so geschrieben:

```json
{ "$type": "<Name>", "$value": { "<PropertyName>": <offene Stelle>, … } }
```

- Jeder Eintrag aus `GetObjectData` wird als `object` geschrieben und damit selbst umhüllt.
- Doppelte `PropertyName` → `JsonException`. Heute macht das nur `TypedArray` (`"_"`), und das wird ersetzt.
- Lesen: Die Einträge werden direkt typisiert gelesen und in `Data` gelegt, dann folgt `ApplyObjectData`. Es gibt
  keinen `JsonNode`-Umweg mehr, und `ManualSerializationData.ReadValues` entfällt.
- Der Name kommt aus `ManualTypeRegistry`. Die bisherigen `ConfigureType`-Discriminatoren (`IPC_TypedParam`,
  `wf-vars`, `TypedArray`, …) werden dafür nicht mehr gebraucht.
- Das Thread-Problem (Zustand in `Data` am Objekt) bleibt für Brücken-Typen bestehen. Deshalb gilt der Umbau zu POCOs
  als Ziel.

### F. Umbau IPC

- `TypedArrayHelper.PackArguments`/`UnpackArguments`: `object[]` direkt im neuen Modus. `TypedArray` entfällt,
  `MessageTranslator.RegisterMessages` ebenso.
- `TypedParam`, `ResolvableArray<T>`, `TypeDescriptor`: zu POCOs umbauen, mit `object`-Feld für den Wert und
  `[JsonInclude]` für bisher private Felder. Die Sonderlogik in `TypedParam.ApplyObjectData` (Lesen gegen `NullType`)
  ist mit exakten Typen in der Hülle überflüssig. Das muss am Code geprüft werden.
- `ContractRegistration`: `Allow` für die Transporttypen bleibt. `ConfigureType` entfällt.
- `BaseService` (Zeilen mit `is not IManualSerializer`): prüfen, ob die Unterscheidung nach dem Umbau noch nötig ist
  oder auf die konkreten Transporttypen zielen muss.
- Die Nachrichtenhülle bleibt `NativePolymorphism` mit Preserve und profitiert nur von Baustein A.

### G. Umbau Workflow

- `WorkflowVariableBag` entfällt. `SerializeVariables`/`DeserializeVariables` serialisieren `Dictionary<string, object>`
  im neuen Modus. Lesen weiterhin unter `RestrictManualTypesToRegistered`, und der `ValueHandle`-Riegel bleibt.
- `IterationFailure` wird zum POCO. `Item` ist eine offene Stelle.
- `WorkflowJson.Materialize` bleibt für unauflösbare Werte (`JsonNode`).
- Doku von `RegisterVariableType`: Sammlungen kommen künftig in ihrem **exakten** Typ zurück, nicht mehr als Array.
- **Zu prüfen:** `WorkflowJson.Serialize`/`Deserialize` (Definitionen, Knoten-Konfiguration) mit eigenen Options und
  `ObjectValueConverter`. Die dokumentierte Grenze „verschachtelte Objekte kommen als `JsonElement`" liesse sich mit
  dem neuen Modus aufheben. Dagegen steht, dass der Export typnamen-unabhängig sein soll. Mit Kurznamen ist das
  gegeben, ohne sie stünden AQNs im portablen Format.

### H. Newtonsoft

`NewtonsoftStrategy` lehnt `EnvelopePolymorphism` mit `NotSupportedException` ab und serialisiert nicht still
statisch. IPC und Workflow nutzen immer die Standardstrategie (Auswahl pro Aufruf über den Strategienamen) und sind
nicht betroffen.

## Phasen

1. **Spike:** C (geteilter Resolver, Out-of-order, `$type`/`$value` unter Preserve). **Erledigt**, Ergebnis siehe C.
2. **Kern:** **Erledigt** (2026-11-24, `ITVComponents.Tests` 34/34 grün, davon 21 neu in
   `EnvelopePolymorphismTests`). Nicht automatisiert getestet: ein Typ aus einem eigenen `AssemblyLoadContext` und die
   Ablehnung durch Newtonsoft (das Testprojekt referenziert die Strategie nicht). `$type`-Werte werden mit
   `UnsafeRelaxedJsonEscaping` geschrieben, sonst stünde `list<x>` im JSON; die Nutzdaten maskiert STJ
   unverändert. Inhalt: neuer Modus, Factory und Converter (B), Preserve (C), Registry-Generika (D),
   versionstolerante Typnamen (D2), Brücke (E),
   `AllowOutOfOrderMetadataProperties` (A), Newtonsoft (H), Ablehnung des alten Formats. Tests im Kern.
3. **IPC** (F). **Erledigt** (2026-11-24): Argumente als `object[]` im neuen Modus, `TypedArray` und
   `MessageTranslator.RegisterMessages` entfallen, `TypedParam`/`ResolvableArray<T>`/`TypeDescriptor` sind POCOs
   (`[JsonInclude]` auf den Feldern), `TypeDescriptor` löst ohne Positivliste versionstolerant auf,
   `ResolvableArray<>` ist zugelassen. Die Zusage aus Leitfaden §110.2/§112.2 bleibt: Unter `RestrictArgumentTypes`
   kommt ein Argument unregistrierten Typs als `null` an. Der Draht liefert dafür einen `JsonNode`, und
   `BaseService.DropRefusedArgument` macht daraus `null` mit Warnung im Log (Methodenargumente, Werte aus
   `TypedParam`, Property-Wert). Die Prüfungen `is not IManualSerializer` in `BaseService` bleiben für die
   Brücken-Typen bestehen. Solution baut, Tests grün: NamedPipe 22, AgentServices 47, UpdateService 100,
   Workflow.Plugins.Ipc 5, ITVComponents.Tests 34.
4. **Workflow** (G). **Erledigt** (2026-11-24): Der Variablen-Stack ist ein `Dictionary<string, object>` im neuen
   Modus. Nicht aufloesbare Werte gehen weiter durch `Materialize`. `WorkflowVariableBag` entfaellt,
   `IterationFailure` ist ein POCO. Das alte Format wird abgelehnt. Neue Tests stehen in `WorkflowVariableJsonTest`.
   Auch die Definitionen sind umgezogen. Entscheid: Wo Daten auftreten koennen, die sich nicht zweifelsfrei einem Typ
   zuordnen lassen, wird umgezogen. Das trifft zu auf `ActivityInputBinding.Literal`,
   `AutomatedActivityNode.Configuration` und die Startwerte des zeitgesteuerten Starts. Aus dem Code, aus Importen
   oder ueber den IPC-Katalog kamen dort `long`, `double`, `DateTime`, `Guid` und Enums verfaelscht zurueck.
   - `WorkflowJson` behaelt seine eigenen Options. Das portable Format mit den `kind`-Diskriminatoren bleibt.
   - Neu haengt `JsonHelper.UseEnvelopePolymorphism(options)` nur die Huelle an. Der `ObjectValueConverter` entfaellt.
   - `WorkflowJson.Deserialize` liest unter der Positivliste.
   - Bestehende Definitionen bleiben lesbar: Blanke Zahlen werden nach der Staffel int, long, decimal, double
     gelesen, und Strings und Bools stehen weiter blank.

   Solution baut, alle Workflow-Testprojekte gruen (756 Tests inkl. `ITVComponents.Tests`).
5. **Erledigt** (2026-11-24): Enum-Nummern sind fest (0/2/3). `ConfigureType` bleibt, weil `JsonSettings` den
   Resolver nutzt. `IManualSerializer` haengt nicht mehr an den STJ-Callbacks. Es bleiben sieben
   Obsolete-Warnungen an den restlichen Implementierungen, als Erinnerung fuer den Folgeumbau. Solution baut, 11
   Testprojekte gruen. **Abbau:** `AssistedPolymorphism`, `SimpleContract`, der Zwei-Phasen-Pfad in `ManualSerializationData` und die
   `IManualSerializer`-Sonderbehandlung in `DynamicContractResolver` werden entfernt. `IManualSerializer` wird
   `[Obsolete]`, mit Verweis auf POCO + offene Stellen.
6. **Erledigt:** `Migration-Future_10-MLM.md` §125, `Migration_4.x-5.x.md` Abschnitt 7. **Migrationshinweis** für Konsumenten: gespeicherte Daten im Assisted-Format (Workflow-Variablen und
   -Kompensationen, eigene Ablagen) einmalig umschreiben oder verwerfen.

## Tests (`ITVComponents.Tests`)

Kern:
- Ein abgeleiteter Typ in einer Property mit Basistyp, in `object`, `List<Base>`, `object[]` und
  `Dictionary<string, object>`: Laufzeittyp und alle Felder bleiben erhalten.
- Verschachtelung über drei Ebenen, mit einer Hülle innerhalb einer Hülle.
- Konkreter Protokolltyp: Die Wurzel hat keine Hülle. `typeof(object)` als Protokolltyp: Die Wurzel hat eine Hülle.
- `$value` steht vor `$type` und wird korrekt gelesen.
- Primitive in `object`: `int`, `long`, `decimal`, `double`, `char`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`
  und ein Enum kommen exakt zurück. `string`/`bool`/`null` stehen ohne Hülle im JSON.
- Nackte Zahl, nacktes Objekt und String in einer Interface-Stelle (JSON von Hand): Regel bzw. `JsonException`.
- `new object()` in einer offenen Stelle: keine Rekursion.
- Sammlungen: `List<X>`, `HashSet<X>`, `Dictionary<string, X>`, `X[]` in `object` kommen im exakten Typ zurück. Mit
  registriertem `X` wird ein Kurzname wie `list<x>` geschrieben.
- Positivliste: Kurzname wird geschrieben, nicht der AQN. Ein AQN in den Daten wird abgelehnt. Bei generischen Namen
  wird ein unbekanntes Argument abgelehnt.
- Typnamen ohne Kurznamen:
  - Geschrieben wird `Namespace.Typ, Assembly` ohne Version, auch für generische Argumente.
  - Ein gespeicherter Name mit **abweichender** Version (höher und tiefer) sowie ohne Version wird aufgelöst.
  - Ein Typ aus einer Plugin-Assembly in einem eigenen `AssemblyLoadContext` wird gefunden.
- Unbekannter oder umbenannter Typ: bei `object` ein `JsonNode`, sonst `JsonException`. Beide Fälle erzeugen einen
  Log-Eintrag mit der richtigen Stufe.
- Aufgelöster Typ passt nicht zum deklarierten: `JsonException`.
- `$ref` in einer Hülle zeigt auf ein Objekt anderen Typs: `JsonException`, keine `InvalidOperationException`.
- Hülle an der Wurzel, gelesen mit konkretem `T`: wird ausgepackt. Mit unpassendem Typ: `JsonException` (B2).
- Preserve:
  - Dasselbe Objekt in zwei offenen Stellen → `$ref`, nach dem Lesen `ReferenceEquals`.
  - Zyklus über eine Hülle hinweg.
  - Referenz von innerhalb nach ausserhalb einer Hülle und zurück.
  - Options ohne Scope → `InvalidOperationException`.
- Nebenläufigkeit: dasselbe Objekt (POCO) parallel serialisiert ergibt identische Ergebnisse. Parallele Aufrufe mit
  Preserve teilen keinen Resolver.
- Brücke: ein `IManualSerializer` (z.B. `DynamicResult`) mit Rundreise. Doppelte `PropertyName` → `JsonException`.
- Altes Assisted-Format → `JsonException` mit Migrationshinweis.
- Newtonsoft + `EnvelopePolymorphism` → `NotSupportedException`.
- Die bestehenden Assisted-Tests in `BasicTests` werden auf den neuen Modus umgestellt (Listen, Arrays, Alias,
  Positivliste).

IPC:
- Rundreise von Argumenten mit abgeleiteten Typen, `List<T>`-Parametern, Primitiven und `TypedParam` mit Proxy.
- `RestrictArgumentTypes`: unregistrierter Argumenttyp wird abgelehnt, Transporttypen (`Allow`) gehen durch.
- Nachrichtenhülle mit Preserve und Out-of-order-Discriminator.

Workflow:
- Variablen-Rundreise mit registrierten POCOs, Sammlungen und Primitiven.
- Unregistrierter Typ unter der Positivliste kommt untypisiert zurück und wird geloggt.
- `IterationFailure` mit polymorphem `Item`.
- Kompensationen mit eingebettetem Variablenstand.

## Abgrenzung

- Das Lesen von Newtonsoft-Daten (`$type` aus `TypeNameHandling`) gehört **nicht** in dieses Issue. Konsumenten mit
  solchen Beständen lösen das bei ihrer Portierung selbst.
- Das Lesen des alten Assisted-Formats wird bewusst nicht unterstützt (Entscheid 7).
- Die restlichen `IManualSerializer`-Implementierungen (DataAccess, ParallelProcessing, Logging) laufen über die Brücke.
  Ihr Umbau zu POCOs ist Folgearbeit.
