# Issue: Firmenprofil-Tabs mobil — Beschriftungen quellen aus den Knöpfen, Speichern klebt am Displayrand

**Status:** BEHOBEN im Toolkit (Future_10, noch nicht publiziert) — Host-Test offen
**Datum:** 2026-08-05
**Quelle:** MLMManager-Session (Konsument), Mobil-Durchgang über `/onboarding/billingprofile`, alle vier Tabs.
**Toolkit-Stand:** `5.0.0-PRE159` · **MudBlazor:** `9.6.0` · gemessen bei Gerätebreite **400 px**
**Betroffen:**
- `…Blazor.MudBlazor.AdminViews/OnboardingViews/Components/Admin/RoleMappingsTab.razor` (Hauptbefund)
- die Rechnungsprofil-Maske desselben Bereichs (Speichern-Abstand)
- **potenziell 4 weitere Dateien mit derselben Toolbar-Bauweise** — Liste unten

> **Kurzfassung:** Im Tab „Rollendefinitionen" stehen drei Knöpfe in einer `flex-wrap: nowrap`-Toolbar;
> ihre Beschriftungen brauchen 78/95/104 px, haben aber je 44 px — der Text quillt sichtbar aus den
> Knöpfen heraus. Das ist **strukturell derselbe Befund A wie in
> [`ISSUE-MLM-WorkflowTasks-Responsive.md`](./ISSUE-MLM-WorkflowTasks-Responsive.md)**, dort gefixt,
> hier nicht — und die dortige offene Frage „haben andere Seiten dieselbe Bauweise?" ist damit mit
> **ja** beantwortet, auch ausserhalb der WorkflowViews.

## Befund 1 — Tab „Rollendefinitionen": Beschriftungen quellen aus den Knöpfen

Der auffälligste Punkt. Gemessen im Tab-Inhalt:

| | Container |
|---|---|
| Klasse | `.mud-toolbar .mud-table-toolbar` |
| `display` / `flex-wrap` | `flex` / **`nowrap`** |
| Breite | 368 px |

| Knopf | Knopfbreite | Label braucht | Label hat | quillt über |
|---|---|---|---|---|
| „Direkte Rolle hinzufügen" | 64 px | 78 px | 44 px | **ja** |
| „Berechtigungs-Set hinzufügen" | 64 px | 95 px | 44 px | **ja** |
| „Delegationsrolle hinzufügen" | 64 px | 104 px | 44 px | **ja** |

Die Knopf-*Boxen* überlappen sich rechnerisch nicht — die **Labels** treten aber aus ihrer Box aus und
legen sich optisch über die Nachbarn. Auf dem Schirm ergibt das drei ineinanderlaufende Farbblöcke mit
abgeschnittenem Text; welcher Knopf welcher ist, lässt sich nicht mehr ablesen.

Bemerkenswert: Die Seite läuft dabei **nicht** über (`scrollWidth` = 400 = Gerätebreite). Anders als bei
`MyTasks` bläht sich hier also kein Layout-Viewport auf — die Knöpfe werden stattdessen zusammengedrückt.
Der Defekt ist rein optisch, aber vollständig: die Leiste ist unbenutzbar.

### Vorschlag — bevorzugt ein „Neu"-Menü statt drei Knöpfen

**Vom Konsumenten ausdrücklich so gewünscht:** ein einzelner Knopf **„Neu"**, der ein Menü mit den
verfügbaren Unterpunkten öffnet — „Direkte Rolle", „Berechtigungs-Set", „Delegationsrolle". Die drei
Aktionen sind Varianten derselben Handlung, und das Menü trägt die langen Namen dort, wo Platz ist:
in der Liste statt auf der Knopffläche.

Das löst zugleich beide Seiten des Problems: die Leiste braucht nur noch die Breite eines kurzen
Wortes, und die Bezeichnungen bleiben vollständig lesbar. Naheliegend ist, das **nicht** auf `xs` zu
beschränken — auf breiten Schirmen ist eine Leiste mit drei langen „… hinzufügen"-Knöpfen ebenfalls
unruhig. Falls doch nur mobil umgestellt werden soll, sollten die beiden Fassungen ihre Einträge aus
**einer** gemeinsamen Quelle beziehen, sonst laufen sie beim nächsten Rollentyp auseinander (dieselbe
Überlegung, die bei `MyTasks` zum `RenderFragment FilterControls` geführt hat).

Unabhängig davon bleibt sinnvoll, was bei `MyTasks` in PRE157 schon gemacht wurde: `flex-wrap`
erlauben statt der starren Reihe und ein `min-width` an den Knöpfen, damit dort nichts mehr auf 64 px
zusammenfällt — das schützt die Leiste auch dann, wenn später wieder etwas hinzukommt.

## Befund 2 — Tab „Rechnungsprofil": Speichern klebt bündig am Displayrand

Am Seitenende gemessen:

| | Wert |
|---|---|
| Unterkante „Speichern" | 922 px |
| Sichtbare Viewporthöhe | 922 px |
| **Abstand zum Unterrand** | **0 px** |

Der Knopf sitzt bündig auf der Kante des Formularcontainers (`.mud-form gap-0`), der wiederum bündig
am Seitenende endet; `body` hat kein `padding-bottom`. Auf dem Telefon steht der Knopf damit direkt auf
dem unteren Displayrand — dort, wo bei vielen Geräten die Systemgeste sitzt.

### Vorschlag

Ein kleiner Abstand genügt, es geht nicht um Grosszügigkeit: ein `padding-bottom` (Grössenordnung
`16–24 px`) am Formular- oder Panel-Container des Bereichs. Sinnvollerweise gleich so, dass es auch
für die anderen Tabs gilt.

## Befund 3 — die Tab-Leiste (bereits gemeldet, hier bestätigt)

`.mud-tabs-tabbar-content` braucht **845 px** bei **272 px** Platz; von vier Titeln
(„Rechnungsprofil", „Benutzer & Einladungen", „Rollendefinitionen", „Sub-Mandanten-Einladungen") sind
etwa anderthalb sichtbar. Bedienbar über die beiden Scroll-Pfeile, aber ohne Überblick, welche
Bereiche es gibt.

Das stand schon als Befund D / Anmerkung 1 in der Tasks-Meldung und wurde dort bewusst nicht
mitgezogen („andere Baustelle, andere Datei") — hier ist es die eigentliche Baustelle. Vorschlag
unverändert: auf `xs` ein Dropdown/Select statt der Pfeil-Leiste.

## Was in Ordnung ist

Damit die Meldung nicht zu breit wirkt:

- **Tab „Benutzer & Einladungen"**: kein Überlauf. Die Tabelle ist vorhanden, aber datenleer (0 Zeilen);
  der Knopf „Mitarbeiter einladen" nimmt 52 % der Breite — auffällig, aber kein Defekt.
- **Tab „Sub-Mandanten-Einladungen"**: sauber. Eingabefeld 334 px (84 %), Knopf 220 px, das
  `MudGrid` mit `xs-12` stapelt korrekt.

## Tragweite — dieselbe Bauweise gibt es mehrfach

Die Umsetzung der Tasks-Meldung hielt fest, die Frage nach weiteren Seiten mit derselben Toolbar sei
offen und `WorkflowInstances` der Kandidat. Ein Zählschritt über `AdminViews` und `WorkflowViews`
(`<ToolBarContent>` mit **≥ 3** Knöpfen):

| Datei | Knöpfe in der Toolbar |
|---|---|
| `OnboardingViews/Components/Admin/RoleMappingsTab.razor` | **6** ← dieser Befund |
| `HelpViews/Components/Admin/HelpResources.razor` | 3 |
| `HelpViews/Components/Admin/HelpTopicTree.razor` | 3 |
| `WorkflowViews/…/WorkflowDefinitions.razor` | 3 |
| `WorkflowViews/…/WorkflowInstances.razor` | 3 |

Der Kandidat aus der Tasks-Meldung ist also nicht der einzige, und der schwerste Fall liegt in einem
ganz anderen Paket. Ob die anderen vier real brechen, hängt von ihren Labellängen ab — geprüft ist nur
`RoleMappingsTab` (der Rest sind Admin-Flächen, für MLM nicht mobil-pflichtig). Falls eine
gemeinsame Lösung angedacht wird: eine Toolbar-Variante, die von sich aus umbricht, wäre an fünf
Stellen wiederverwendbar.

## Nachtrag der Toolkit-Session — wie es umgesetzt wurde

Alle drei Befunde erledigt, aber der erste bewusst zweistufig: einmal so, dass er sich nicht wiederholen
kann, und einmal so, wie der Konsument es sich gewuenscht hat.

### Befund 1 — zuerst global, dann als Komponente

**Global:** eine Regel in `Blazor.MudBlazor/wwwroot/itv-mudblazor.css` laesst `.mud-table-toolbar` unter
600px umbrechen (`flex-wrap:wrap`, `height:auto`) und schuetzt die Knoepfe mit `min-width:max-content`.
Damit ist der Defekt „Beschriftung quillt aus dem Knopf" an **allen fuenf** Stellen der Tragweiten-Tabelle
weg — und an jeder Toolbar, die spaeter dazukommt. Die Datei liefert der Host ohnehin ueber
`AddToolkitClientStyleSheet` aus, es war keine Aenderung an einer einzigen `.razor` noetig.

**Als Komponente:** `ToolbarActionMenu` (neu in `Blazor.MudBlazor/SharedComponents/`) nimmt eine Liste von
`ToolbarAction` und rendert einen „Neu"-Knopf mit Menue. Jeder Eintrag traegt seine eigenen Permissions —
wortgleich zu den `SecureView`s, die vorher die einzelnen Knoepfe umschlossen — und das Gating bleibt live
(derselbe `EntityChangeRefresher` wie in `SecureView`, ein entzogenes Recht entfernt den Eintrag ohne
neuen Circuit). Faellt alles weg, verschwindet das Menue statt leer dazustehen.

`RoleMappingsTab` benutzt es und war die Gelegenheit, die offene Frage „eine Quelle oder zwei" zu
beantworten: die drei Anlege-Aktionen kommen aus **einer** Liste, ihre Permissions aus dem schon
vorhandenen `WritePermFor(kind)`, und die Beschriftungen aus `KindLabel(kind)` — derselben Methode, die
jetzt auch die Spalte „Art" fuellt. Eine vierte Rollenart erscheint damit automatisch an beiden Stellen.
Nicht auf `xs` beschraenkt, wie im Vorschlag angeregt.

### Befund 2 — Speichern-Abstand

`mb-6` an den `MudTabs` der Seite. Bewusst dort und nicht als globale `body`-Regel: die Seitenhuelle
gehoert dem Host, das Toolkit soll ihm kein `padding-bottom` aufzwingen.

### Befund 3 — Tab-Leiste

Statt Dropdown/Select (was die Tab-Inhalte haette umbauen muessen): die vier Reiter haben jetzt Icons, und
auf `xs` entfaellt ihre Beschriftung — vier Icons zu je ~48px passen in die gemessenen 272px, also keine
Scrollpfeile und voller Ueberblick. Der Titel bleibt als `ToolTip` erhalten. Der Messpunkt ist `MudHidden`,
was hier geht, weil die Seite interaktiv rendert; auf den static-SSR-Konto-Seiten waere er wirkungslos
(siehe `ISSUE-MLM-IdentityPages-Integration.md`).

**Nicht geprueft:** ob die vier weiteren Toolbars aus der Tragweiten-Tabelle real gebrochen haben — die
CSS-Regel deckt sie ab, gemessen wurde weiterhin nur `RoleMappingsTab`. Ebenso ungeprueft am Geraet: ob
die Icon-Reiter auf 400px tatsaechlich ohne Scrollpfeile auskommen.

## Verifikationsstand

- Alle Zahlen am laufenden System gemessen (angemeldet, DevTools-Device-Emulation, 400 × 978 bzw.
  922 px sichtbar), Tab für Tab durchgeschaltet.
- Die Tragweiten-Tabelle stammt aus einem Zählschritt über die `.razor`-Dateien, **nicht** aus einer
  Messung der jeweiligen Seiten — nur `RoleMappingsTab` ist tatsächlich beobachtet.
- Die Gestaltungsfrage „drei Knöpfe oder ein Menü" ist **entschieden**: der Konsument wünscht das
  „Neu"-Menü (siehe Vorschlag zu Befund 1). Offen bleibt nur, ob es auf `xs` beschränkt wird.
