# Issue: Aufgabenliste + Aufgaben-Dialog sind nicht mobiltauglich (Anstoß aus MLM)

> **Kurzfassung:** Zwei unabhängige Ursachen. (1) Die Toolbar in `MyTasks.razor` läuft über, bläht
> dadurch den Layout-Viewport auf und schiebt die Buttons des maximierten Dialogs 526 px aus dem Bild.
> (2) `.mud-dialog-content` scrollt wegen `min-height: auto` nie — das betrifft **alle 50 Dateien**,
> die `EditDialogDefaults` verwenden, nicht nur die Workflow-Views. Die Endnutzer-Views unter
> „Einstellungen" sind dagegen sauber.

**Status:** ERLEDIGT im Toolkit (siehe „Umsetzung" am Ende) — Host-Gegentest offen
**Datum:** 2026-08-03
**Quelle:** MLMManager-Session (Konsument). MLM konsumiert `…Blazor.MudBlazor.WorkflowViews` per PackageReference.
**Toolkit-Stand:** `5.0.0-PRE156`
**Betroffen:**
- `ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews/Tasks/Components/MyTasks.razor`
- `ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews/Tasks/Components/UserTaskDialog.razor`
- `ITVComponents.WebCoreToolkit.Blazor.MudBlazor/SharedComponents/EditDialogDefaults.cs`
- `ITVComponents.WebCoreToolkit.Blazor.MudBlazor/SharedComponents/DialogMaximizeToggle.razor`

## Warum das für MLM zählt

MLM hat ab sofort die Vorgabe, dass **an der Benutzerkante mit dem Mobilgerät gearbeitet werden muss**.
Reine Admin-Ansichten dürfen desktop-only bleiben — `/Workflow/Tasks` gehört aber ausdrücklich **nicht**
dazu: es ist die Liste, die ein normaler Sachbearbeiter täglich benutzt, und die Aufgaben werden
unterwegs abgearbeitet.

Der Kommentar in `MyTasks.razor:15-16` formuliert genau diesen Anspruch bereits selbst:

> „Die zentrale Arbeitsliste. Sie ist die einzige Ansicht dieses Moduls, die ein normaler Benutzer
> taeglich sieht - deshalb lokalisiert (…) und responsiv."

Die Lokalisierung ist eingelöst, die Responsiveness nicht.

## Befund A — Die Toolbar läuft über und quetscht die Bedienelemente

`MyTasks.razor:37-77` legt **sieben** Elemente in ein `<ToolBarContent>`: Refresh-Knopf, Titel (`Typo.h6`),
`WorkflowEnvironmentPicker`, `MudSpacer`, zwei `MudSelect` (`Style="max-width:190px"`), ein `MudSwitch`
und ein `MudTextField` für die Suche. MudBlazors Toolbar ist `flex-wrap: nowrap`, es gibt keine
Breakpoint-Logik.

**Gemessen im Browser** (DevTools-Device-Emulation, angemeldet, Viewport 552 px — also noch nicht einmal
ein schmales Telefon):

| Element | Position/Breite | Zustand |
|---|---|---|
| Toolbar gesamt | 368 px sichtbar, **536 px benötigt** | `flex-wrap: nowrap`, läuft über |
| `WorkflowEnvironmentPicker` (`.ml-3`) | Breite **0 px** | vollständig kollabiert |
| `MudSpacer` (`.flex-grow-1`) | Breite **0 px** | wirkungslos, kann nicht mehr ausgleichen |
| Suchfeld (letztes `mud-input-control`) | x = 520, Breite **32 px** | auf ein Fragment gequetscht **und aus dem Viewport geschoben** |

Bei 400 px Breite bricht zusätzlich der Titel „Offene Aufgaben" zweizeilig um und schiebt die Zeile
weiter auseinander. Das feste `Style="max-width:190px"` an den beiden Selects (`MyTasks.razor:53` und
`:60`) begrenzt nur nach oben — nach unten gibt es keine Anpassung.

**Netto:** Auf dem Telefon sind Umgebungswahl und Suche faktisch nicht bedienbar.

### Die Toolbar ist die alleinige Ursache des Überlaufs — und damit auch von Befund C

Das ist der wichtigste Zusammenhang dieser Meldung. Gemessen bei Gerätebreite 400 px, mit geschlossenem
Dialog, über alle Elemente ausserhalb von Dialog/Overlay:

| Element | braucht | hat |
|---|---|---|
| `.mud-toolbar` | **620 px** | 368 px |
| `.mud-table.mud-data-grid` | 620 px | 368 px | ← erbt es nur, weil `ToolBarContent` im Grid sitzt |
| `.mud-table-head` / `.mud-table-body` | 368 px | 368 px | ← sauber |
| `.content` → `main` → `.page` | 636 px | 400 px | ← die Kette nach oben |

Es gibt **keine zweite Überlaufquelle**. Die Toolbar allein treibt `documentElement.scrollWidth` auf
**636 px** bei 400 px Gerätebreite. Genau daraus folgt der aufgeblähte Layout-Viewport, der in Befund C
die Dialog-Buttons aus dem Bild schiebt:

```
Toolbar braucht 620 px bei 368 px Platz
  → Seite braucht 636 px bei 400 px Gerät
    → Layout-Viewport wird auf 636 × 1556 aufgezogen (Faktor 1,59)
      → FullScreen-Dialog wird 1556 px hoch
        → Aktionsleiste bei y = 1504, sichtbar sind nur 978 px
          → „Erledigen"/„Schliessen" liegen 526 px ausserhalb
```

**Praktische Folge für die Umsetzung:** Wer die Toolbar repariert, nimmt dem Dialog-Problem seine
Grundlage gleich mit. Der Fix aus Vorschlag 1 bleibt trotzdem nötig — ein langes Formular kann auch bei
korrektem Viewport überlaufen — aber die Reihenfolge ist damit klar.

## Befund B — Das Grid selbst ist in Ordnung (nur die Dichte ist es nicht)

**Korrektur gegenüber der ersten Fassung dieser Meldung:** Das Grid ist mobil bereits korrekt. Gemessen:
es trägt `mud-xs-table`, die Body-Zellen sind mit `data-label` auf `display: flex` gestapelt
(„Aufgabe / Antwort von chorche", „Zuständig / Nicht zugewiesen", …), die Kopfzeile ist kollabiert
(alle `th` auf **0 px**), und Kopf wie Body liegen bei **368 px** — also **kein** Überlauf. Die
ursprüngliche Behauptung „sechs Spalten konkurrieren um den Platz" war falsch.

Was bleibt, ist eine reine Dichte-Frage: alle sechs Felder werden gestapelt, also **sechs Zeilen pro
Aufgabe**. `CreatedUtc` und `CorrelationKey` (`MyTasks.razor:99` und `:116`) kosten damit je eine
volle Zeile, obwohl sie für die tägliche Arbeit selten tragen. Auf 978 px sichtbarer Höhe passen so
nur wenige Aufgaben ins Bild. Das ist Komfort, kein Defekt — siehe Vorschlag 4.

## Befund C — Maximiert liegt die Buttonleiste 526 px ausserhalb des Sichtbaren

Das ist der Punkt, der beim Arbeiten am meisten weh tut — und er ist **am echten, geöffneten Dialog
gemessen** (angemeldet, Aufgabe aus Scope „nicht zugewiesen", Gerät 400 × 978).

Zwei Größen laufen hier auseinander:

| | Breite | Höhe |
|---|---|---|
| **Layout-Viewport** (was CSS, `100vh` und `FullScreen` sehen: `window.innerWidth/Height`) | 636 | **1556** |
| **Visual Viewport** (was der Benutzer tatsächlich sieht: `window.visualViewport`) | 400 | **978** |

Der Layout-Viewport ist um Faktor 1,59 aufgezogen, obwohl `App.razor` ein korrektes
`<meta name="viewport" content="width=device-width, initial-scale=1.0">` mitbringt. Die Seite belegt
mehr Breite als das Gerät hat (`document.documentElement.scrollWidth` = 636 bei 400 px Gerät) — das ist
**Befund A, der hier zurückschlägt**: die überlaufende Toolbar bläht den Layout-Viewport auf.

Damit rechnet `FullScreen` mit 1556 px Höhe, sichtbar sind aber 978 px. Gemessen im maximierten Zustand:

- `.mud-dialog.mud-dialog-fullscreen`: `height: 1556px`, `max-height: none`
- `.mud-dialog-actions` beginnt bei **y = 1504** → **526 px unterhalb** des sichtbaren Bereichs
- „Erledigen" und „Schliessen" sind damit schlicht nicht vorhanden für den Benutzer

**Maximieren verschlimmert das Problem, es löst es nicht.** Im nicht-maximierten Zustand war derselbe
Dialog 319 px hoch, zentriert, und alle Buttons lagen im Bild. Erst `FullScreen` bindet die Dialoghöhe
an den aufgeblähten Layout-Viewport und schiebt die Aktionsleiste hinaus.

### Warum der Content das nicht abfängt — der Flexbox-Fallstrick

Naheliegend wäre, dass der Content scrollt und die Actions dadurch im Bild bleiben. Er tut es nicht,
und der Grund ist eine klassische Flexbox-Falle. Gemessen an `.mud-dialog-content`:

```
flex:       1 1 auto
min-height: auto      ← das ist das Problem
max-height: none
overflow-y: auto      ← wirkungslos, solange min-height:auto gilt
```

`.mud-dialog` ist `display:flex; flex-direction:column`. Ein Flex-Item mit `overflow-y: auto` scrollt
**nur dann**, wenn `min-height: 0` gesetzt ist. Bei `min-height: auto` (dem Default) wächst das Item
stattdessen auf seine Inhaltshöhe und drückt die Geschwister — hier die Aktionsleiste — aus dem
Container. `overflow-y: auto` ist also bereits vorhanden, läuft aber ins Leere.

Das ist die gute Nachricht an der Sache: **eine einzige CSS-Deklaration** (`min-height: 0` auf dem
Content) ist der Kern des Fixes.

### Erschwerend

Wenn der Dialog per Escape oder Klick daneben geschlossen wird, ist das laut
`UserTaskDialog.razor:207-212` sauber abgefangen (die Sperre wird freigegeben) — der Benutzer *kann*
also rauskommen. Er kann die Aufgabe nur nicht **abschliessen**, weil der Knopf dafür ausserhalb des
Bildes liegt. Der Weg aus der Situation ist damit immer „Aufgabe unerledigt verlassen".

Nebenbei: `MyTasks.razor:263` übergibt `L["PageTitle"]` als Dialogtitel, im geöffneten Dialog steht
deshalb „Meine Aufgaben" statt des Aufgabentitels. Auf dem knappen Mobil-Schirm ist das eine
verschenkte Zeile.

## Befund D — Die Endnutzer-Views unter „Einstellungen" sind sauber (mit zwei Anmerkungen)

Auf Wunsch aus MLM gegengeprüft, weil auch diese Views zur mobilen Pflicht gehören. Alle vier bei
Gerätebreite 400 px gemessen:

| View | Route | Layout-Viewport | Seitenüberlauf |
|---|---|---|---|
| Mein Profil | `/Account/Manage` | 400 = Gerät | keiner |
| Meine Mandanten | `/Account/Onboarding/MyTenants` | 400 = Gerät | keiner |
| Meine Abonnements | `/Account/Manage/Subscription` | 400 = Gerät | keiner |
| Firmenprofil | `/onboarding/billingprofile` | 400 = Gerät | keiner |

**Kein einziger Layout-Viewport ist aufgebläht** — das Problem aus Befund A/C existiert hier nicht, die
Seiten halten sich sauber an die Gerätebreite. Der Vergleich stützt zugleich die Diagnose: die
Aufblähung auf `/Workflow/Tasks` kommt tatsächlich von der dortigen Toolbar und ist kein
allgemeines Layout-Problem der Anwendung.

Zwei Anmerkungen, beide klein:

1. **Firmenprofil: die Tab-Leiste ist mobil unübersichtlich.** `.mud-tabs-tabbar-content` braucht
   **845 px** bei 262 px. Es gibt vier Tabs mit langen Titeln („RECHNUNGSPROFIL", „BENUTZER &
   EINLADUNGEN", „ROLLENDEFINITIONEN", „SUB-MANDANTEN-EINLADUNGEN"), sichtbar sind eineinhalb.
   MudTabs blendet korrekt zwei Scroll-Pfeile ein, es ist also **bedienbar** — aber man sieht nie,
   welche Bereiche es überhaupt gibt. Auf `xs` wäre ein Dropdown/Select statt der Pfeil-Leiste die
   bessere Führung. Der „Speichern"-Knopf liegt im sichtbaren Bereich.
2. **„Meine Mandanten" wurde nur im leeren Zustand geprüft** („Sie sind noch an keinem Mandanten
   beteiligt"). Sobald dort eine Liste mit Daten erscheint, wäre erneut zu messen.

### Tragweite des Dialog-Fixes: er ist nicht auf WorkflowViews beschränkt

`EditDialogDefaults.Edit` / `.Detail` wird an **95 Stellen in 50 Dateien** verwendet — quer durch
`AdminViews` (TenantSecurityViews, UserAdmin, HelpViews) und `WorkflowViews`. Darunter auch
`OnboardingViews/Components/Admin/EmployeesTab.razor` und `RoleMappingsTab.razor`, also Dialoge, die
**genau aus dem Firmenprofil unter „Einstellungen" heraus** geöffnet werden.

Der Flexbox-Fallstrick aus Befund C trifft damit potenziell jeden dieser Dialoge, sobald sein Formular
höher wird als der sichtbare Bereich. **Der Fix gehört deshalb zentral** in `EditDialogDefaults` bzw.
in das Dialog-CSS — eine Reparatur nur in `UserTaskDialog` würde 49 weitere Dateien ungefixt lassen.

> **Dieser Teil ist als eigene Meldung ausgelagert:**
> [`ISSUE-MLM-Dialog-Content-Scrolling.md`](./ISSUE-MLM-Dialog-Content-Scrolling.md) — damit der
> generische Dialog-Fix unabhängig von den Workflow-Views beauftragt werden kann. Dort steht auch der
> Knackpunkt, wo die CSS-Regel überhaupt hin kann (scoped CSS erreicht Dialoge nicht, weil der
> `MudDialogProvider` sie ausserhalb der aufrufenden Komponente rendert).
> **Was in DIESER Meldung bleibt:** die überlaufende Toolbar (Befund A) — sie ist MyTasks-spezifisch
> und bleibt auch nach dem Dialog-Fix bestehen.

## Vorschläge

Priorisiert; 1 und 2 lösen den akuten Schmerz.

### 1. Content wirklich scrollen lassen, Höhe an den *sichtbaren* Bereich binden

Zielverhalten: Der Dialog nutzt die Fläche, ist aber **nie höher als der sichtbare Bereich**; die
Aktionsleiste ist **immer sichtbar**; der Inhalt scrollt in sich.

Reihenfolge nach Wirkung:

1. **`min-height: 0` auf `.mud-dialog-content`.** Das ist der eigentliche Fix (siehe Flexbox-Fallstrick
   oben). Erst damit wird das vorhandene `overflow-y: auto` wirksam, der Content scrollt, und Titel und
   Aktionsleiste bleiben als Flex-Items stehen. Ohne diese Zeile helfen alle anderen Maßnahmen nur
   zufällig.
2. **Höhe an `100dvh` statt an `100vh`/`FullScreen` binden.** `dvh` folgt der ein-/ausfahrenden
   Browserleiste auf iOS/Android; MLM hat genau das gerade im Navigationsmenü gebraucht. Zusätzlich
   `max-height: 100dvh` auf `.mud-dialog`, damit ein aufgeblähter Layout-Viewport (Befund A) nicht
   durchschlägt.
3. **Erst danach „von Anfang an maximiert".** `DialogMaximizeToggle` einen Parameter
   `InitiallyMaximized` geben, statt hart `maximized = false` (`DialogMaximizeToggle.razor:24`), und
   ihn auf schmalen Viewports setzen (MudBlazor `IBreakpointService`) — bzw. ein Preset `Task` /
   `MobileFirst` in `EditDialogDefaults`. **Wichtig: diese Maßnahme allein macht es schlimmer**, wie
   oben gemessen. Sie ist erst sinnvoll, wenn 1 und 2 stehen.
4. `overscroll-behavior: contain` auf dem scrollenden Content, damit das Wischen im Formular nicht die
   Liste dahinter mitzieht.

### 2. Filter kollabierbar, Toolbar umbruchfähig

- Refresh, Titel und die Aktionen bleiben sichtbar; **Umgebung, Ansicht, Art, „Nur überfällig" und Suche
  wandern auf schmalen Viewports in einen einklappbaren Bereich** (`MudCollapse`/Expansion-Panel oder ein
  Filter-Knopf, der ein `MudPopover`/Bottom-Sheet öffnet). Eingeklappt sollte er anzeigen, **wie viele
  Filter aktiv sind**, damit nichts unbemerkt gesetzt bleibt — der Scope steht per Default auf `Mine`
  (`MyTasks.razor:154`), das ist bereits ein wirksamer Filter.
- Solange die Controls in der Toolbar bleiben: `flex-wrap: wrap` erlauben und die festen
  `max-width:190px` durch etwas Fluides ersetzen (`min-width` + `flex`), damit nichts auf 0 bzw. 32 px
  kollabiert.

### 3. Grundmaß kleiner

Der Wunsch aus MLM ist ausdrücklich „alles etwas kleiner". Konkret: `Size.Small` für die Icon-Buttons,
`Margin.Dense` konsequent (bei den Selects schon gesetzt, beim Suchfeld nicht), kompaktere Typo für den
Grid-Titel — `Typo.h6` (`MyTasks.razor:39`) ist auf 400 px der Grund für den Zweizeiler. Das ist die
billigste Maßnahme mit sofort sichtbarer Wirkung.

### 4. Weniger Zeilen pro Aufgabe (Komfort, kein Defekt)

Da das Grid mobil stapelt (Befund B), kostet jedes Feld eine eigene Zeile. Auf `xs`/`sm` nur Titel,
Frist und Aktion zeigen und „Zuständig", Erstellt und Bezug ausblenden — oder Erstellt/Bezug in die
Titelzelle als zweite Zeile (`Typo.caption`) einhängen; das Muster gibt es in `MyTasks.razor:82-83`
für `TaskKey` bereits. MudDataGrid kann Spalten über `Hidden` binden, das ließe sich an einen
Breakpoint hängen.

## Abgrenzung

Alles oben liegt in Toolkit-Paketen; MLM kann es nicht sinnvoll konsumentenseitig überschreiben (die
Dialog-Optionen entstehen in `MyTasks.razor` selbst, nicht an einer von aussen erreichbaren Stelle).
MLM-eigene Mobile-Themen (Navigationsmenü, Layout-Shell) sind separat und bereits erledigt.

Falls gewünscht, kann MLM als Konsument gegentesten, sobald ein PRE-Paket bereitsteht.

## Verifikationsstand dieser Meldung

Alles am laufenden System gemessen (MLM auf `5.0.0-PRE156`, angemeldet, DevTools-Device-Emulation,
Gerät 400 × 978), nichts davon geraten:

- **Befund A** — gemessen; zusätzlich per Ausschluss belegt, dass die Toolbar die **einzige**
  Überlaufquelle der Seite ist (Kopf/Body des Grids liegen sauber bei 368 px).
- **Befund B** — am echten Grid mit Daten gemessen und dabei **widerlegt**: `mud-xs-table`, gestapelte
  Zellen mit `data-label`, Kopfzeile kollabiert, kein Überlauf.
- **Befund C** — **am echten, geöffneten Aufgaben-Dialog gemessen**, in beiden Zuständen: nicht
  maximiert (319 px hoch, alle Buttons im Bild) und maximiert (1556 px hoch, Aktionsleiste bei y=1504
  bei 978 px sichtbarem Bereich → 526 px ausserhalb). Layout- vs. Visual-Viewport und die
  Flex-Eigenschaften von `.mud-dialog-content` sind ebenfalls gemessen, nicht aus der MudBlazor-Doku
  übernommen.

Zwei Korrekturen gegenüber früheren Fassungen dieser Meldung, beide entstanden durch Nachmessen am
echten System statt Ableiten aus dem Quellcode:

1. `.mud-dialog-content` hat sehr wohl `overflow-y: auto` — es ist nur wegen `min-height: auto`
   wirkungslos. „Kein Overflow auf dem Content" war zu grob und hätte an der falschen Stelle repariert.
2. Das Grid ist mobil bereits responsiv; der ursprüngliche Befund B („sechs Spalten konkurrieren um den
   Platz") war schlicht falsch. Irreführend war, dass `.mud-table.mud-data-grid` tatsächlich 620 px
   braucht — aber nur, weil `ToolBarContent` innerhalb des Grids liegt und dessen Bedarf durchschlägt.

- **Befund D** — alle vier Endnutzer-Views unter „Einstellungen" einzeln im Browser gemessen; die
  Tragweite des Dialog-Fixes (95 Fundstellen / 50 Dateien) per Grep über das Toolkit-Repo ermittelt.

**Nicht geprüft:** ob die Vorschläge auf Desktop regressionsfrei sind. Insbesondere `min-height: 0`
und eine `100dvh`-Deckelung sollten dort neutral sein, das ist aber nicht verifiziert. Ebenso offen:
ob weitere WorkflowViews-Seiten (Instanzen-Monitoring, Designer) dieselbe Toolbar-Bauweise haben —
`MyTasks.razor` war der einzige daraufhin geprüfte Fall. `WorkflowInstances.razor` nutzt
`EditDialogDefaults` dreimal, ist also für den Dialog-Teil ein wahrscheinlicher Kandidat.
„Meine Mandanten" wurde nur im leeren Zustand gesehen.

---

## Umsetzung (Toolkit-Session, 2026-08-04)

Befund A, C, die Dichte aus B und die beiden Nebenbemerkungen sind umgesetzt. Der generische Dialog-Teil
steckt in [`ISSUE-MLM-Dialog-Content-Scrolling.md`](./ISSUE-MLM-Dialog-Content-Scrolling.md) — dort
ist er erledigt und braucht **keine** Host-Änderung.

### Befund A — die Toolbar

Der Zuschnitt folgt Vorschlag 2, mit einer Entscheidung, die die Meldung offengelassen hat: **die Filter
existieren nur EINMAL im Markup.** Sie stehen als `RenderFragment FilterControls` im Code-Block und
werden an zwei Stellen benutzt — auf breiten Geräten in der Leiste, auf schmalen in einem
`MudCollapse` darüber. Zwei Abschriften wären beim nächsten Filter auseinandergelaufen.

Woher die Engine weiss, welcher Fall gilt: ein einzelner `<MudHidden Breakpoint="Breakpoint.SmAndDown"
@bind-Hidden="narrow" />` ohne Inhalt — ein reiner Messpunkt. Toolbar, Klappbereich **und** die
Spaltenauswahl hängen an derselben Variablen; drei eigene Breakpoint-Abfragen hätten sich
auseinanderentwickelt.

Auf schmalen Geräten trägt die Leiste damit nur noch: Aktualisieren, Titel, Abstandhalter, Filter-Knopf.
Der Knopf trägt eine **Badge mit der Zahl der wirksamen Filter** (wie in der Meldung gefordert) — der
Bereich zählt dabei nur mit, wenn er *nicht* auf „Meine" steht, sonst zeigte der Knopf dauerhaft eine 1
und die Zahl sägte an ihrer eigenen Aussage.

Zusätzlich innerhalb der Filterzeile: `flex-wrap` statt der starren Reihe, und `min-width` neben dem
vorhandenen `max-width` an den beiden Selects. Nach oben zu begrenzen hat nicht verhindert, dass sie auf
0 px zusammenfielen — das war die eigentliche Ursache der gequetschten Bedienelemente.

### Befund B / Vorschlag 4 — Dichte

`CreatedUtc` und `CorrelationKey` bekommen `Hidden="@narrow"`. Mobil stapelt das Grid die Zellen, jedes
Feld kostet also eine ganze Zeile; ohne diese beiden passen doppelt so viele Aufgaben ins Bild. Die
Spalten sind auf breiten Geräten unverändert da.

### Vorschlag 3 — Grundmaß

`Typo.h6` → `Typo.subtitle1` (der Zweizeiler auf 400 px), `Size.Small` am Aktualisieren-Knopf und am
Schalter, `Margin.Dense` am Suchfeld (fehlte als einziges).

### Nebenbemerkung — Dialogtitel

Der Aufgaben-Dialog trägt jetzt den **Titel der Aufgabe** statt „Meine Aufgaben". Beim Deep-Link aus einer
Benachrichtigungs-Mail steht er noch nicht zur Verfügung — dort bleibt es beim Seitentitel.

### Neuer Ressourcen-Schlüssel

`Filters` in allen vier `WorkflowTaskMessages`-Dateien (en/de/fr/it).

### Vorschlag 1, Punkt 3 — auf schmalen Geräten von selbst maximiert

Nachgereicht und **als Standard** gesetzt (`DialogMaximizeToggle.MaximizeOnMobile = true`, Umschaltpunkt
`MobileBreakpoint = SmAndDown`). Die Meldung hatte gewarnt, Maximieren mache es schlimmer — das galt
*vor* dem CSS-Fix. Mit scrollendem Content ist die Rechnung umgekehrt: der Dialog nimmt die volle Fläche,
der Inhalt scrollt in sich, Titel und Aktionsleiste bleiben stehen. Nicht maximiert steht derselbe Dialog
als schmaler Kasten in einem gepolsterten Container, und was nicht hineinpasst, ist weg.

Der Zwang, dass der Inhalt in die Fläche passen muss, ist der bessere Handel: passt er nicht, wird
gescrollt — die Knöpfe am Fuss bleiben erreichbar.

Zwei Dinge, die dazugehören:

- **Die Automatik tritt zurück**, sobald jemand den Knopf selbst benutzt (oder `InitiallyMaximized`
  gesetzt ist). Sonst spränge der Dialog beim Drehen des Telefons ungefragt zurück.
- **Voraussetzung ist, dass der Inhalt in `DialogContent` steht.** Beim `UserTaskDialog` ist das der
  Fall — auch die eigene Maske (`DynamicComponent`) rendert dort hinein. Aber: eine Maske, die ihre
  **eigenen Aktionen mitbringt** (`UserTaskDialog.razor:41-43`), legt diese Knöpfe damit *in* den
  scrollenden Bereich. Sie sind erreichbar, aber nicht angeheftet — anders als „Erledigen"/„Schliessen"
  des Mantels. Wer eine Maske mit eigenen Aktionen baut, sollte sie oben halten oder kurz genug.

Nur Dialoge mit `<DialogMaximizeToggle />` sind betroffen (das war schon vorher die Opt-in-Grenze) —
also genau die, denen jemand die Maximieren-Schaltfläche gegeben hat, weil ihr Inhalt gross ist.

### Was NICHT umgesetzt ist
- Die **Tab-Leiste im Firmenprofil** (Befund D, Anmerkung 1) — andere Baustelle, andere Datei,
  bewusst nicht mit hineingezogen.
- Die offene Frage aus der Meldung, ob **andere WorkflowViews-Seiten** dieselbe Toolbar-Bauweise haben.
  `WorkflowInstances.razor` ist der genannte Kandidat und ist **nicht** angefasst; es ist eine
  Monitoring-Ansicht, keine tägliche Endnutzer-Fläche. Falls sie mobil gebraucht wird: dasselbe Muster,
  eine Datei.

### Verifikationsstand

- `…Blazor.MudBlazor` und `…Blazor.MudBlazor.WorkflowViews` bauen fehlerfrei, keine RZ-Diagnostik.
- **Nicht verifiziert:** das Verhalten am laufenden System. Insbesondere der Umschaltpunkt (`SmAndDown`)
  und die Frage, ob der erste Render kurz die breite Fassung zeigt, bevor der Breakpoint-Dienst meldet
  — die Filterzeile bricht in beiden Fällen um, ein Überlauf entsteht dabei also nicht. Der Gegentest
  aus MLM ist der Nachweis.
