# Plan: Austauschbare Widget-Renderer (`IWidgetRenderer`)

**Status:** **AP0–AP6c umgesetzt** (2026-08-11; AP0–5 committed als `6e7324ea`, AP6 uncommitted). Build
grün, 48 Tests grün, **nicht laufzeit-getestet**. Offen: der Host-Migrationsschritt (zwei Spalten,
Leitfaden §25.9) und der Host-Test.

**Beim Umsetzen gelernt** (die Beispiele weiter unten waren teilweise falsch, siehe §25.9.3 im Leitfaden):
CScript verlangt für Text **doppelte** Anführungszeichen — einfache bezeichnen einen Typ; und ein Ausdruck
darf **nicht mit `{` beginnen** (Grammatik-Prädikat), weshalb der Renderer das Objektliteral selbst
einklammert. `ChartType.Pie` funktioniert, weil der Typ als Variable im Geltungsbereich liegt.
**Datum:** 2026-08-11
**Grundlage:** `ISSUE-MLM-Dashboard-Widget-Renderers.md` (Anforderung aus dem Konsumenten).
**Toolkit-Stand:** `5.0.0-PRE171`. Alles hier Genannte ist gegen diesen Stand gelesen; wo etwas abgeleitet
und nicht geprüft ist, steht es dabei.

## 1. Was gegenüber dem Issue korrigiert bzw. präzisiert ist

### 1.1 Die Zeilenform — der Issue lag hier daneben (und mein erster Vorschlag auch)

Es gibt **keine `DynamicResult`-Zeilen** im Diagnostics-Pfad. `DynamicResult` (`ITVComponents.DataAccess`)
gehört zu einer anderen Schiene, die hier nicht benutzt wird. Tatsächlich kann `ContextForDiagnosticsQuery`
(`EntityFramework/Extensions/ServicesExtensions.cs:50-94`) **genau zwei** Datenquellen liefern — alles
andere wirft `InvalidOperationException`, die Liste ist also vollständig und nicht offen:

| Quelle | Weg | Was in `Rows` liegt |
|---|---|---|
| `DbContext` | `WrappedDbContext` → `ContextExtensions.RunDiagnosticsQuery` → `NativeScriptHelper.RunLinqQuery` | was das CScript-/LINQ-Skript zurückgibt: typischerweise anonyme Typen oder Entities → **Reflection** |
| `DynamicDataAdapter` | `WrappedDynamicDataAdapter.RunDiagnosticsQuery` → `DynamicDataAdapter.SqlQuery(string, IDictionary<string,object>)` | laut Signatur `IEnumerable<IDictionary<string,object>>` → **Dictionary** |

Ein gemeinsamer Spaltenzugriff braucht also **zwei** Fälle (Dictionary, Reflection) plus den Skalar-Fall
(die Query gibt eine nackte Zahl zurück). `IBasicKeyValueProvider` wird **nicht** gebraucht.

### 1.2 `WidgetRenderer.Data` ist `object?`, nicht `WidgetTemplateModel`

`SharedComponents/Widgets/WidgetRenderer.razor:47`. Die Komponente ist heute ein allgemeines
„Scriban gegen irgendwas"-Werkzeug. Sie auf den typisierten Wert zu verengen wäre ein Bruch für jeden, der
sie direkt benutzt. **Der heutige `WidgetRenderer` wird deshalb nicht angefasst**; die Vertragserfüllung
übernimmt eine dünne Komponente `ScribanWidgetRenderer`, die intern `<WidgetRenderer …>` rendert.

### 1.3 Die Hausform für Erweiterungspunkte existiert bereits — und kennt **nur** den Code-Weg

`CustomCompanyInfoViewConfiguration` (`AdminViews/OnboardingViews/Extensibility/`) ist dieselbe Form:
Schlüssel → Komponente, `RegisterView<T>() where T : IComponent, IVertrag`, Doppelvergabe wird abgewiesen,
Schlüssel case-insensitiv, Tests daneben. Angebunden ist sie über
`services.ConfigureCustomCompanyInfoViews(c => c.RegisterView<T>("key"))` — also `services.Configure<T>`
(IOptions). **Einen Konfigurationsweg (Typname aus JSON) gibt es dort nicht**, und der Klassenkommentar
begründet das ausdrücklich: ein Typname als Zeichenkette machte Konfigurationspflege gleichbedeutend mit
Code-Ausführung.

Für die Widget-Renderer gilt (abgestimmt): **Code-Registrierung ist der Vertrag, der Konfigurationsweg
kommt danach als dünne Schale**, die dieselbe Registrierung füllt.

## 2. Entschieden

1. **Registrierungsweg:** erst Code (`ConfigureWidgetRenderers`), danach optional Konfiguration
   (`WebPartConfig.WidgetRenderers` mit Typauflösung über `ExpressionParser` + Start-Validierung).
2. **Konfigurationsformat der Kachel: bleibt ein Template, kein statisches JSON.** Begründung des
   Konsumenten: der Konfigurationstext hat heute *Markup-Charakter* — er nimmt die Daten und bereitet sie
   für die Darstellung auf. Die Query liefert unspezifisch geformte Daten; die Umformung gehört in den
   Konfigurationstext, nicht in den Renderer. Ein reines JSON-Dokument könnte das nicht.
3. **Für Diagramme gibt es ZWEI Renderer, nicht einen** — einen mit Scriban, einen mit CScript. Beide enden
   in derselben Zwischenform, der gemeinsame Teil (Deklaration → `MudChart`) liegt also nur einmal vor.
   Begründung des Konsumenten: CScript ist ihm geläufiger, erlaubt bei Bedarf LINQ-Abfragen über die Zeilen
   und kennt native C#-Ausdrücke, die Scriban nicht hat. Ausgestaltung: 3.3.
4. **Unbekannter `RendererKey` → Fehler-Kachel, kein stiller Fallback.** Sie nennt, *was verlangt wurde*
   und *was verfügbar wäre* (die registrierten Schlüssel mit Anzeigenamen). Ein Tippfehler soll sofort
   sichtbar sein, statt sich als „die Kachel sieht anders aus als gedacht" zu tarnen.
5. **Der Schlüssel kommt aus einem Attribut am Renderer-Typ**, nicht aus einer Zeichenkette am
   Registrierungsaufruf (3.2). Damit kann er weder beim Registrieren noch in der Konfiguration
   verschrieben werden.
6. **CScript: Ausdruck oder Block wird ausdrücklich gewählt**, nicht geraten — wie im Workflow-Editor
   (`WorkflowViews/Design/Components/CScriptField.razor`, Umschalter *Expression* / *Script block*).
   Offen ist nur, **wo** die Wahl gespeichert wird (5.a).
7. **Klicks sind in erster Linie Navigation** (3.4). Das HTML-Template kann heute `<a href>` schreiben;
   das Diagramm bekommt dieselbe Möglichkeit über ein optionales Ziel je Beschriftung.
8. **Editor-Prüfung (AP4) und Konfigurationsweg (AP5) sind Teil der ersten Umsetzung**, nicht später.
9. **Telerik bleibt unangetastet.** Der `RendererKey` ist im alten Editor nicht pflegbar und hätte im alten
   Dashboard ohnehin keine Wirkung (dort rendert das JS ausschliesslich Scriban). Wird im Leitfaden vermerkt.

## 3. Verträge

### 3.1 Der Renderer

```csharp
namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets;

public interface IWidgetRenderer
{
    string TemplateSource { get; set; }              // der Konfigurationstext des Widgets
    WidgetTemplateModel? Data { get; set; }          // Rows / Row / Count / Params / Title
    EventCallback<WidgetAction> OnAction { get; set; }
    EventCallback<Exception> OnRenderError { get; set; }
}
```

Eine Implementierung ist eine gewöhnliche Blazor-Komponente mit `@implements IWidgetRenderer` und
`[Parameter]`-Properties.

### 3.2 Registrierung — der Schlüssel steht am Typ

```csharp
[WidgetRenderer("chart.cscript",
    DisplayName = "{\"de\":\"Diagramm (CScript)\",\"Default\":\"Chart (CScript)\"}",  // Kultur-JSON erlaubt
    EditorLanguage = "csharp")]                                        // Monaco-Sprache im Editor
public partial class CScriptChartRenderer : IWidgetRenderer { … }
```

```csharp
services.ConfigureWidgetRenderers(c => c.RegisterRenderer<CScriptChartRenderer>());
```

`RegisterRenderer<T>() where T : IComponent, IWidgetRenderer` liest Schlüssel und Metadaten aus dem
Attribut; ein Overload mit expliziten Werten übersteuert es (für Fälle, in denen derselbe Typ unter zwei
Schlüsseln laufen soll). Fehlt das Attribut und werden auch keine Werte übergeben, ist das ein Fehler beim
Registrieren, nicht beim Rendern.

**Warum am Typ und nicht am Aufruf:** der Schlüssel steht damit genau einmal — sonst existiert er dreimal
(Registrierung, Konfiguration, Datenbankspalte) und muss dreimal gleich geschrieben werden. Der
Konfigurationsweg (AP5) nennt dadurch nur noch den **Typ**; der Schlüssel ergibt sich. Übrig bleibt die
eine unvermeidbare Stelle: der Wert in `Widgets.RendererKey`, den der Editor aber als Auswahl anbietet
(AP4) statt als Freitext.

Anzeigename, Editor-Sprache und die optionale Prüfmethode hängen am **Descriptor** (Attribut +
Registrierung), nicht am Komponenten-Interface — sonst bräuchte der Editor eine Instanz der Komponente,
um sie zu erfragen. Die Prüfmethode wird beim Registrieren mitgegeben (`validate:`), weil ein Attribut
keinen Delegaten tragen kann.

Der eingebaute Scriban-Renderer registriert sich unter `""` **und** `"scriban"`; leer = heutiges Verhalten.

### 3.3 Die Diagramm-Familie: zwei Renderer, ein Kern

Beide Renderer erzeugen aus dem Konfigurationstext dieselbe Zwischenform — eine
`IDictionary<string, object>` — und geben sie an denselben Kern weiter. Der Kern kennt weder Scriban noch
CScript:

```
ScribanChartRenderer  ─ Scriban rendert Text ─→ JSON ─┐
                                                      ├─→ IDictionary<string,object>
CScriptChartRenderer  ─ CScript wertet aus ──→ ObjectLiteral ─┘
                                                      │
                                                      ↓
                              ChartWidgetDeclaration.FromMap(...)   (Prüfung + Meldungen)
                                                      ↓
                              ChartWidgetView  →  MudChart + Klick → WidgetAction
```

Dass die CScript-Seite **ohne JSON-Umweg** ankommt, ist kein Zufall: `ObjectLiteral` ist ein
`DynamicObject` **und** — über `IScope : IDictionary<string, object>` — ein Wörterbuch
(`Core/Literals/ObjectLiteral.cs:16`, `Core/RuntimeSafety/IScope.cs:10`). Es lässt sich also direkt lesen.

**Scriban-Variante** (`chart.scriban`): das Template rendert eine JSON-Deklaration. Zwei neue
Template-Funktionen in `WidgetTemplateFunctions` (der dafür vorgesehene Einhängepunkt) halten den
Normalfall kurz:

* `column(rows, "Name")` — zieht eine Spalte als Liste heraus (benutzt den Spaltenzugriff aus 1.1);
* `json(wert)` — schreibt einen Wert JSON-gerecht (Escaping, Listen, Zahlen invariant).

```
{
  "type": "pie",
  "labels": {{ json (column Rows "Status") }},
  "series": [ { "name": "Anzahl", "data": {{ json (column Rows "Anzahl") }} } ],
  "palette": ["#2979ff", "#00acc1"],
  "legend": "bottom"
}
```

**CScript-Variante** (`chart.cscript`): der Text ist ein Objektliteral über demselben Modell
(`Rows`, `Row`, `Count`, `Params`, `Title` liegen als Variablen im Scope), mit LINQ und nativen
C#-Ausdrücken:

```
{ type: 'pie',
  labels: Rows.Select(r => r.Status).ToArray(),
  series: [ { name: 'Anzahl', data: Rows.Select(r => r.Anzahl).ToArray() } ],
  palette: ['#2979ff', '#00acc1'],
  legend: 'bottom' }
```

Die Schlüsselnamen der Zwischenform sind in beiden Varianten dieselben und werden ohne Rücksicht auf
Gross-/Kleinschreibung gelesen — sonst wäre `Type` vs. `type` genau die Sorte Fehler, die eine leere
Kachel erzeugt.

### 3.4 Was in der Deklaration stehen darf: kleiner Kern, offener Rest

Die Deklaration schreibt **nicht** vor, welche Parameter `MudChart` kennt. Sie besteht aus zwei Teilen:

**Aufbereitete Felder** — die, die aus den Query-Daten gebaut werden müssen und deshalb nicht einfach
durchgereicht werden können:

| Feld | Pflicht | Wirkung |
|---|---|---|
| `type` | ja | `pie`, `donut`, `bar`, `stackedBar`, `line`, `timeseries`, `heatMap`, `rose`, `radar`, `sankey`, `scatterPlot` → `ChartType` |
| `labels` | bei allen ausser `line`/`timeseries` | die Kategorien; Eintrag ist **Text** oder `{ "text": …, "navigateTo": … }` (siehe unten) → `ChartLabels` |
| `series` | ja | Liste aus `{ "name": …, "data": [ … ] }`; bei `pie`/`donut` reicht eine → `ChartSeries` |

`series` muss der Kern selbst bauen, weil `MudChart<T>` **generisch** ist
(`where T : struct, INumber<T>, IMinMaxValue<T>, IFormattable`): der konstruierte Typ und die
`ChartSeries<T>`-Instanzen entstehen zur Laufzeit (`double`, sofern die Deklaration nichts anderes sagt).
`labels` ist aufbereitet, weil dort die Navigationsziele mit drinstecken.

**Alles Weitere wird durchgereicht.** Jedes andere Feld der Deklaration wird gegen die
`[Parameter]`-Properties des konstruierten `MudChart<T>` geprüft (Name ohne Rücksicht auf
Gross-/Kleinschreibung) und mit `ITVComponents.TypeConversion.TypeConverter.TryConvert` auf den
Zieltyp gebracht — `width`, `height`, `legendPosition`, `canHideSeries`, `matchBoundsToSize`, `class`,
`style` und was MudBlazor in kommenden Fassungen dazulegt, ohne dass wir eine Liste nachpflegen.

Ist der Wert selbst eine Map und der Zielparameter ein Objekt (`chartOptions`), wird **rekursiv** nach
derselben Regel befüllt:

```
"chartOptions": { "showLegend": true, "showToolTips": false,
                  "chartPalette": ["#2979ff", "#00acc1"] }
```

**Die Prüfung ist dabei kein Beiwerk, sondern der Grund, warum das überhaupt tragfähig ist:**
`MudComponentBase.UserAttributes` ist mit `[Parameter(CaptureUnmatchedValues = true)]` deklariert. Ein
verschriebener Name wirft also **nicht**, sondern landet still als HTML-Attribut am Wurzelelement — die
Einstellung wäre wirkungslos, ohne dass jemand erführe warum. Deshalb gilt:

* unbekannter Name → Befund (Editor-Prüfung beim Speichern, Fehler-Kachel zur Laufzeit), nie stillschweigend;
* nicht konvertierbarer Wert → Befund mit Feldname, Wert und Zieltyp;
* **gesperrt** sind `SelectedIndex`/`SelectedIndexChanged` (die verdrahtet der Renderer für die
  Navigation) und `UserAttributes` selbst.

Nebeneffekt, den man mitnehmen sollte: dieselbe Reflection kann dem Editor die **Liste der verfügbaren
Parameter** anzeigen. Wer über den Kern hinausgeht, muss sich zwar mit der MudBlazor-API befassen — aber
er muss sie nicht erraten.

`labels` und `series[].data` müssen gleich lang sein; ist das nicht so, ist es ein Befund, kein stilles
Abschneiden. Ein Wert, der sich nicht nach `double` wandeln lässt, ebenso — eine Kategorie lautlos
wegzulassen wäre die schlechteste aller Auskünfte.

**Klick = Navigation.** Der Regelfall ist der Sprung auf eine Seite, nicht eine anwendungsseitig
ausgewertete Aktion. Ein Beschriftungs-Eintrag darf deshalb statt Text ein Objekt sein:

```
"labels": [ { "text": "Offen",    "navigateTo": "Orders?status=open" },
            { "text": "Erledigt", "navigateTo": "Orders?status=done" },
            "Storniert" ]
```

Klick auf Segment oder Legendeneintrag (`MudChart.SelectedIndexChanged`) → ist ein Ziel hinterlegt, wird
navigiert; sonst wird wie beim HTML-Template ein `WidgetAction("select", <Text>)` ausgelöst, das die
Anwendung über das bereits verdrahtete `OnWidgetAction` bekommt. Beides kostet nichts extra und lässt
`canHideSeries` unberührt (das ist ein anderer Klick).

`navigateTo` wird wie ein `href` im HTML-Template behandelt — mit derselben Falle: in einer
mandanten-präfixierten Anwendung geht ein **root-absolutes** Ziel (`/Orders`) am Präfix vorbei und endet
im 404 (siehe `BUG-PRE141-TenantUrlGuard-Absolute-Links.md`). Der Renderer löst relative Ziele deshalb
gegen die aktuelle Basis auf; absolute Ziele bleiben, wie sie sind, und sind Sache des Autors.

## 4. Arbeitspakete

| AP | Inhalt | Dateien |
|---|---|---|
| **0** | `IWidgetRenderer`, `WidgetRendererDescriptor`, `WidgetRendererConfiguration` (Register/Get, case-insensitiv, Doppelvergabe abweisen), `ConfigureWidgetRenderers` | neu in `Blazor.MudBlazor/SharedComponents/Widgets/` + `Extensions/`; Registrierung in `Blazor.MudBlazor/WebPartInit.RegisterServices` — **nicht** an `UseViews` binden, `DashboardHost` gehört zur Basis |
| **1** | `ScribanWidgetRenderer` (erfüllt den Vertrag, rendert intern `WidgetRenderer`), registriert unter `""`/`"scriban"` | neu, ~20 Zeilen |
| **2** | Spalten `Widgets.RendererKey nvarchar(64) NULL` **und** `Widgets.RendererOptions nvarchar(max) NULL` (Abschnitt 5); `DashboardWidgetDefinition.RendererKey`/`.RendererOptions`; im Store aus `tmp`, **nicht** aus `lng` (der Renderer ist keine Sprachfrage — die Konfiguration im `Template` bleibt dagegen je Sprache pflegbar wie heute) | `EntityFramework/Models/DashboardWidgetDefinition.cs`, `TenantSecurity/Shared/Models/Base/DashboardWidget.cs`, `DbDiagnosticsQueryStore.GetDashboardItem`; SQL manuell in den Leitfaden (Snapshot-Drift, Konvention §20/§24/§25) |
| **3** | Versand: `TileTemplate` ersetzt `<WidgetRenderer>` durch `<DynamicComponent>`; unbekannter Schlüssel → **Fehler-Kachel** („verlangt: `x` — verfügbar: …") + Log-Eintrag. Dafür muss die Registry ihre Descriptors auch **aufzählbar** anbieten, nicht nur nachschlagbar | `DashboardHost.razor` |
| **4** | Editor: Renderer-Auswahl (Anzeigename übersetzt) statt Freitext, Monaco-Sprache aus dem Descriptor, Beschriftung „Konfiguration", **Prüfung beim Speichern** (`validate`), Umschalter *Ausdruck/Block* bzw. die Options-Maske des Renderers (je nach 5.a) | `DashboardWidgetDialog.razor`, `DashboardWidgetViewModel`, `DashboardWidgetAdminHandler` (List/Create/Update) |
| **5** | Konfigurationsweg: `WebPartConfig.WidgetRenderers`, Auflösung über `ExpressionParser`, **Start-Validierung** (nicht auflösbar / Vertrag nicht erfüllt / doppelter Schlüssel → Meldung mit Schlüssel und Typname, nicht erst beim Rendern) | `MudBlazor/Config/WebPartConfig.cs`, `MudBlazor/WebPartInit.cs` |
| **6a** | Diagramm-Kern: `ChartWidgetDeclaration.FromMap(IDictionary<string,object>)` (Prüfung + Meldungen) + `ChartWidgetView` (Deklaration → `MudChart`, Klick → `WidgetAction`) | neu in `Blazor.MudBlazor/SharedComponents/Widgets/Charts/` |
| **6b** | `CScriptChartRenderer` (`chart.cscript`) — `ObjectLiteral` direkt in den Kern | neu |
| **6c** | `ScribanChartRenderer` (`chart.scriban`) + Spaltenzugriff (`WidgetRowAccessor`: Dictionary → Reflection → Skalar) + Template-Funktionen `column`/`json` | neu; `WidgetTemplateFunctions.ImportFunctions` |
| **7** | Leitfaden §25.9 + Übersichtszeile; Tests in `AdminViews.Test` (Registry, Accessor, Deklarations-Parser) nach dem Muster `CustomCompanyInfoViewRegistrationTest` | `docs/Migration-Future_10-MLM.md` |

**Reihenfolge:** AP0–5 am Stück — danach ist Scriban eine Implementierung unter mehreren, der Editor bietet
die Auswahl samt Prüfung an, und ein Konsument kann einen eigenen Renderer beisteuern (im Code oder über
die Konfiguration). Dann AP6a→6b→6c: erst der Kern, dann die CScript-Variante (die geläufigere, also die,
an der sich der Kern zuerst bewährt), dann die Scriban-Variante mit `column`/`json`. AP7 begleitet.

Die Options-Spalte (Abschnitt 5) sitzt in **AP2** — zusammen mit `RendererKey`, damit es bei **einem**
manuellen SQL-Schritt bleibt —, ihre Maske in **AP4**.

## 5. Renderer-Optionen (entschieden)

„Ausdruck oder Block" gehört nicht in den Konfigurationstext und auch nicht in eine eigene Spalte je
Einstellung. Stattdessen: **eine generische Options-Spalte** `Widgets.RendererOptions nvarchar(max) NULL`
(JSON, invariant). Der Descriptor deklariert, welche Optionen sein Renderer hat — als
`DeclaredField`-Liste, also dieselbe Form, die die Widget-Parameter schon benutzen; der Editor zeigt dafür
die generische Maske.

Der CScript-Chart-Renderer deklariert damit *ScriptMode* (Ausdruck/Block, Vorgabe Ausdruck) — bedient
über zwei Knöpfe wie im Workflow-Editor. Ein künftiger Ampel-Renderer deklariert seine Schwellenwerte,
ohne dass jemand nochmal ans Schema muss.

Damit bleibt es bei **zwei** Spalten insgesamt (`RendererKey`, `RendererOptions`) statt einer pro
Kachelart — dieselbe Rechnung, die schon gegen die vier Chart-Spalten des ersten Entwurfs gesprochen hat.

## 6. Risiken und Merksätze

* **`DynamicComponent` baut je Render ein neues Parameter-Dictionary** — die Zielkomponente bekommt bei
  jedem Zeichnen `OnParametersSet`. Der Scriban-Renderer fängt das heute mit einem `ReferenceEquals`-Cache
  ab (`WidgetRenderer.razor:67-74`); **jeder** neue Renderer braucht dasselbe, sonst parst eine Kachel bei
  jedem Render neu.
* **Laufzeitverhalten von `DynamicComponent` im Bearbeiten-Modus** ist nicht geprüft. Die Kacheln hängen
  dort in einem `MudDynamicDropItem`-Wrapper; nach dem Fund vom 2026-08-11 (`OnlyZone`) ist das ein
  gewöhnliches `div` mit gewöhnlichen Kindkomponenten — Risiko gering, aber offen.
* **Razor-Namensraum-Falle:** neue Komponenten in `SharedComponents/Widgets/` brauchen wie `WidgetRenderer`
  ein explizites `@namespace`, sonst finden Nachbarkomponenten sie nicht (nur `RZ10012`, Parameter werden
  still zu HTML-Attributen).
* **Die Konfiguration ist lokalisierbar** (`Template` kommt aus der Localizations-Zeile) — für einen
  Chart-Renderer heisst das, dass Beschriftungen auch über eine Sprachzeile statt über `Translate` im
  Template kommen können. Beides ist zulässig; der `RendererKey` selbst darf nicht je Sprache abweichen.
* **CScript: `Parse` und `ParseBlock` sind nicht gegeneinander tolerant.** Ein Block ohne `return` liefert
  `null`; `Parse` auf einen Block angewendet wirft **nicht**, sondern liefert still etwas Falsches. Genau
  deshalb wird der Modus gewählt und nicht erraten (Entscheidung 6). Liefert die Auswertung `null`, sagt
  die Fehler-Kachel ausdrücklich „kein Ergebnis — fehlt ein `return`?".
* **Der Umschalter kommt nicht aus `CScriptField`.** Die Komponente liegt in `WorkflowViews` und ihr
  `ScriptMode` im Paket `ITVComponents.Workflow`; die AdminViews dürfen darauf nicht verweisen, und das
  Enum kann nicht umziehen, ohne die Serialisierung der Workflow-Definitionen zu berühren. Der Widget-Editor
  bekommt deshalb dieselbe Bedienung (zwei Knöpfe neben dem `CodeEditor`) mit eigenem Enum. Sollte sich das
  ein drittes Mal wiederholen, gehört die Komponente in die Basis gezogen — dann aber mit einem neutralen
  Enum und einer Umsetzung im Workflow-Zweig.
* **`ExpressionParser` hält geparste Ausdrücke bereits vor** (`parsedExpressions`, `Core/ExpressionParser.cs:22`)
  — der CScript-Weg parst also nicht bei jedem Rendervorgang neu, nur die Auswertung läuft. Das entkräftet
  das Kostenargument gegen ein ausführbares Format weitgehend; der Renderer-eigene Cache (Merksatz oben)
  bleibt trotzdem nötig, weil auch die Auswertung nicht bei jedem Zeichnen laufen soll.
* **Ein unbekannter Parametername an einer Mud-Komponente ist STILL**, nicht laut:
  `MudComponentBase.UserAttributes` hat `[Parameter(CaptureUnmatchedValues = true)]`, der Wert landet also
  als HTML-Attribut statt als Fehler. Jede Durchreiche-Mechanik muss deshalb selbst gegen die
  `[Parameter]`-Properties prüfen — sonst ist ein Tippfehler unsichtbar.
* **`MudChart<T>` ist generisch.** Über `DynamicComponent` gibt es keine Typinferenz; der konstruierte Typ
  muss zur Laufzeit gebildet werden. Beim Umsetzen prüfen, was `timeseries` braucht — dort arbeitet
  MudBlazor mit einem eigenen Serientyp.
* **Enum-Bezüge in CScript** (`ChartType.Pie` statt `'pie'`) setzen voraus, dass der Typ im Scope liegt —
  beim Umsetzen prüfen, ob `variables["ChartType"] = typeof(ChartType)` den statischen Zugriff trägt.
  Wenn nicht: Zeichenketten, die der Kern auf das Enum abbildet (was er ohnehin können muss, weil die
  Scriban-Seite nur Zeichenketten liefern kann).
