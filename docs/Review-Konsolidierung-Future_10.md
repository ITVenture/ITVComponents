# Review: Konsolidierung nach der Fix-Welle (Workflow + Blazor)

Stand 2026-08-24, erhoben am Code (nicht am Gedächtnis). Anlass: in den letzten ~60 Commits (PRE175 →
PRE190) sind sehr viele Einzel-Fixes in den Workflow- und den Blazor-Teil geflossen. Die Frage war, ob
dabei Doppelspurigkeiten entstanden sind und wo sich eine Konsolidierungsrunde lohnt.

Erhoben über fünf parallele Review-Läufe (Workflow-Kern, WorkflowViews-Handler, WorkflowViews-UI/Graph,
Blazor-Handler-Muster quer über alle View-Pakete, Querschnitt „stille Fehler"). Alle Zahlen sind
gemessen. Die schwerwiegendsten Befunde sind einzeln gegengeprüft; wo die Gegenprüfung einen
Reviewer-Befund korrigiert hat, steht das dabei.

## Kurzfassung

Die Architektur ist in Ordnung. Was fehlt, ist nicht Design, sondern **zu Ende geführte Umstellungen**:
die passende Abstraktion existiert jeweils schon und läuft im selben Ordner produktiv — sie wurde bei
den Fixes umgangen statt benutzt.

| Bereich | Zustand |
|---|---|
| Workflow-Kern (Engine, Stores, Worker) | strukturell gesund; Fehlerbehandlung vorbildlich |
| WorkflowViews-Handler | Guards laufen auseinander, weil jeder sie selbst tippt |
| WorkflowViews-UI / Graph | doppeltes Routing ist unvermeidbar und sauber gemacht |
| Blazor-Handler quer (AdminViews u.a.) | grosse Mengen kopiertes Gerüst, wenig echte Fehler |
| Blazor-Fehlerbehandlung | **hier sitzt die Schuld** — 55 von 146 `catch` ohne Log |

**Die Doppelspurigkeit ist nicht das Problem, sie ist die Ursache.** Weil jeder Handler seinen Guard
selbst schreibt, deckt jeder Guard unterschiedlich viel ab — so sind die Befunde 3, 4 und 5 unten
entstanden.

---

## A. Echte Fehler

Nicht Redundanz, sondern kaputt. Alle fünf am Code verifiziert.

### 1. Der Web-Worker arbeitet die Nachrichten-Outbox nie ab

`WorkflowWorkerService.Drive` (`ITVComponents.Workflow.WebWorker/WorkflowWorkerService.cs:229-265`)
bildet den Poll-Zyklus des `WorkflowRunner` nach und trifft fünf der sechs Punkte: `FindRunnable`,
`ClaimDueTimers`, `TriggerDueStarts`, `FindBranchesWaitingForTarget`,
`FindFinishedChildrenWithWaitingParent`. Es fehlt genau einer:

```
WorkflowRunner.cs:344     engine.DeliverPendingMessages(owner, timerLease);
WorkflowWorkerService.cs  — keine Entsprechung
```

Repo-weit gibt es drei Erwähnungen von `DeliverPendingMessages`: die Definition, ein Test und der
ParallelProcessing-Runner. Wo **nur** der Web-Worker läuft, wird `ClaimOutgoingMessages` nie gerufen —
eine Vormerkung, die nach einem Absturz zwischen Commit und Zustellung in der Outbox liegt, bleibt dort
für immer. Das ist genau die Ausfallsicherung, für die die Outbox gebaut wurde.

**Status: behoben.** Siehe „Runde 1" unten.

### 2. Blanker Catch-all auf der Principal-Revalidierung

`ITVComponents.WebCoreToolkit.Blazor/Security/BlazorContextUserProvider.cs:229`

```csharp
private void OnAuthenticationStateChanged(Task<AuthenticationState> task) => _ = ApplyAsync(task);
...
catch
{
    // a failed revalidation must not crash the circuit; keep the previous principal
}
```

Fire-and-forget, und der `catch` schluckt alles. Scheitert die Revalidierung dauerhaft (abgelaufenes
Cookie, Repo-Fehler), arbeitet der Circuit unbegrenzt mit dem alten Principal weiter — ohne eine Spur im
Log. Ausgerechnet der Weg, auf dem entschieden wird, wer gerade angemeldet ist. Das Verhalten „alten
Principal behalten" ist richtig; dass es stumm bleibt, ist der Fall aus der Hausregel.

### 3. `MayTouch` deckt nur die Hälfte der Eingriffe ab

`ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews/Monitoring/Handlers/Impl/WorkflowMonitorHandlerBase.cs:114-200`

| Operation | Instanz geladen | `MayTouch` |
|---|---|---|
| `SetPriorityAsync` | ja | **ja** |
| `SignalAsync` | ja | **nein** |
| `SetSuspendedAsync` | nein | **nein** |
| `CancelAsync` | nein | **nein** |

Alle vier prüfen die Permission `Operate` — aber Permission und Mandantengrenze sind zwei verschiedene
Fragen. Wer `Operate` hat, kann per geratener Instanz-Id eine fremde Instanz abbrechen oder anhalten,
sofern der Query-Filter nicht greift. Die Klasse warnt an anderer Stelle selbst davor, sich auf ihn zu
verlassen: „ob der greift, entscheidet die Registrierung des Kontexts im Host".

**Status: behoben.** Alle sechs Eingriffe laufen jetzt über `TryLoadOwnInstance`; `CancelAsync` und
`SetSuspendedAsync` laden dafür die Instanz, was sie vorher nicht taten. „Gibt es nicht" und „gehört
einem anderen Mandanten" bekommen dabei getrennte Log-Meldungen — ein gemeinsames stilles `false` macht
aus einem Angriffsversuch einen Tippfehler.

### 4. Definitions-Speichern ohne Besitzprüfung

`WorkflowDesignHandler.cs:117` zusammen mit `EfWorkflowStore.cs:498-509`. Der Store lädt bewusst
filterfrei und delegiert die Zugriffsentscheidung ausdrücklich an den Aufrufer:

```csharp
// Bewusst OHNE Query-Filter: ... Die Zugriffsentscheidung faellt am Verweis, nicht hier
WorkflowDefinitionRow row = ctx.WorkflowDefinitions.IgnoreQueryFilters()
    .FirstOrDefault(d => d.DefinitionKey == definitionKey);
```

Der Aufrufer löst die Delegation nicht ein: er liest aus `stored` nur `IsPublic`, nie `TenantId`. Ein
`Key` aus einem fremden Mandanten im geposteten Modell landet damit auf dessen Zeile, und
`SaveDefinition` schreibt zusätzlich `definition.TenantId` neu. `ListDefinitionsAsync` und
`GetDefinitionAsync` prüfen ausserdem gar nichts und verlassen sich vollständig auf `SecureView` in der
Komponente.

**Status: behoben** über `MayTouchDefinition` in der neuen Handler-Basis. Die fehlenden Guards auf den
*lesenden* Wegen (`List`/`Get`) sind **offen** — sie sind eine eigene Entscheidung, weil dort heute
bewusst der Query-Filter trägt.

### 5. Spaltensortierung im Tsc-Modus wirkungslos

`TscUserAdminHandler.cs:125` wertet `SortColumn` nicht aus:

```csharp
q = query.SortDescending ? q.OrderByDescending(u => u.UserName) : q.OrderBy(u => u.UserName);
```

Das Gegenstück `UserAdminHandler.cs:119-125` sortiert über vier Spalten. Ein Klick auf „E-Mail" sortiert
im Tsc-Modus nach Benutzername.

> **Korrektur am Reviewer-Befund:** Der Bericht sagte, alles falle auf `OrderByDescending` zusammen.
> `SortDescending` wird durchaus beachtet — ignoriert wird die *Spalte*.

---

## B. Die Abstraktion existiert, sie wird umgangen

Das durchgehende Muster. Jeweils: die Lösung ist da und läuft, der Fix wurde daneben gelegt.

| Vorhanden | Umgangen an |
|---|---|
| `ClearWait` (`WorkflowEngine.cs:810`) | 7 Stellen bauen es von Hand nach, jede mit anderem Umfang |
| `MutateAndCommit` (`WorkflowEngine.cs:1995`) | 3 eigene Retry-Schleifen; `CancelWorkflow:1344` committet **ohne** Versionsprüfung |
| `ICoreSystemContextFactory` | 10 Handler nutzen sie, 17 schleppen 62 Zeilen Typparameter mit (~1.050 Z.) |
| Die Onboarding-`…Helper` | für ein Handler-Paar ausfaktorisiert, für das andere nie durchgezogen |
| `SecureView` | 22 von 35 Seiten berechnen daneben ein `hasWrite`, das nie gelesen wird |
| `WorkflowJson.Deserialize` | `WorkflowDesignHandler.cs:209` nimmt blankes `JsonSerializer.Deserialize` |

Bei `ClearWait` ist die Folge latent statt harmlos: die Methode löscht vier Warte-Anker, die Handformen
je eine Teilmenge. `FireBoundaryTimer:3221` und `FireBoundaryMessage:3415` machen im *identischen* Zweig
Verschiedenes — die Message-Variante ruft `ClearWait`, die Timer-Variante lässt `WaitingCorrelation` und
`WaitingKind` stehen. `Accepts:783` liest genau diese Felder: ein weitergelaufenes Token könnte eine
Nachricht annehmen, die ihm nicht mehr gilt.

---

## B2. Entschieden: was heisst „kein Mandant"?

**Erledigt 2026-08-25.** Der Befund war: dieselbe Frage — „geht mich diese Zeile etwas an?" — wurde an
zwei Stellen gegenläufig beantwortet. `OpenTasks` las „kein Mandant" als `TenantId IS NULL` und zeigte
in einer Anlage mit Mandanten **nichts**; der Guard las denselben Zustand als Ein-Mandanten-Betrieb und
erlaubte **alles**. Die Arbeitsliste war leer, während Abbrechen, Anhalten und Signal auf jede geratene
Instanz-Id gingen — genau verkehrt herum.

### Die Regel

`WorkflowTenantScope` beantwortet die Frage einmal, in zwei Formen: `Restrict` für die Abfrage, `Owns`
für den Guard.

| `ctx.UseTenantFilter` | `ctx.CurrentTenant` | Liste **und** Guard |
|---|---|---|
| `false` | (immer null) | `TenantId == null` |
| `true` | `"kunde-a"` | `TenantId == "kunde-a"` |
| `true` | `null` | **nichts** + Log (`IsUnresolved`) |

Der dritte Zustand bekommt damit einen eigenen Namen: Mandantenbetrieb ohne ermittelbaren Mandanten ist
ein **Verdrahtungsfehler**, kein Betriebszustand. Weder „alles" noch „die mandantenlosen" ist dort
richtig, sondern nichts — hörbar.

### Warum abgelesen und nicht konfiguriert

Die Antwort kommt vom geleasten `WorkflowContext`, nicht aus einer Einstellung. Grund: **genau dieses
`ctx.CurrentTenant` schreibt im Store auch den Mandanten der Zeilen**
(`SaveInstance`: `instance.TenantId ?? ctx.CurrentTenant`). Lese- und Schreibseite haben damit eine
gemeinsame Quelle und können nicht auseinanderlaufen. Ein eigener Schalter an der
Umgebungs-Konfiguration könnte dem Kontext widersprechen — und der Widerspruch wäre still: Zeilen
mandantenlos geschrieben, Ansicht mandantengebunden gesucht, niemand sieht etwas.

Dass die Entscheidung **pro Umgebung** gilt, ergibt sich von selbst: jede leaset ihr eigenes
Store-Plugin. Eine Anlage, in der jeder Mandant seine eigene Workflow-Datenbank hat, betreibt diese
Ablagen mandantenlos; eine geteilte Ablage betreibt sie mandantengebunden. Beides nebeneinander im
selben Prozess ist erlaubt.

### Die Ausnahme, die bleiben muss

**Definitionen und Auslöser sind nicht mitgezogen.** Dort heisst `TenantId == null` nicht „gehört
niemandem", sondern *öffentlich* — sichtbar und startbar für alle Mandanten, so auch der Query-Filter
(`TenantId == CurrentTenant || TenantId == null`). Diese dritte Möglichkeit lässt sich nicht in ein
Prädikat zwingen, das nur „meins" und „keins" kennt; wer es versucht, lässt jeden öffentlichen Ablauf
aus Designer und Startauswahl verschwinden. Die Grenze steht als Doku an `CurrentTenant()`.

### Nebenbefund, mit behoben

Die Handler fragten `IPermissionScope` **direkt** und ignorierten dabei `UseTenantFilter`. Für eine
filterfreie Umgebung filterte die Ansicht also nach Mandant, während ihr eigener Kontext es nicht tat.

---

## C. Konsolidierungs-Runden

### Runde 1 — billig, schliesst Fehler mit (Aufwand S) — **erledigt**

1. **Outbox-Einzeiler** (Befund 1). `DeliverPendingMessages` im `Drive` des Web-Workers, direkt nach
   `TriggerDueStarts`, mit demselben `lockOwner` und derselben Lease. Zählt in `any` mit — eine
   nachgeholte Nachricht weckt einen Empfänger, der erst im nächsten Suchlauf auftaucht.
2. **`ClearWait` erweitert** um `WaitingTarget` + `WaitingForChildInstanceId` — jetzt alle sechs Anker.
   Zwölf Aufrufstellen statt vier: die sieben Handformen sind weg, dazu die zwei Kind-Instanz-Rückkehren
   und der Ziel-Handoff. Beim **Parken** wird derselbe Aufruf verwendet (erst alles löschen, dann den
   einen Anker setzen, der jetzt gilt) — das ist billiger, als je Wartepunkt zu überlegen, was der
   vorherige hinterlassen haben könnte.
3. **`WorkflowHandlerBase`** (`Runtime/WorkflowHandlerBase.cs`) mit **einem** Instanz-Guard
   (`TryLoadOwnInstance`), **einem** Definitions-Guard (`MayTouchDefinition`) und **einer**
   Mandanten-Entscheidung (`OwnsTenant`). Alle drei Handler erben. Damit prüfen jetzt **alle sechs**
   Monitoring-Eingriffe den Besitz statt drei, und das Definitions-Speichern prüft ihn überhaupt.
   Zusammengezogen: `BeginOperation` (mit `NeedsEngine` für den Entwurf), `CurrentTenant`, `UserName`,
   `HasPermission`.
4. **Die stillen `catch`** in `FileDownload.razor`, `FileUpload.razor` und `NavigationTreeGrid.razor`.
   Letzteres war eine Fix-Narbe: derselbe DnD-Block wurde in `HelpResources.razor` und
   `DashboardWidgets.razor` sauber nachgezogen, hier vergessen — jetzt mit derselben Fassung.
5. **Sortierung vor dem Blättern** in beiden User-Handlern. Der ursprünglich gemeldete Befund
   (ignoriertes `SortColumn` im Tsc-Modus) ist harmlos — die Mail-Spalten blendet die Maske über
   `SupportsEmail = false` aus, sortierbar bleibt nur der Name. Darunter lag der echtere Fehler:
   **beide** Handler paginierten den Mandanten-Zweig ganz ohne `ORDER BY`. Ohne den steht die
   Reihenfolge einer Seite der Datenbank frei — derselbe Benutzer kann auf Seite 1 und 2 auftauchen, ein
   anderer auf keiner.

### Runde 2 — die grossen Posten

| Posten | Ersparnis | Risiko |
|---|---|---|
| Onboarding Flat/Hierarchy generisch | ~750 Z. | mittel, nicht breaking |
| Trigger-/Aktivierungs-Logik store-neutral | ~450 Z. | **mittel-hoch** |
| `CrudGrid`-Komponente für 61 Grid-Seiten | ~2.000 Z. | niedrig-mittel |
| `EditDialogShell` für 43 Dialoge | ~950 Z. | niedrig |
| `SvgWriter`: Viewer und Editor zeichnen dasselbe SVG zweimal | ~200 Z. | mittel |

**Onboarding — erledigt, aber anders als geplant. Siehe „Runde 2a" unten: die Zahl in diesem Absatz
hält der Prüfung nicht stand.** `FlatOnboardingAdminHandler.cs` (775 Z.) und
`HierarchyOnboardingAdminHandler.cs` (776 Z.) sind zu **96,6 %** identisch — nur 53 Zeilen unterscheiden
sich, alle 30 Member existieren beidseitig, kein Verhaltensunterschied. Der Härtetest: `git log` zeigt
für beide Dateien **dieselben 6 Commits**. Jede Änderung musste bisher zweimal gemacht werden; beim
siebten Mal wird sie es nicht.

**Stores:** Der Trigger-/Aktivierungs-Lebenszyklus (`SyncTriggers`, `RehomeActivations`,
`EnsureOwnActivation`, Pattern-Reset, `WarnAboutOrphans`) ist in beiden Stores vollständig ausformuliert
— ~180 Zeilen in-memory, ~300 in EF, inklusive wortgleicher Log-Texte. Das ist Fachlogik, keine
Persistenz. **Sie sind bereits auseinander:** der Altbestand-Fang über `DefinitionKey`
(`EfWorkflowStore.cs:225`) existiert nur in EF. Ein grüner In-Memory-Test beweist für diesen Weg nichts
mehr.

Ebenfalls Store-Divergenz: `FindWaitingForSignal`/`FindWaitingForBroadcast` filtern in-memory zusätzlich
über `instance.Status == Waiting`, in EF rein über den Token-Zustand. Eine Instanz mit einem aktiven und
einem wartenden Zweig (Status `Running` — der Normalfall bei parallelen Regionen und bei Nachrichten am
Schritt) findet der EF-Store, der In-Memory-Store nicht. — **erledigt, siehe Runde 1b.**

### Runde 1b — die Store-Divergenz bei den Suchläufen — **erledigt**

Betroffen waren **drei** Abfragen, nicht zwei: `FindWaitingForSignal`, `FindWaitingForBroadcast` und
`FindDueTimers` trugen in-memory je einen zusätzlichen Filter über `instance.Status == Waiting`. Der
Filter ist ersatzlos gestrichen; beim Timer bleibt `!Suspended` stehen, denn **den** hat der EF-Store
auch (angehalten heisst: fällig ja, vorantreiben nein).

Die Regel stand längst im selben File: `FindBranchesWaitingForTarget` sagt in seinem Kommentar genau das
Richtige — *„rein am Token-Zustand orientiert (nicht am Instanz-Status)"*. Wieder der Befund aus Abschnitt
B in klein: die Fassung war da, sie wurde nur nicht angewendet.

Dass der Instanz-Status hier nichts zu suchen hat, ist keine Auslegung, sondern im Kern nachlesbar:
weder `SignalInstance` noch `ReactivateSignal` noch `TriggerTimers` schauen ihn an. Wer ein Ereignis
annimmt, entscheidet allein `WorkflowEngine.Accepts` am **Token**.

Mitgenommen: `InMemoryWorkflowStore.ClaimDueTimers` schnitt bei `maxInstances` aus einer nur nach
Dringlichkeit sortierten Liste ab — der EF-Store sortiert zusätzlich nach der ältesten Fälligkeit je
Instanz. Bei Gleichstand entschied in-memory die Reihenfolge des Dictionaries. Jetzt beide gleich.

**Der Beweis liegt neu in `ITVComponents.Workflow.EntityFramework.Test/WorkflowStoreContractTest.cs`:**
sieben Szenarien, jedes über `[DataRow]` gegen **beide** Store-Fassungen. Das Test-Projekt des Kerns kann
das nicht leisten — es sieht den EF-Store nicht. Genau deshalb konnte die Divergenz so lange bestehen.
Gegenprobe gefahren: mit wieder eingebautem Filter fallen genau die drei `("memory")`-Fälle, die
`("ef")`-Fälle bleiben grün.

### Runde 1c — ein fälliger Timer hob einen Fault auf — **entschieden und behoben**

Beim Nachverfolgen der Faulted-Kante aufgefallen. Es galt **im EF-Store, also im Betrieb** — die
Angleichung aus Runde 1b hat es nur sichtbar gemacht, nicht verursacht.

`Fault()` setzt den Status und lässt die übrigen Tokens **stehen** (gewollt — daran hängt der Retry).
Ein noch scharfer Timer an einem anderen Zweig wurde später fällig, `FindDueTimers` lieferte die Instanz,
und `TriggerTimers` bzw. `ReactivateTimers` setzten `Status = Running`, **ohne den Fault zu prüfen**.
Die Instanz lief still weiter, die Fehlermeldung war weg. Dasselbe galt für ein eintreffendes Signal,
für die Ziel-Übernahme und für die Rückkehr eines Subworkflows.

Typischer Weg dorthin: ein Fristen-Timer am Schritt bleibt scharf, während ein anderer Zweig faultet.

### Die Entscheidung

**Ein Retry muss ausdrücklich ausgelöst und protokolliert sein.** Ein Timer, der von selbst fällig wird,
ist beides nicht — er darf eine gefaultete Instanz nicht weiterlaufen lassen. Wieder aufgenommen wird
sie ausschliesslich über `RetryFaultedBranches`.

### Wo der Riegel sitzt — und wo ausdrücklich nicht

Neu `WorkflowEngine.MayResumeOnEvent(instance, opName)`: **eine** Stelle, dieselbe Statusmenge wie
`Advance` (Completed, Faulted, Cancelled), mit Log-Eintrag beim Abweisen. Angeschlossen sind die sieben
Wege, auf denen ein Ereignis einen Wartepunkt weiterschiebt: `SignalWorkflow`/`SignalInstance`,
`TriggerTimers`, `ReactivateSignal`, `ReactivateTimers`, `ReactivateForTargets`, `MessageDelivered` und
`DeliverChildCompletion`.

**Der Store bleibt bei den ereignisgetriebenen Suchläufen aussen vor.** `FindWaitingForSignal` findet
eine gefaultete Instanz weiterhin — er muss, sonst käme man nach einem Retry nie wieder an den Zweig
heran, und eine Nachricht, die eine gefaultete Instanz nicht erreicht hat, ist eine Meldung, die man
haben will. Abgewiesen wird in der Engine, wo sich das berichten lässt.

**Bei den gepollten Suchläufen dagegen schon**, und zwar genau dort, wo die Bedingung für „angehalten"
ohnehin steht: `FindDueTimers`, `ClaimDueTimers` und `FindBranchesWaitingForTarget` lassen Gefaultete
aus — in **beiden** Stores. Der Grund ist nicht Fachlichkeit, sondern Betrieb: ein fälliger Timer bleibt
fällig. Ohne diese Bedingung grüffe der Riegel bei **jedem Runner-Takt** aufs Neue und schriebe dieselbe
Warnung endlos fort — der Guard wäre selbst der nächste Fehler. `ClaimDueTimers` verbräuchte zudem je
Poll einen Platz von `maxInstances` für eine Instanz, die niemand aufgreifen darf.

### Der Test, der aus dem falschen Grund grün war

Die Gegenprobe (Riegel entschärft, Tests müssen fallen) hat einen der drei Schutztests als **wertlos**
entlarvt: er prüfte nur, dass die Instanz am Ende noch `Faulted` ist — und das ist sie auch **ohne**
Riegel. Das Token des gescheiterten Zweigs bleibt nämlich aktiv (es IST der Wiederaufsatzpunkt), ein
Vortrieb führt die Aktivität deshalb erneut aus, sie scheitert wieder, und die Instanz landet ein
zweites Mal auf `Faulted`. Der Status verrät hier gar nichts.

Ob der Timer gefeuert hat, sieht man **nur an seinem eigenen Token**: ist es noch `Waiting` mit
`DueUtc`, hat er nicht gefeuert. Genau darauf prüft der Test jetzt — und fällt in der Gegenprobe.

**Merke:** bei einem Riegel gegen „still weiterlaufen" ist der Instanz-Status die schlechteste Zusage,
die man prüfen kann. Er stellt sich von selbst wieder her.

### `CompleteUserTask` — nachgezogen, mit eigenem Ausgang

Dort klickt ein **Mensch** auf eine Aufgabe, die ihm angezeigt wurde. Der Riegel greift auch hier, aber
ein blosser Log-Eintrag wäre die falsche Antwort — und `NotFound` wäre eine Ausrede: die Aufgabe gibt
es sehr wohl noch, und nach einem Retry lässt sie sich auch wieder erledigen.

Neu deshalb `UserTaskCompletionStatus.InstanceNotResumable` (angehängt, damit bestehende Werte ihre Zahl
behalten). Zu unterscheiden von `Faulted`: **dort** ist die Aufgabe erledigt und der Prozess erst danach
gescheitert, **hier** war er es schon vorher.

In `UserTaskDialog.razor` bekommt der Fall einen eigenen `case` — bewusst **ohne** `completed = true`:
es wurde nichts abgeschlossen, also darf weder die Nachbereitung der Maske laufen noch der Assistent
weiterspringen. Der Dialog bleibt offen; die Eingaben des Benutzers wegzuwerfen wäre die schlechtere
Antwort. Meldung in allen vier Sprachen (`WorkflowTaskMessages(.de|.fr|.it).resx`).

Der Post-Hook `IUserTaskView.PostResolveActivityAsync` sieht den neuen Ausgang **nicht**: er läuft nur nach
einem echten Abschluss, und genau das ist hier nicht passiert.

### Runde 2a — Onboarding: was davon wirklich ging

**Die 96,6 % stimmen als Text. Sie stimmen nicht als Typen** — und daran ist die geplante gemeinsame
Basisklasse gescheitert.

Mehrere Zeilen, die im `diff` gar nicht auftauchen, sind in Wahrheit verschieden. Sie sehen nur gleich
aus, weil das `using` die Auflösung macht:

```csharp
var role = new Role { TenantId = current, RoleName = freeName, IsSystemRole = false };
db.RoleRoles.Add(new RoleRole { PermissiveRoleId = set.RoleId, PermittedRoleId = direct.RoleId });
```

`Role` ist hier einmal `CoreIdentity.Models.Role` und einmal `CoreIdentityTree.Model.Role` — zwei CLR-Typen
mit je **zwölfparametriger** generischer Basis. Dasselbe bei `RoleRole`, `User`, `Tenant`, `TenantUser`.
Dazu greift der Handler nicht nur auf Skalare zu, sondern auf **Navigationen** (`p.Tenant.DisplayName`,
`p.Employees.Count`, `m.Role.RoleName`, `p.DefaultAddress`) — eine nicht-generische Skalar-Basis im Modell
hätte die nicht abgedeckt.

Die Rechnung für eine gemeinsame Basisklasse: **~19 Typparameter**, ein Drittel davon nur, um die
Constraint-Kette zu schliessen, dazu ~10 abstrakte DbSet-Accessoren (an `ISecurityContext<45 Argumente>`
lässt sich `TContext` nicht binden). Das ist genau die Kategorie, die weiter unten unter
„Zurückgestellt" steht — 700 lesbare Zeilen gegen eine Wand aus `where`-Klauseln. **Nicht gemacht.**

**Gemacht wurde die Helfer-Extraktion** im Hausmuster von `OnboardingPendingHelper`: statische Methoden
mit **engen** Typparametern je Methode, neu in `OnboardingAdminHelper.cs`. Die Auswahlregel: was eine
*Entscheidung* trifft oder auf einem *geteilten* Typ arbeitet, geht hinein; die getippten Abfragen auf
strategie-eigenen Entitäten bleiben beim Handler, dort kostet der Typ nichts.

Zusammengeführt: `Authorize`, `ForceDedicatedRoleForMappings`, beide Permission-Switches,
`RequiredAssignPermission`, `MayIgnoreFeatureGate`, `ActiveFeatureNames`, `ListFeaturesAsync` (`Feature`
ist ein geteilter Typ), die Consent-Abfrage (`ConsentRecord` ist strategie-neutral — das sagt
`IOnboardingConsentContext` selbst), `FindFreeRoleNameAsync` (Namensregel hier, Abfrage per Rückruf beim
Aufrufer) sowie `ToInput`/`Fill` auf `AddressBase<>`.

| | vorher | nachher |
|---|---|---|
| `FlatOnboardingAdminHandler` | 775 | 667 |
| `HierarchyOnboardingAdminHandler` | 776 | 668 |
| `OnboardingAdminHelper` | — | 240 |

**216 doppelt gepflegte Zeilen sind zu einer Fassung geworden.** Die Netto-Zeilenzahl bleibt dabei etwa
gleich — der Gewinn ist „eine Stelle statt zwei", nicht „weniger Code". Wer die Ersparnis in Zeilen
misst, misst das Falsche.

Von den sechs gemeinsamen Commits hätten **drei** nur noch eine Stelle berührt (Re-Gating auf
`Onboarding.Admin.*`, Feature-Sichtbarkeits-Gate, Zustimmungs-Nachweise). Die `IDbContextFactory`-Umstellung
nicht — die sass im Rumpf jeder einzelnen Methode.

Neu abgesichert: `OnboardingAdminHelperTest` im AdminViews-Testprojekt — Berechtigungs-Zuordnung,
Namensfindung samt Aufgeben nach fünf Varianten, Adress-Übernahme. Vorher gab es für diese Handler
**keinen einzigen Test**; ein Test hätte damals ohnehin nur eine der beiden Fassungen getroffen. Der
Helfer bleibt `internal` (das Projekt wird als Paket ausgeliefert) und ist über `InternalsVisibleTo`
prüfbar.

### Runde 2b — `EditDialogShell`: 35 von 45 Dialogen

Hier hielt die Schätzung der Prüfung stand. 45 Dialoge trugen dasselbe Gerüst — Rahmen, `MudForm`,
Aktionsleiste, `Cancel`, `Save` — und in **35** davon war der Abschlussweg **wortgleich**, in genau zwei
Formen (mit und ohne Hinweis-Meldung). Keiner nutzte `TitleContent`, 44 hatten genau zwei Knöpfe.

Neu `ITVComponents.WebCoreToolkit.Blazor.MudBlazor/SharedComponents/EditDialogShell.razor`. Bewusst
**nicht** generisch: `MudForm.Model` ist selbst `object`, und die Nutzlast eines `DialogResult` ebenso —
ein `TModel` hätte nichts geprüft, wäre aber an jeder Aufrufstelle mitzuschreiben gewesen.

| | |
|---|---|
| 35 Dialoge | −1064 / +286 Zeilen |
| Hülle | +137 (grösstenteils Doku) |
| **netto** | **≈ −640 Zeilen** |

**Der eigentliche Gewinn ist aber nicht die Zeilenzahl.** In jedem der 35 Dialoge stand

```csharp
await form.Validate();
if (!form.IsValid) return;
```

von Hand. Wer das beim nächsten Dialog vergisst, schliesst ihn trotz ungültiger Eingaben — und niemand
merkt es, bis Unsinn in der Datenbank steht. Das kann jetzt niemand mehr vergessen.

**Sofort eingelöst:** der Compiler meldete an dieser Zeile `CS0618` — `MudForm.Validate()` ist zugunsten
von `ValidateAsync()` veraltet. Die Korrektur war **eine** Zeile; vorher wäre sie fünfunddreissig Mal
fällig gewesen. Genau dafür macht man so einen Umbau.

### Zwei Uneinheitlichkeiten, die dabei sichtbar wurden — bewusst NICHT vereinheitlicht

- **Der Speichern-Knopf sieht nicht überall gleich aus**: 16 Dialoge setzen `Variant="Variant.Filled"`,
  der Rest nichts (also `Variant.Text`, die Vorgabe von `MudButton`).
- **Bei ungültiger Eingabe** zeigen 20 Dialoge keine Meldung und 15 ein „Please correct invalid fields".

Beides blieb zunächst als Parameter (`SaveVariant`, `InvalidMessage`) je Maske erhalten — eine
Optik-Entscheidung für vierzig Masken gehört nicht in einen Umbau, der sonst nichts sichtbar ändert.

**Nachgetragen (Runde 2d):** die Entscheidung zum Speichern-Knopf ist gefallen und war dann genau das,
was die Hülle versprochen hat — Vorgabe der Hülle auf `Variant.Filled`, fünfzehn nun überflüssige
Attribute gestrichen. **Neunzehn Dialoge sehen dadurch anders aus als vorher**, und das ist gewollt: der
Speichern-Knopf ist die vorgeschlagene Handlung und soll sich vom Abbrechen daneben abheben.
`InvalidMessage` bleibt je Maske.

### Was stehen blieb, und warum

Zehn Dialoge sind nicht umgestellt — nicht übersehen, sondern vom Umbau-Skript ausdrücklich abgewiesen:
es arbeitet streng konservativ und lässt jede Datei unverändert, die nicht **exakt** auf das Muster
passt. Sie haben echten eigenen Abschluss-Code (Speichern über einen Handler, Zusatzprüfungen, eine vom
Server vergebene Id als Nutzlast, ein Skript-Editor, dessen Inhalt erst beim Speichern abgeholt wird).
Für sie bietet die Hülle `OnValidated` und `Result` an; ob sich der Umbau dort lohnt, ist einzeln zu
entscheiden und nicht im Sweep.

Nebenbei entfielen 15 `@inject ISnackbar Snackbar`, die nach dem Umbau niemand mehr brauchte.

### Runde 2c — `CrudGrid`: der Posten hält der Prüfung NICHT stand

Von den drei grossen Posten ist das der einzige, dessen Schätzung (~2.000 Z.) sich nicht einlösen lässt.

**Ein gemeinsames Raster-Bauteil ist blockiert.** `CrudGrid<T>` bräuchte einen gemeinsamen
Handler-Vertrag (`ListAsync`/`CreateAsync`/`UpdateAsync`/`DeleteAsync`). Den gibt es nicht — jeder der
rund sechzig Handler hat eigene Methodennamen. Ihn nachzurüsten hiesse, `PagedResult`/`ListQuery`
zusammenzulegen, und **genau das steht unten unter „Zurückgestellt" als breaking**. Nebenbefund:
`ListQuery` (TenantSecurityViews) und `UserListQuery` (AspNetCoreTenantSecurityUserView) sind Feld für
Feld identisch, `PagedResult<T>` ebenso.

**Die Werkzeugleiste ist nicht skriptbar.** 65 Leisten, 795 Zeilen — aber die häufigste normalisierte
Form kommt **viermal** vor. Die Varianz ist echt: mit/ohne Suchfeld (nur 30 von 65 haben eines),
mit/ohne Hinzufügen-Knopf, unterschiedliche `SecureView`-Berechtigungen, `@if (Root)`-Verzweigungen,
lokalisierte und wörtliche Titel. Ein `CrudGridToolbar` ist machbar, aber das wären **65 Handgriffe an
Markup mit sichtbarer Wirkung**, nicht ein Sweep. Offen, als eigene Entscheidung.

**Gemacht wurde der Teil mit einem Fehler-Bezug:** neu `GridQuery.ToListQuery` (und `UserGridQuery.
ToUserListQuery` für die Benutzer-Ansichten). Die Übersetzung `GridState` → `ListQuery` stand achtzehn
Mal ausgeschrieben da; die Sortierung geht darin über `state.SortDefinitions.FirstOrDefault()`. Wer die
Zeile vergisst oder `SortColumn` nicht setzt, bekommt ein Raster mit anklickbaren, **wirkungslosen**
Spaltenköpfen — und das ist kein erfundenes Risiko, sondern **Befund 5 aus Runde 1**
(„Spaltensortierung im Tsc-Modus wirkungslos"). Jetzt kann er nicht mehr entstehen.

18 Dateien, −163/+18 Zeilen.

**Bewusst NICHT gemacht:** die übrigen 107 wiederkehrenden Einzeiler
(`new GridData<X> { Items = result.Items, TotalItems = result.TotalCount }` und die leere Variante) auf
Helfer umzustellen. Sie sparen **keine** Zeile, verhindern **keinen** Fehler und hätten 59 Dateien
angefasst — das wäre Unruhe statt Konsolidierung.

### Runde 2d — `CrudGridToolbar`, und was der Compiler dabei gefunden hat

Die Werkzeugleiste wurde doch angegangen — **55 von 65** Leisten laufen jetzt über
`ITVComponents.WebCoreToolkit.Blazor.MudBlazor/SharedComponents/CrudGridToolbar.razor`.
82 Dateien, −606/+377, Komponente 100 Zeilen.

Zwei Entwurfsentscheidungen ergaben sich aus den Zahlen, nicht aus dem Gefühl:

- **`TitleTypo`**: die vermeintlich titellosen Leisten hatten sehr wohl einen Titel, nur mit
  `Typo.subtitle2` statt `h6`. Das sind Untergitter *innerhalb* von Dialogen, die nicht mit dem
  Dialogtitel konkurrieren sollen — eine sinnvolle Unterscheidung, kein Wildwuchs.
- **`OnSearch` getrennt von `SearchChanged`**: der gebundene Wert soll bei jedem Tastendruck aktuell
  sein, der Server-Aufruf aber nicht. Die Vorlagen machten das schon so (`@bind-Value` plus
  `OnDebounceIntervalElapsed`); ein Zusammenlegen hätte je Taste eine Abfrage ausgelöst.

**Der erste Anlauf war falsch, und das ist der lehrreiche Teil.** Der Compiler meldete:

- **`RZ9996`, 55×** — das Umbau-Skript hatte `<ToolBarContent>` *ersetzt* statt dessen Inhalt zu füllen.
  `MudDataGrid` nimmt als direktes Kind nur seine eigenen Fragmente an. Die Komponente gehört
  **hinein**, nicht an dessen Stelle.
- **`RZ9986`, 1×** — `Roles.razor` trägt den Titel `Roles for tenant @effectiveTenantId`. Als
  Attributwert ist das gemischter C#-/Markup-Inhalt, den Razor ablehnt. Regel jetzt: enthält der Titel
  `@` oder `"`, geht er als `TitleContent`-Fragment.
- Dazu ein dritter, nur kosmetisch: der Suchfeld-Regex verschluckte das `
`, das nackte `
` passte
  danach nicht mehr auf den CRLF-Zeilensplit — die Einrückung des Restinhalts zerfiel.

**Bemerkenswert ist, was NICHT passierte:** `RZ10012` kam in keinem Lauf vor. Die Komponente wurde also
von Anfang an überall aufgelöst — der `@namespace`- und `_Imports`-Weg stimmte. Genau diese Diagnose
kann bei einem Razor-Sweep still danebengehen (siehe die Unterordner-Falle), und sie war sauber.

Zehn Leisten blieben stehen: sieben mit abweichendem Suchfeld (`Clearable`/`Label`/`Margin` statt der
üblichen Form) und drei strukturelle Sonderfälle, darunter der Hilfe-Themenbaum mit seiner
`@if (Root)`-Verzweigung.

**Mitgenommen:** die zehn nicht umgestellten Dialoge (plus `BillingProfile.razor` und
`TestFormDialog.razor`, die nach Dateinamen keine Dialoge sind, den Aufruf aber auch hatten) rufen jetzt
`ValidateAsync()` statt des veralteten `Validate()` — zwölf Dateien, `CS0618` damit vollständig weg.

### Runde 2e — Trigger-Logik: nicht zusammengelegt, sondern nachgewiesen

**Die Zusammenlegung scheitert nicht an der Logik.** Die Entscheidungsregeln *sind* identisch — die
`AllowReschedule`-Ausnahme, die `IsPublic`-Regel, bis in die Log-Texte hinein. Sie scheitert am
**Speicher-Modell**: die eine Fassung arbeitet auf Domänenobjekten in Dictionaries, die andere auf
EF-Zeilen mit `IgnoreQueryFilters` und `SaveChanges` — und die trägt SQL-Übersetzbarkeit im Gepäck.
`EfWorkflowStore` vermerkt ausdrücklich, dass `DefaultIfEmpty(wert)` sich **nicht** übersetzen lässt,
und genau das benutzt die In-Memory-Fassung. Eine gemeinsame Implementierung müsste in der EF-Form
geschrieben werden und dem Speicher ohne Datenbank die Zwänge einer Datenbank aufdrücken.

**Also nachgewiesen statt zusammengelegt.** Es gab ~30 Tests für diesen Lebenszyklus — alle nur gegen
EF. Neu `WorkflowTriggerLifecycleContractTest`: sechs Zusagen über **beide** Fassungen. Darunter die,
die am leichtesten kaputtgeht — die Aktivierung hängt an der **fachlichen Identität** des Auslösers,
nicht an seiner Zeilennummer; der `TriggerKey` wird bei jedem Neuaufbau neu vergeben.

Eine Abweichung ist geprüft und **gewollt** (steht als Kommentar, nicht als Test): der EF-Store sammelt
zusätzlich über `DefinitionKey`, um Auslöser-Zeilen aus der Zeit vor einer Korrektur einzufangen. Ein
Speicher ohne Persistenz kann keinen Altbestand haben.

**Der Test schlug beim Schreiben dreimal zu — und jedes Mal lag es an der Erwartung, nicht an den
Stores.** Die waren sich in allem einig:

1. Eine öffentliche Definition ist nicht automatisch übernehmbar — der Start-Knoten muss es erlauben.
2. `SaveActivation` schreibt bewusst nur Zustimmung, Muster-Übersteuerung und Variablen und lässt den
   Lauf-Zustand einer bestehenden Zeile in Ruhe.
3. `LastInstanceId` wird nur zusammen mit `lastRunUtc` geschrieben — „welche Instanz" ohne „wann" wäre
   eine halbe Auskunft.

Der zweite ist der lehrreichste: als schlichte Zuweisung am Objekt war der Test **in-memory grün und in
EF rot**. Der Speicher ohne Datenbank liefert dieselbe Referenz zurück, die der Aufrufer bearbeitet hat
— die Zuweisung „wirkt" dort ohne jedes Speichern. Genau dafür läuft der Test gegen beide Fassungen.

### Runde 2f — `SvgWriter`: die Schätzung setzt eine Kopie voraus, die es nicht gibt

**Ansicht und Editor erzeugen nicht dasselbe SVG.** Die Ansicht zeichnet in absoluten Koordinaten, der
Editor am Ursprung innerhalb einer verschobenen Gruppe (`transform="translate(x,y)"`) und mit
`data-*`-Merkmalen, weil an ihnen das Ziehen und das Interop hängen — dieselbe Unterscheidung wie beim
doppelten Kanten-Routing, das weiter oben schon als unvermeidbar steht.

Geteilt ist die **Geometrie**, und das ist der Teil, dessen Auseinanderlaufen niemand bemerkt: eine
Raute mit einer anderen Spitze fällt nicht als Fehler auf, sondern höchstens als „sieht im Editor
irgendwie anders aus". Neu `Graph/SvgShapes.cs` mit `Diamond` und `Hexagon`; der Versatz ist der
einzige Unterschied zwischen den Aufrufern, also ist er ein Parameter. Die Einbuchtung des Sechsecks lag
ohnehin schon gemeinsam in `GraphLayout.HexagonInset` — der neue Typ zieht nach, was danebenstand.
Mitgenommen: die Zahlenformatierung (SVG braucht den Punkt; unter deutschem Gebietsschema wäre ein
Komma ein zweiter Koordinaten-Trenner, und die Form zerfiele).

74 Zeilen neu, 26 entfernt — die geschätzten ~200 Zeilen Ersparnis gibt es nicht, weil die Kopie nicht
existiert.

### Zurückgestellt

- ~~**`HasPermission(ClaimsPrincipal user, …)`**~~ — **erledigt, siehe Runde 3.** Der Parameter ist weg.
- **Die 17 generischen Handler-Präambeln** (~1.050 Z.): Aritätsänderung, breaking für
  Host-Registrierungen.
- **`PagedResult<T>`/`ListQuery` dreifach definiert**: Namespace-Wechsel ist breaking für Hosts mit
  eigenen Handler-Implementierungen.

### Runde 3 — der erste zurückgestellte Bruch, vorgezogen

Die Preview-Phase ist der billigste Zeitpunkt für einen breaking change, und der Grund ist nicht „der
Pilot ist klein", sondern: **solange MLM der einzige Konsument ist, ist der Compiler ein vollständiger
Prüfer.** Sobald ein zweiter Host dazukommt, den man nicht parallel baut, wird aus „einmal durchbauen"
ein Anruf.

**`HasPermission(ClaimsPrincipal user, …)` → `HasPermission(…)`.** Vorher geprüft, nicht angenommen: von
**31** Implementierungen liest **keine einzige** den Principal. 62 Deklarationen und Implementierungen,
**278** Aufrufstellen, 115 Dateien.

**Zwei Lehren aus dem Durchlauf**, beide fuer die Planung des nächsten Bruchs wichtiger als die Zahlen:

1. **Die Inventur war unvollständig.** Gesucht wurde nach Bezeichnern (`user`, `currentUser`); die Form
   `auth.User` — ein Member-Zugriff — war nicht gezählt. Drei Stellen fielen durch. Folgenlos, aber nur
   dank des Compilers, nicht dank der Suche.
2. **Ein grüner Teilbau ist kein Beweis.** Im ersten Anlauf scheiterten zwei Projekte; alles, was von
   ihnen abhängt, wurde gar nicht erst gebaut, und der dritte Fehler lag unentdeckt darunter. Die erste
   Fehlerliste nach einem solchen Update ist nie die vollständige — das gilt für MLM genauso.

Geprüft über die **ganze Solution**: 103 Projekte, 0 Fehler, 837 Tests grün.

### Was dabei über die Verdrahtung herauskam — und die Reihenfolge ändert

Beim Abschätzen der Client-Kosten stellte sich heraus: **kein Client schreibt die 47 Typargumente je
aus.** `WebPartInit` löst die Registrierungs-Methoden über `MethodHelper.GetMethod<TDelegate>(contextType,
name)` per Reflexion auf, und die Typparameter werden über ihren **Namen** aus dem DbContext gefüllt.

Das korrigiert den Eintrag unten gleich zweifach:

- **Die 17 generischen Handler-Präambeln sind für Clients gar nicht breaking** — die Arität erscheint in
  keinem Client-Code. Der Eintrag „breaking für Host-Registrierungen" war falsch.
- **Aber es ist der einzige der drei Posten, bei dem der Compiler NICHT schützt.** Die Bindung ist
  namensbasiert und wird beim Start aufgelöst. Ein umbenannter Typparameter sieht aus wie nichts — die
  Registrierung unterbleibt, und man merkt es an einer Ansicht, die fehlt.

Damit dreht sich die Reihenfolge: die Präambeln sind **kein** guter Kandidat für einen Bruch (kein
Client-Nutzen, schwächstes Sicherheitsnetz), `PagedResult<T>`/`ListQuery` dagegen schon — breite
Fläche, aber compiler-gefunden.

Der stille Fehlschlag in der Verdrahtung selbst ist mit behoben (siehe Commit „Eine Registrierung, die
nicht zustande kommt, sagt es jetzt").

---

## C2. Der Nachtrag: welcher Kontext für wen

Nachgetragen 2026-08-24, aus der Anschluss-Untersuchung. Das ist der grösste Fund der Runde und die
Ursache dafür, dass ein Host eine vollständige Infrastruktur gebaut hat, die niemand aufruft.

### Die eine Regel

**Der Runner braucht einen filterfreien Kontext. Die Ansichten brauchen einen gefilterten. Beide zeigen
auf dieselbe Datenbank.**

Der Grund steht in `EfWorkflowStore.LoadInstances:1581`:

```csharp
// HIER wird die Mandanten-Grenze gezogen - fuer alle Suchlaeufe, die vorher nur Kandidaten-Ids
// gesammelt haben.
List<WorkflowInstanceRow> rows = ctx.WorkflowInstances.Where(r => ids.Contains(r.Id)).ToList();
```

Die 33 `IgnoreQueryFilters` im Store sind keine 33 Einzelentscheidungen — sie sammeln Kandidaten-Ids,
und die Mandantengrenze wird **einmal, zentral** gezogen. Läuft der Runner auf einem gefilterten
Kontext, wertet der Mandant dort als `null` aus; das heisst `TenantId IS NULL`, und `LoadInstances`
verwirft danach *jede* Zeile mit Mandant — nicht nur lauffähige Instanzen, sondern ebenso fällige Timer,
Zeitpläne und Nachrichten. Der Runner liefe vollständig leer, ohne eine Meldung.

> **Korrektur an einer früheren Empfehlung dieses Dokuments:** ich hatte vorgeschlagen, `FindRunnable`
> und `FindChildInstances` „wie ihre 33 Geschwister" auf `IgnoreQueryFilters` zu ziehen. Das hätte
> nichts gebracht — die Zeilen wären eine Ebene später trotzdem weggefiltert worden. Die Ursache lag
> nicht bei den Suchläufen, sondern beim Kontext.

### Wie ein Host das seit dem 27.07. verdrahtet

| Weg | Kontext | Registrierung |
|---|---|---|
| Ansichten / Handler | Plugin, **tenant-gefiltert** | `IFreshInjectablePlugin<WorkflowContext>` + `WorkflowEngineFactory` |
| Web-Worker (Runner) | `IDbContextFactory<WorkflowContext>`, **filterfrei** | `AddWorkflowWebWorker()` |

**Nicht mehr nötig** (und seit dem 27.07. von der Bibliothek nicht mehr gezogen): ein DI-registrierter
`IWorkflowStore`, eine DI-registrierte `WorkflowEngine`, ein eigener Hosted Service um den
`WorkflowRunner` aus `ParallelProcessing`. `WorkflowOperation.Engine` baut die Engine pro Operation über
die Factory mit dem frisch geleasten Store.

Wer die drei trotzdem registriert, bekommt keinen Fehler — sie werden schlicht nicht aufgerufen. Genau
das ist passiert: der Vertrag in `WorkflowViewsOptions.ConfigureViews` beschrieb bis heute den Stand vom
24.07., drei Tage vor dem Umbau. Beide Doku-Stellen sind richtiggestellt.

### Was am Worker dafür nötig war

- Store aus der Kontext-Fabrik, wenn keine Umgebung ein Store-Plugin nennt (sonst leaste er den
  gefilterten Kontext der Ansichten)
- eine Standard-Umgebung, wenn gar keine konfiguriert ist — vorher tat der Worker ohne
  `Environments`-Sektion kommentarlos nichts
- `IWorkflowTenantFeatureGate` durchreichen: es wird nur an zwei Stellen gefragt
  (`StartFromMessageTriggers:973`, `RunScheduledStart:1147`), beide worker-seitig, und der Worker
  übergab keins — ein beendetes Abo hielt damit nichts auf

Vier Tests (`WorkflowDefaultEnvironmentTest`) pinnen die Standard-Umgebung fest; der tragende ist
`DescriptorNamesNoStorePlugin`.

---

## D. Was ausdrücklich gesund ist

Damit hier niemand sucht, wo nichts ist.

- **Der Chart-Renderer-Verdacht ist widerlegt.** `ScribanChartRenderer` und `CScriptChartRenderer` teilen
  45 substantielle Zeilen, davon ~30 der von Blazor erzwungene `[Parameter]`-Block. Der Strategy-Umbau
  ist bereits gebaut — per Komposition über `ChartWidgetPanel.Build`.
- **Kein DbContext-Zugriff aus Komponenten** — null Fundstellen über alle fünf View-Pakete. Der
  Handler-Durchstich hält.
- **Kein Identity-UI in AdminViews** — `Microsoft.AspNetCore.Identity` erscheint dort nur in
  Handler-Dateien, nie in einer Komponente.
- **Eine `[LoadWebPartConfig]`- und eine `[ServiceRegistrationMethod]`-Methode je `WebPartInit`** — die
  Regel „eine Methode pro Aspekt" ist eingehalten.
- **Der Workflow-Kern bei der Fehlerbehandlung**: 52 `catch`-Blöcke, 8 formale Verstösse, davon 7
  harmlose `OperationCanceled`-Normalfälle.
- **Keine Deadlock-Gefahr, keine Scope-Lecks**: die zwei `.Result`-Treffer sind korrektes
  `await (await ShowAsync(...)).Result` auf MudBlazor-Dialogen; alle `CreateDbContext()`-Aufrufstellen
  stehen unter `using`.

### Das doppelte Kanten-Routing ist unvermeidbar

`Graph/EdgeRouter.cs` und `wwwroot/graph-editor.js` sind eine Zeile-für-Zeile-Portierung derselben
Rechnung (alle 8 Konstanten, A* inklusive Zustandskodierung, Turn-Penalty, Simplify, Align). Das
zusammenzulegen geht nicht: der Server-Render braucht fertige Pfade ohne JS, und ein Interop-Roundtrip je
`pointermove` ist über Blazor Server nicht flüssig. Beides ist im Code korrekt begründet, und alles was
ausgelagert werden **konnte** — Spurvergabe, Andock-Versätze, Knotengrössen — kommt bereits als `data-*`
vom Server.

**Was fehlt, ist nicht die Zusammenlegung, sondern der Beweis:** `EdgeRoutingTest.cs` hat 8 Tests, alle
nur gegen C#. Der Klassenkommentar sagt, die Zusicherungen seien „testbar statt nur behauptet" — für die
Hälfte, die der Benutzer beim Ziehen sieht, ist das nicht wahr. Ein gemeinsamer Fixture-Satz (JSON mit
~15 Fällen, gelesen von `EdgeRoutingTest` **und** einem kleinen Node-Test) macht die Doppelung
beherrschbar, ohne sie aufzulösen.

Kleiner und lohnend im selben Umfeld: `GraphLayout.Compute` läuft 2-3× pro Mausgeste (einmal in
`AssignSection`, einmal in `CanvasBody` bei **jedem** Render) — auch ein blosser Auswahl-Klick rechnet
das gesamte Layout inklusive A* neu.

---

## E. Nicht bestätigt

Zwei Reviewer-Befunde haben die Gegenprüfung nicht überstanden:

- **`Clone` in `StartConfigEditor` vs. `UserActivityEditor`**: gemeldet als stiller Datenverlust. Die
  Divergenz stimmt (die Start-Fassung lässt `ReadOnly` und `PayloadName` fallen), der Schaden nicht:
  `WorkflowDefinitionValidator.cs:987` hält ausdrücklich fest, dass beide Felder beim Start bedeutungslos
  sind, und übergibt `readOnlyMeaningful: false`. Bleibt ein Konsolidierungs-Kandidat, kein Bug.
- **Tsc-Sortierung**: siehe die Korrektur bei Befund 5.
