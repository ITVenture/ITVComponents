# BUG PRE278: Über den InMemory-Hub kann sich kein Service anmelden – die Antwort auf `RegisterService` wird nie geschrieben

**Status:** behoben (2026-11-20) · **Datum:** 2026-11-20 · **Quelle:** Prototyp der .NET-4.8-Kompatibilitätsschicht (Legacy4.x), Gegenprobe rein in .NET 10

## Der Fall

Ein `InMemoryServer` meldet sich über den InMemory-Kanal an einem .NET-10-Hub an
(`MessageServiceHub` + `ServiceHubChannel` + `OpenHubFactory`). Beide Seiten laufen auf .NET 10,
der Fehler betrifft also nicht nur die Anbindung von 4.8.

```csharp
var hub = new MessageServiceHub();
var endPoint = new ServiceHubChannel(hubAddress, new OpenHubFactory(), hub);
endPoint.Initialize();

var viaChannel = new InMemoryServer(hubAddress, new Dictionary<string, object> { { "calc", new Calculator() } },
                                    new OpenHubFactory(), "ProtoServiceViaChannel");
viaChannel.Initialize();
```

Beobachtet (zweimal hintereinander, je nach 10 s):

```
ERROR - Reconnect changed: Type: System.TimeoutException; Message: The operation has timed out.
   at ITVComponents.Helpers.AsyncExtensions.CancelAfterAsync[TResult](...)            AsyncExtensions.cs:94
   at ...InMemory.Hub.Client.ServiceClient.RegisterService(RegisterServiceMessage)     ServiceClient.cs:76
   at ...InMemory.Hub.HubConnections.InMemoryServiceHubConsumer.InitHubConnection()    InMemoryServiceHubConsumer.cs:347
```

Gleichzeitig liefert `hub.Broker.DiscoverService("ProtoServiceViaChannel").Ok == true`. Der Hub hat die
Anmeldung also **verarbeitet**, aber der Service bekommt die Antwort nie. Damit fehlt ihm das
Session-Ticket: er ruft nie `ServiceReady`, und Aufrufe an ihn laufen ins Leere. Im Log des Hubs
erscheint **nichts**.

Ohne Belang ist der Fehler nur dort, wo der Server lokal am Hub hängt
(`new InMemoryServer(hub, …)` mit `LocalServiceHubConsumer`) und Clients nur über InMemory aufrufen.
Das deckt der Prototyp ab, und dort läuft alles.

## Ursache (hohe Wahrscheinlichkeit, nicht im Debugger nachgewiesen)

| Stelle | Was passiert |
|---|---|
| `ServiceHubChannel.ClientComm`, Z. 161–164 | `RegisterServiceMessage` → `e.Result = RegisterServiceResponseMessage` |
| `ServiceHubChannel.ClientComm`, Z. 177 | Tick → `e.Result = ServiceTickResponseMessage` |
| `MemoryServiceChannel.IncomingData`, Z. 266 | schreibt `new Response { Payload = evData.Result }`. `Response.Payload` ist als `IProtocolMessage` deklariert, also polymorph. |
| `ProtocolMessages.cs`, Z. 123–131 | `IProtocolMessage` kennt per `[JsonDerivedType]` nur `ServiceOperationResponse`, `ServerOperationRequest`, `ServiceDiscoveryResponse/-Request`, `ServiceSessionOperationRequest`, `RegisterServiceRequest`. |
| `MemoryServiceChannel`, statischer Konstruktor, Z. 82–85 | ergänzt nur `InMemRequest`, `InMemResponse`, `InMemDisconnect`, `InMemConnect`. |
| `NamedPipe/Channels/PipeMessageChannel.cs`, Z. 45–46 | **nur hier** werden `RegisterServiceResponseMessage` („PipeRegisterServiceResponse“) und `ServiceTickResponseMessage` („PipeServiceTickResponse“) registriert. |

In einem reinen InMemory-Prozess sind `RegisterServiceResponseMessage` und `ServiceTickResponseMessage`
also unbekannte abgeleitete Typen von `IProtocolMessage`. Das Attribut gibt kein
`UnknownDerivedTypeHandling` an, deshalb wirft System.Text.Json beim Serialisieren der `Response`.

**Zweiter Fehler, und der Grund, warum man nichts sieht:** `MemoryServiceChannel.IncomingData`
(Z. 229 ff.) arbeitet in einem `Task.Run` ohne `try/catch`. Die Exception landet in einem unbeobachteten
Task und wird nirgends protokolliert. Laut den globalen Regeln ist ein Log hier Pflicht.

## Vorschlag

1. **Die beiden Antworttypen für InMemory registrieren.** Achtung: Werden derselbe Typ und dieselbe Basis
   mit zwei verschiedenen Discriminatoren registriert, kollidiert das, sobald InMemory und NamedPipe im
   selben Prozess geladen sind (STJ lehnt doppelte abgeleitete Typen ab). Die sauberste Lösung ist daher:
   - `RegisterServiceResponseMessage` und `ServiceTickResponseMessage` **zentral** als
     `[JsonDerivedType]` an `IProtocolMessage` in `ProtocolMessages.cs` aufnehmen, z.B. mit
     `"RegisterServiceResponse"` und `"ServiceTickResponse"`,
   - und die beiden Zeilen in `PipeMessageChannel.cs` entfernen. Das ändert das Pipe-Drahtformat; Client
     und Hub einer Pipe-Verbindung müssen dann auf demselben Stand sein.
2. **`MemoryServiceChannel.IncomingData`**: den ganzen Rumpf des `Task.Run` in `try/catch` fassen und mit
   `LogEnvironment.LogEvent($"…{ex.OutlineException()}", LogSeverity.Error)` protokollieren,
   inklusive Kanalname und der empfangenen Nachricht.
3. **Test** (z.B. in einem neuen `InMemory.Test` oder neben `NamedPipe.Test`): Hub mit `ServiceHubChannel`,
   ein `InMemoryServer` meldet sich **über die Hub-Adresse** an, ein `InMemoryClient` ruft eine Methode auf.
   Ein zweiter Test lädt InMemory und NamedPipe im selben Prozess und prüft, dass es keine
   Discriminator-Kollision gibt.

## Bezug zur .NET-4.8-Kompatibilitätsschicht

Die 4.8-Seite (`ITVComponents.InterProcessCommunication.InMemory.Net10Compat` auf `Legacy4.x`) braucht
für die Service- und Event-Richtung dieselben Discriminatoren. **Bitte die endgültigen Namen für
`RegisterServiceResponseMessage` und `ServiceTickResponseMessage` hier unter „Behebung“ eintragen.** Die
4.8-Seite übernimmt sie dann in `Net10Wire`.

## Behebung

**Discriminatoren (endgültig, für `Net10Wire` der 4.8-Seite):**

| Typ | Discriminator an `IProtocolMessage` |
|---|---|
| `RegisterServiceResponseMessage` | `RegisterServiceResponse` |
| `ServiceTickResponseMessage` | `ServiceTickResponse` |

- Beide stehen jetzt zentral als `[JsonDerivedType]` an `IProtocolMessage` (`ProtocolMessages.cs`). Die
  Registrierungen „PipeRegisterServiceResponse“/„PipeServiceTickResponse“ in `PipeMessageChannel` sind entfernt.
  Das ändert das Pipe-Drahtformat. Bewusst so entschieden: Der Pipe-Hub war noch nirgends produktiv im Einsatz.
- `MemoryServiceChannel.IncomingData`: Der Rumpf liegt jetzt in `HandleIncoming`, und das `Task.Run` fängt jede
  Exception und protokolliert sie als Error (Kanalname, Modus, `OutlineException`). Eine Antwort auf eine nicht mehr
  offene Anfrage (Zeitüberschreitung) wird als Warning protokolliert und verworfen, statt unbeobachtet zu werfen.
- **Dritter Fehler, durch den neuen Test aufgedeckt:** System.Text.Json teilt die Typ-Infos zwischen allen
  gleichwertigen Options. Wurde `IProtocolMessage` einmal serialisiert, bevor ein weiterer Kanal seine Typen über
  `ExtendNativeProtocolType` registriert hatte (z.B. erst InMemory, dann NamedPipe im selben Prozess), dann blieben
  die späteren Typen unbekannt (`PipeRequest is not supported by polymorphic type IProtocolMessage`).
  `DefaultJsonStrategy` baut deshalb bei jeder Erweiterung Resolver und Options neu.
- `MessageClient.IsBidirectional` wirft keine NRE mehr, wenn die Verbindung nicht zustande kam.
- **Anleitung für die 4.8-Seite (`Net10Wire`):** Beide Typen als abgeleitete Typen von `IProtocolMessage` mit genau
  diesen Discriminatoren aufnehmen (`"$type": "RegisterServiceResponse"` bzw. `"$type": "ServiceTickResponse"`,
  Felder unverändert: `Ok`, `SessionTicket`, `Reason` bzw. `Ok`, `Reason`, `PendingOperationsCount`). Die alten
  Namen „PipeRegisterServiceResponse“/„PipeServiceTickResponse“ gibt es nicht mehr.
- Tests (`NamedPipe.Test/InMemoryHubTest.cs`): Ein Service meldet sich über die Hub-Adresse an, und ein
  `InMemoryClient` ruft ihn auf. InMemory und NamedPipe werden im selben Prozess geladen, und beide Antworten machen
  den Hin- und Rückweg mit den neuen Discriminatoren.
