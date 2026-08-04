# Issue: Dialog-Inhalt scrollt nie — Aktionsleiste wird aus dem Bild gedrückt (Anstoß aus MLM)

**Status:** ERLEDIGT im Toolkit (siehe „Umsetzung" am Ende) — Host-Gegentest offen
**Datum:** 2026-08-04
**Quelle:** MLMManager-Session (Konsument), gefunden beim Mobil-Test von `/Workflow/Tasks`.
**Toolkit-Stand:** `5.0.0-PRE156` · **MudBlazor:** `9.6.0`
**Betroffen:** `EditDialogDefaults.Edit` / `.Detail` — **95 Verwendungen in 50 Dateien** quer durch
`…Blazor.MudBlazor.AdminViews` und `…Blazor.MudBlazor.WorkflowViews`.

> **Kurzfassung:** `.mud-dialog-content` hat `overflow-y: auto`, scrollt aber nie, weil es als Flex-Item
> auf `min-height: auto` steht. Statt zu scrollen wächst es und drückt `<DialogActions>` aus dem
> Container. Bei langen Formularen sind „Speichern"/„Abbrechen" dadurch nicht mehr erreichbar. Der
> Kern des Fixes ist **eine CSS-Deklaration** — aber sie muss an einer Stelle landen, die Dialoge
> überhaupt erreicht (scoped CSS tut das nicht, siehe unten).

## Symptom

Ein Dialog, dessen Formular höher ist als der sichtbare Bereich, lässt sich nicht mehr abschliessen:
der Inhalt füllt den Dialog, die Aktionsleiste liegt darunter ausserhalb des Bildes. Der Benutzer kommt
per Escape oder Klick daneben zwar heraus, kann den Vorgang aber **nicht bestätigen** — der einzige Weg
aus der Situation ist Abbrechen.

Auf Mobil tritt das sofort auf. Auf dem Desktop ebenso, sobald das Browserfenster niedrig genug ist
oder das Formular genug Felder hat.

## Ursache — der Flexbox-Fallstrick

Gemessen am geöffneten `UserTaskDialog` (Chrome, MudBlazor 9.6.0):

```
.mud-dialog          display: flex;  flex-direction: column;  max-height: none
.mud-dialog-content  flex: 1 1 auto;  min-height: auto;  max-height: none;  overflow-y: auto
```

`.mud-dialog-content` ist ein Flex-Item in einer Column. Ein solches Item scrollt **nur dann**, wenn
`min-height: 0` gesetzt ist. Beim Default `min-height: auto` ist die Mindesthöhe die Inhaltshöhe — das
Item kann also gar nicht kleiner werden als sein Inhalt, `overflow-y: auto` bekommt nie etwas zu tun,
und die Geschwister (Titel, `.mud-dialog-actions`) werden aus dem Container gedrückt.

Das `overflow-y: auto` ist also bereits vorhanden und korrekt gemeint — es läuft nur ins Leere.

**Belegzahlen aus dem konkreten Fall** (Gerät 400 × 978, Dialog maximiert): Dialog 1556 px hoch,
`.mud-dialog-actions` beginnt bei y = 1504, sichtbar sind 978 px → die Buttonleiste liegt **526 px**
ausserhalb.

## Tragweite

`EditDialogDefaults.Edit` / `.Detail` (`…Blazor.MudBlazor/SharedComponents/EditDialogDefaults.cs`)
wird an **95 Stellen in 50 Dateien** verwendet:

| Bereich | Beispiele |
|---|---|
| `AdminViews/TenantSecurityViews` | Tenants (4×), Features, Roles, Permissions, PermissionSets, PlugIns, DashboardWidgets, Localization, AuthenticationTypes, DiagnosticsQueries, GlobalSettings, … |
| `AdminViews/…UserView/UserAdmin` | Users (3×), UserPropertiesGrid, UserClaimsGrid |
| `AdminViews/OnboardingViews` | `EmployeesTab.razor`, `RoleMappingsTab.razor` |
| `AdminViews/HelpViews` | HelpResources, HelpTopicTree, HelpButton |
| `WorkflowViews` | MyTasks, WorkflowInstances (3×), WorkflowInstanceDetailDialog |

Besonders relevant für MLM: `EmployeesTab` und `RoleMappingsTab` werden aus dem **Firmenprofil** heraus
geöffnet — einer Endnutzer-Ansicht, die mobil funktionieren muss. Das ist kein reines Admin-Thema.

## Vorschlag

### Der Kern

```css
.mud-dialog-content { min-height: 0; }
```

Damit wird das vorhandene `overflow-y: auto` wirksam: der Inhalt scrollt, Titel und Aktionsleiste
bleiben stehen. Sinnvoll ergänzt um eine Höhendeckelung, damit ein aufgeblähter Layout-Viewport nicht
durchschlägt:

```css
.mud-dialog { max-height: 100dvh; }
.mud-dialog-content { min-height: 0; overscroll-behavior: contain; }
```

`dvh` statt `vh` ist auf Mobil wichtig — die ein-/ausfahrende Browserleiste auf iOS/Android schneidet
`vh` sonst ab. Für ältere Engines eine `100vh`-Zeile davor als Fallback (beide überleben das
CSS-Bundling, in MLM verifiziert).

### Wo die Regel hin muss — der eigentliche Knackpunkt

Das Toolkit liefert derzeit **kein globales Stylesheet** aus; in den Blazor-Projekten gibt es nur
component-scoped CSS (`CodeEditor.razor.css`, `CScriptField.razor.css`, `WorkflowEditor.razor.css`).
**Scoped CSS greift hier nicht:** `MudDialogProvider` rendert Dialoge in einen eigenen DOM-Teilbaum
ausserhalb der aufrufenden Komponente, die Scope-Attribute passen also nicht — auch `::deep` hilft
nicht.

Drei gangbare Wege, in der Reihenfolge, die ich empfehlen würde:

1. **Ein kleines Stylesheet im Paket `…Blazor.MudBlazor`** (z. B. `wwwroot/itv-mudblazor.css`), das die
   Regel global setzt. Kostet eine neue Zeile im Host (`<link>` in `App.razor`) und damit eine
   Migrations-Notiz für Konsumenten — dafür wirkt es für alle 50 Dateien auf einen Schlag und auch für
   Dialoge, die Konsumenten selbst bauen.
2. **Klasse über die Presets setzen.** `DialogOptions` kennt `Class` (in 9.6.0 aber kein
   `ContentClass`), also `EditDialogDefaults.Edit/.Detail` um `Class = "itv-dialog-fit"` erweitern und
   dazu die Regel `.itv-dialog-fit .mud-dialog-content { min-height: 0 }` ausliefern. Gezielter als 1,
   braucht aber trotzdem einen Ort für das CSS — löst das Grundproblem also nicht allein.
3. **Pro Dialog `ContentClass` am `MudDialog` setzen.** Funktioniert ohne neues Stylesheet, bedeutet
   aber 50 Dateien anfassen und wird bei jedem neuen Dialog wieder vergessen. Nur als Notlösung.

### Optional, unabhängig davon

`DialogMaximizeToggle` (`…/SharedComponents/DialogMaximizeToggle.razor:24`) startet hart mit
`maximized = false`. Ein Parameter `InitiallyMaximized` würde erlauben, auf schmalen Viewports gleich
maximiert zu öffnen. **Wichtig: erst nach dem CSS-Fix sinnvoll** — ohne ihn verschlimmert Maximieren
das Problem, weil die Dialoghöhe dann an den Viewport gebunden wird und die Actions noch weiter nach
unten rutschen (in MLM gemessen: nicht maximiert war derselbe Dialog 319 px hoch und vollständig
sichtbar).

## Abgrenzung

Diese Meldung betrifft **nur** das Dialog-Verhalten und ist bewusst von
[`ISSUE-MLM-WorkflowTasks-Responsive.md`](./ISSUE-MLM-WorkflowTasks-Responsive.md) getrennt, damit sie
unabhängig beauftragt werden kann. Dort geht es um die überlaufende Toolbar in `MyTasks.razor`; beide
Themen treffen sich nur darin, dass die Toolbar den Layout-Viewport aufbläht und die Dialog-Wirkung
dadurch verstärkt.

MLM kann sich als Konsument interimistisch selbst helfen, indem es die Regel in seine `app.css`
schreibt — das ist aber ein Workaround pro Konsument und ersetzt den Toolkit-Fix nicht.

## Verifikationsstand

- Die Flex-Eigenschaften sind am geöffneten Dialog im Browser **gemessen** (nicht aus der
  MudBlazor-Doku übernommen), ebenso die Positionszahlen.
- Die Tragweite (95/50) stammt aus einem Grep über das Toolkit-Repo.
- Dass es kein globales Toolkit-Stylesheet gibt, ist per Dateisuche geprüft.
- **Nicht geprüft:** ob `min-height: 0` auf Desktop irgendwo unerwünschte Wirkung hat. Erwartung ist
  „neutral" (es erlaubt Schrumpfen, erzwingt es nicht), verifiziert ist es nicht. Ebenso ungeprüft, ob
  einzelne Dialoge sich heute unbewusst auf das Wachsen verlassen.
- **Nicht reproduziert** an einem zweiten Dialog ausserhalb von `UserTaskDialog` — die Ursache liegt
  aber in gemeinsamem MudBlazor-CSS, nicht im Dialog selbst.

---

## Umsetzung (Toolkit-Session, 2026-08-04)

Umgesetzt wie unter „Vorschlag" Weg 1 — mit einer Vereinfachung: **der Host muss nichts hinzufügen.**

### Der Knackpunkt war schon gelöst

Die Meldung ging davon aus, das Toolkit habe keinen Weg, ein globales Stylesheet auszuliefern. Den gibt
es: `AddToolkitClientStyleSheet(...)` schreibt in dieselben `ClientResourceOptions`, aus denen
`<ITVentureReferences />` seine `<link>`- und `<script>`-Tags erzeugt — der Mechanismus, über den
heute schon `widget-actions.js` und die BlazorMonaco-Skripte ankommen.

### Was geändert wurde

- **Neu:** `ITVComponents.WebCoreToolkit.Blazor.MudBlazor/wwwroot/itv-mudblazor.css`

  ```css
  .mud-dialog-content { min-height: 0; overscroll-behavior: contain; }
  .mud-dialog                                { max-height: 100vh;  max-height: 100dvh; }
  .mud-dialog:not(.mud-dialog-fullscreen)    { max-height: calc(100vh - 32px); max-height: calc(100dvh - 32px); }
  ```

  Die `vh`-Zeile steht jeweils vor der `dvh`-Zeile als Rückfall für ältere Engines. Die Trennung
  fullscreen / nicht-fullscreen ist der Rand: ein Vollbild-Dialog soll die ganze Fläche nutzen, ein
  normaler behält den Abstand, den MudBlazor sonst auch lässt — sonst stiesse er an den Rand seines
  gepolsterten Containers und würde dort abgeschnitten.

- **Angemeldet** in `WebPartInit.RegisterServices` des Pakets `…Blazor.MudBlazor`, bewusst **nicht** an
  `config.UseViews` gebunden: die Regeln betreffen Mud-Dialoge überhaupt, also auch die, die ein
  Konsument selbst baut.

- **`DialogMaximizeToggle`** hat einen Parameter `InitiallyMaximized` bekommen (Punkt „Optional" der
  Meldung). Er wird im `OnAfterRenderAsync(firstRender)` angewandt — `SetOptionsAsync` während des
  Dialog-Aufbaus zu rufen ist die Sorte Rennen, die sich sporadisch als leerer Dialog zeigt. Toggle und
  Erstanwendung teilen sich eine Methode; zwei ähnliche Options-Listen wären beim nächsten neuen Feld
  auseinandergelaufen.

### Was der Konsument tun muss

**Nichts**, sofern seine statisch gerenderte Host-Seite `<ITVentureReferences />` enthält — was sie
für die Skripte ohnehin braucht. Fehlt die Zeile, kommt das Stylesheet nicht an (genau wie heute schon
die CScript-Felder still nicht rendern, siehe Workflow-Integration-Guide §2.4).

Der Interims-Workaround in der `app.css` von MLM kann danach raus.

### Verifikationsstand

- Beide betroffenen Pakete bauen fehlerfrei (0 Fehler, keine RZ-Diagnostik).
- **Nicht verifiziert:** die Wirkung am laufenden System — CSS-Regeln lassen sich hier nicht testen.
  Der Gegentest aus MLM (Vorschlag am Ende der Meldung) ist der eigentliche Nachweis. Ebenso ungeprüft
  bleibt die in der Meldung genannte Frage, ob `min-height: 0` auf Desktop irgendwo unerwünscht wirkt.
