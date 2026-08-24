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

**Onboarding:** `FlatOnboardingAdminHandler.cs` (775 Z.) und `HierarchyOnboardingAdminHandler.cs`
(776 Z.) sind zu **96,6 %** identisch — nur 53 Zeilen unterscheiden sich, alle 30 Member existieren
beidseitig, kein Verhaltensunterschied. Der Härtetest: `git log` zeigt für beide Dateien **dieselben
6 Commits**. Jede Änderung musste bisher zweimal gemacht werden; beim siebten Mal wird sie es nicht.
Die Abstraktion liegt bereits eine Schicht tiefer (`BillingProfileBase<>`, `EmployeeBase<>`,
`AddressBase<>`), sie wird nur nicht genutzt.

**Stores:** Der Trigger-/Aktivierungs-Lebenszyklus (`SyncTriggers`, `RehomeActivations`,
`EnsureOwnActivation`, Pattern-Reset, `WarnAboutOrphans`) ist in beiden Stores vollständig ausformuliert
— ~180 Zeilen in-memory, ~300 in EF, inklusive wortgleicher Log-Texte. Das ist Fachlogik, keine
Persistenz. **Sie sind bereits auseinander:** der Altbestand-Fang über `DefinitionKey`
(`EfWorkflowStore.cs:225`) existiert nur in EF. Ein grüner In-Memory-Test beweist für diesen Weg nichts
mehr.

Ebenfalls Store-Divergenz: `FindWaitingForSignal`/`FindWaitingForBroadcast` filtern in-memory zusätzlich
über `instance.Status == Waiting`, in EF rein über den Token-Zustand. Eine Instanz mit einem aktiven und
einem wartenden Zweig (Status `Running` — der Normalfall bei parallelen Regionen und bei Nachrichten am
Schritt) findet der EF-Store, der In-Memory-Store nicht.

### Zurückgestellt

- **`HasPermission(ClaimsPrincipal user, …)`**: 33× identisch, `user` wird **nirgends** gelesen — kein
  einziger Ausreisser unter 274 Aufrufstellen, also kein aktiver Bug. Es ist eine geladene Waffe für den
  ersten Aufrufer, der bei Impersonation einen fremden Principal einsetzt. Die Default-Interface-Methode
  ist billig; den Parameter zu streichen ist breaking und gehört an einen Major-Bump.
- **Die 17 generischen Handler-Präambeln** (~1.050 Z.): Aritätsänderung, breaking für
  Host-Registrierungen.
- **`PagedResult<T>`/`ListQuery` dreifach definiert**: Namespace-Wechsel ist breaking für Hosts mit
  eigenen Handler-Implementierungen.

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
