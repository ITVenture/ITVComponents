# BUG (PRE250): `FactoryWrapper` öffnet je Aufruf einen Scope, sucht das Zielobjekt aber nur in der Hauptfabrik — Bereichs-Plugins sind über IPC nicht erreichbar

**Status:** behoben in `5.0.0-PRE251` (`f99a0565`) — der `FactoryWrapper` findet Bereichs-Plugins über `PluginFactory.DescribeScopedPlugin`, Test `DescribeScopedPluginTest`

> Gemeldet aus **MiniStore** (POS-Agent, WinForms-Tray, .NET 10, meldet sich als `GrpcServer` am Hub
> des Webs an; `ITVComponents.InterProcessCommunication.Grpc 5.0.0-PRE250`). Quellstand geprüft:
> `cleanworkspace/ITVComponents-Public` @ `d4ae7238`.

## Übersicht

| | |
|---|---|
| **Betroffen** | `ITVComponents.InterProcessCommunication.Shared/Security/FactoryWrapper.cs` — Indexer (Z. 65, 119) und `Contains` (Z. 108) |
| **Kern** | `MessageServer.ClientInvokation` öffnet je eingehender Nachricht einen Scope (`plugins.OpenScope(...)`, `MessageServer.cs` Z. 266), und `FactoryWrapper.OpenScope` merkt ihn sich in `currentScope` (Z. 157). **Gelesen wird `currentScope` aber nirgends.** Indexer und `Contains` fragen `wrapped[pluginName]` bzw. `wrapped.Contains(uniqueName)` — die Hauptfabrik, ohne aktiven Scope und ohne `triggerAsParameterRequest`. |
| **Folge** | Ein Objekt, das als Bereichs-Plugin (`PluginLoadType.Scope`) über einen `IDynamicLoader` definiert ist, ist für den Client **nicht vorhanden**: `CheckRemoteObjectAvailability` sagt „nicht verfügbar“, ein Methodenaufruf findet kein Ziel. Der Scope je Aufruf existiert, ist aber wirkungslos — bis auf die darin registrierten Objekte (`requestContext`, `services`), die nur ein Plugin sähe, das im Scope entsteht. |
| **Nicht betroffen** | Server, deren Objekte ausschliesslich Singleton-Plugins der Hauptfabrik sind. |

## Der Fall

Der POS-Agent stellt dem Web seinen Gerätedienst `IPosDeviceService` unter dem Plugin-Namen
`PosDevices` zur Verfügung. Die Idee: **alle** Geräte stehen in der Konfiguration, aber als
Bereichs-Plugins; erst das aufgerufene Objekt bestimmt, welche geladen werden. `PosDevices` ist darum
selbst ein Bereichs-Plugin, das im Scope des Aufrufs entsteht und über `$ifactory` die Geräte nach
Namen aus demselben Scope holt:

```csharp
// zweiter IDynamicLoader neben dem SettingsDrivenLoader
yield return new PluginConfigurationItem
{
    Name = "PosDevices",
    ConstructionString = "[MiniStore.PosAgent.dll]<MiniStore.PosAgent.DeviceSet>$ifactory,$terminalName",
    LoadType = PluginLoadType.Scope
};
```

Lokal (`factory.NewScope(...)` → `scope["PosDevices", true]`) funktioniert das. Über den Hub nicht:

```
FactoryWrapper.Contains("PosDevices")  → wrapped.Contains(...)   → false (nicht in pluginInstances)
FactoryWrapper["PosDevices"]           → wrapped["PosDevices"]   → null
```

Dass der Scope zu diesem Zeitpunkt offen ist, hilft nicht: `PluginFactory.NewScope` setzt
`CurrentScope` nach dem Aufbau wieder auf `null` (`WithScope` … `finally`), es gibt also keinen
„aktiven“ Scope, über den `wrapped[...]` ihn fände. Der einzige Verweis darauf ist `currentScope` im
Wrapper selbst.

## Betroffene Stellen

| Datei | Zeile | Was |
|---|---|---|
| `…Shared/Security/FactoryWrapper.cs` | 65 | Indexer: `retVal = wrapped[pluginName];` |
| `…Shared/Security/FactoryWrapper.cs` | 108, 119 | `Contains`: `wrapped.Contains(uniqueName)` / `wrapped[uniqueName]` |
| `…Shared/Security/FactoryWrapper.cs` | 157–158 | `OpenScope` setzt `currentScope` — einziger Schreiber, kein Leser |
| `…MessagingShared/Server/MessageServer.cs` | 266 | Scope je Nachricht; alle Pfade danach (`ExecuteMethod`, `CheckForAvailableProxy`, Get/SetProperty, Events) gehen über den Wrapper |

## Vorschlag

1. **Indexer:** ist `currentScope.Value` gesetzt, über den Scope auflösen — `scope[pluginName, true]`.
   Der Scope-Collector fällt für Singleton-Plugins ohnehin auf die Hauptfabrik zurück, das bisherige
   Verhalten bleibt also erhalten; neu kommen die Bereichs-Plugins dazu.
2. **`Contains`:** zusätzlich „ein `IDynamicLoader` kennt den Namen als Bereichs-Plugin“
   (`HasScopedPlugin`) — ohne zu laden, damit eine Verfügbarkeitsprüfung keine Geräte öffnet. Für
   `securityRequired` braucht es dann den Typ; wenn das nur über eine Instanz geht, ist Auflösen im
   aktuellen Scope vertretbar — die Nachricht hat ihren eigenen Scope, der danach freigegeben wird.
3. **Achtung beim Decorator-Cache:** `serviceDecorators.TryAdd(pluginName, decorator)` cacht nach
   Namen über die Lebensdauer des Wrappers. Für eine Scope-Instanz wäre das falsch — der nächste
   Aufruf bekäme den Decorator einer bereits freigegebenen Instanz. Für Treffer aus dem Scope darf
   dieser Cache nicht befüllt werden.

## Umgehung (im Host)

Das aufgerufene Objekt als **Singleton** in der Hauptfabrik laden und den Scope selbst je Methode
öffnen (`factory.NewScope(...)`, Geräte darin nach Namen auflösen, nach dem Aufruf freigeben). Das
funktioniert, verlagert aber genau die Scope-Verwaltung in den Host, die der Server schon macht.
MiniStore wartet vorerst auf die Korrektur: der Agent ist lokal (Tray) voll funktionsfähig, nur der
Zugriff aus dem Web bleibt bis dahin aus.
