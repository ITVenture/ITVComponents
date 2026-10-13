# BUG PRE253: Ein In-Prozess-`MessageClient` erholt sich nie wieder, wenn er einmal „offline“ gesehen hat

**Status:** behoben (nach PRE253, siehe „Behebung“) · **Datum:** 2026-10-13 · **Quelle:** MiniStore, erster Lauf der Agent-Dienste (§72) gegen einen echten Agenten

## Der Fall

Das Web erreicht die Kassen-Agenten über `IAgentClientFactory`. Der Host baut dafür einen
`MessageClient(IServiceHubProvider, serviceName, useEvents: false)` auf dem Hub im eigenen Prozess und
puffert ihn je Dienstname, wie der Vertrag es erlaubt („sie darf ihn je Dienstname puffern“).

Ablauf am 2026-10-13, Auszug aus `SystemLog` (UTC):

```
11:38:48  Zugang Kasse1-360d… widerrufen (neu koppeln); der Agent fliegt vom Hub
11:39:05  EndPointBroker::TryUnRegisterService  Unregistering service NywH… failed: Service-connection has timed out!
11:39:55  AgentClientFactory   Opened a connection to the POS agent NywH…           <- Client entsteht, Agent ist weg
11:39:55  AgentConnector       Agent NywH… could not be reached. | Currently offline!
          at BaseClient.CheckRemoteObjectAvailability(String uniqueObjectName)
11:40:18  EndPointBroker::RegisterService  Registering service NywH… for tenant scope 45e6…   <- Agent ist zurück
11:40:22  AgentConnector       Agent NywH… could not be reached. | Currently offline!
11:42:10  … dasselbe …
11:43:35  … dasselbe, bis zum Neustart des Webs
```

Der Agent ist angemeldet, `ClientApps.LastSeenUtc` läuft weiter. Die Kasse bleibt im Web trotzdem
**dauerhaft „offline“**, bis die Anwendung neu startet.

## Ursache

`MessageClient` (`MessagingShared/Client/MessageClient.cs`):

| Stelle | Was passiert |
|---|---|
| Konstruktor `(IServiceHubProvider, string, bool)`, Z. 34–59 | legt einen `LocalServiceHubConsumer` an. **`connector` bleibt `null`**, denn nur der geschützte Konstruktor mit `IHubConnectionFactory` setzt ihn. |
| `Test()`, Z. 333–358 | Der erste Aufruf läuft durch und ruft `DiscoverService`. Ist der Dienst gerade nicht angemeldet, wird `connected = false`. |
| `Test()`, `else`-Zweig, Z. 359–371 | Jeder weitere Aufruf geht hierhin und ruft `ReConnectClient()`. |
| `ReConnectClient()`, Z. 416 ff. | `connector.CreateConnection()` mit `connector == null` wirft eine **NullReferenceException**. |
| `catch` in `Test()` | **schluckt sie still**: kein Log, `connection = null`, `connected = false`. |

Von da an endet jeder `Test()` im selben `catch`. Der Client kommt nie wieder auf `DiscoverService`,
auch wenn der Dienst längst zurück ist. Auch der `reconnector`-Timer in `BaseClient` (`PollConnection`)
läuft jedes Mal in dieselbe Wand.

Folgefehler: `Dispose(bool)` ruft `connection.Dispose()` auf dem inzwischen `null`-gesetzten Feld und
wirft damit selbst eine NullReferenceException.

Dasselbe passiert nicht nur beim allerersten Aufruf. Es reicht, dass der Agent **einmal kurz weg** ist,
zum Beispiel bei einem Neustart, einer neuen Kopplung oder einem Netzaussetzer, während das Web ihn
anfragt.

## Vorschlag

1. **In-Prozess-Wiederverbinden ohne Fabrik:** Für den `LocalServiceHubConsumer` gibt es nichts neu
   aufzubauen. Der `else`-Zweig sollte dort einfach erneut `connection.DiscoverService(targetService)`
   fragen. Alternativ merkt sich der Client im lokalen Konstruktor eine Fabrik, die einen neuen
   `LocalServiceHubConsumer` baut.
2. **Kein stiller `catch`** in `Test()`: mindestens `LogEnvironment.LogEvent(…, LogSeverity.Error)` mit
   `ex.OutlineException()`. Ohne diese Spur sieht das Fehlerbild genau aus wie „Agent ist aus“.
3. `Dispose(bool)` gegen `connection == null` absichern.
4. Ein Test im `AgentServices.Test`, der einen Dienst ab-, den Client offline-, dann den Dienst wieder
   anmeldet und `CheckRemoteObjectAvailability` erneut ruft. Der InMemory-Verteiler zeigt das
   vermutlich auch.

## Umgehung (in MiniStore umgesetzt)

`MiniStore.Web/Services/AgentClientFactory.GetClient`:

- fragt vorher `serviceHub.Broker.DiscoverService(…)`. Ist der Agent nicht angemeldet, entsteht **kein**
  Client. Die Fabrik wirft stattdessen, und der `AgentConnector` macht daraus `Unreachable`.
- verwirft einen gepufferten Client, der nicht mehr `Operational` ist, und baut ihn neu auf.
- Das `Dispose` des toten Clients wirft, wie oben beschrieben, eine Ausnahme. Die wird auf Debug
  protokolliert.

Fällt die Ursache weg, bleibt die Umgehung harmlos. Die Vorab-Prüfung kann dann entfallen.

## Behebung

| Datei | Änderung |
|---|---|
| `MessagingShared/Client/MessageClient.cs` | `Test()`: ohne `connector` (In-Prozess) fragt der `else`-Zweig nur erneut `DiscoverService`, statt neu aufzubauen. Der `catch` protokolliert (Error). `Dispose(bool)` ist gegen `connection == null` abgesichert. |
| `MessagingShared/Hub/HubConnections/LocalServiceHubConsumer.cs` | `Initialize()` setzt `initialized`. Bisher meldete jeder `Test()` eines Clients mit Events seinen Rückkanal erneut an („already registered“), und `Dispose()` meldete nie ab. |
| `MessagingShared/Hub/EndPointBroker.cs` | `UnRegisterService(ILocalServiceClient)` warf auch nach erfolgreichem Entfernen „Service is unknown!“. Es fehlte ein `return`. |
| `Shared/Base/BaseClient.cs` | `TestConnection()`: Wirft `Test()`, wird das protokolliert und eine bis eben gute Verbindung auf „offline“ gesetzt. Die alte Bedingung `!tmpConn && connectionStatus` konnte nie zutreffen. |

Folge: Ein In-Prozess-`MessageServer` meldet seinen Dienst beim `Dispose` jetzt wirklich ab, er kann also unter demselben Namen neu starten.

Tests: `ITVComponents.AgentServices.Test/LocalClientReconnectTest.cs`

- Dienst weg und wieder da
- Client entsteht, bevor der Dienst da ist
- Rückkanal wird einmal angemeldet und wieder freigegeben
- lokaler Dienst lässt sich neu anmelden

Die Umgehung in MiniStore (Vorab-Prüfung, toten Client verwerfen) kann entfallen. Sie schadet aber nicht.
