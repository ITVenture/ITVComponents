# Issue: Austauschbare Widget-Renderer (`IWidgetRenderer`) statt fest verdrahtetem Scriban (Anstoß aus MLM)

**Status:** TOOLKIT-SEITIG ERLEDIGT mit `5.0.0-PRE172` (Leitfaden §25.9) — noch am selben Tag umgesetzt,
und zwar über die Anforderung hinaus: zwei Diagramm-Renderer statt einem (`chart.scriban` /
`chart.cscript`), Renderer-Einstellungen als eigenes Feld (`RendererOptions`), Attribut-basierte
Schlüssel/Beschriftung, Prüfmethode beim Speichern, Config-Export zieht beide Felder mit. Offen ist nur
noch, was in MLM passiert: die Startseiten-Kachel könnte vom handgeschriebenen SVG auf einen
Diagramm-Renderer umziehen (kein Zwang — der Scriban-Weg bleibt).
**Datum:** 2026-08-11 (erledigt am selben Tag)
**Quelle:** MLMManager-Session (Konsument). MLM hat die Dashboard-Fläche (§25) auf der Startseite
eingebunden (`MLMManager.Web/Components/Pages/Home.razor`) und braucht als Nächstes Diagramm-Kacheln.
**Toolkit-Stand:** `5.0.0-PRE171`. Alle Aussagen sind gegen diesen Stand verifiziert; wo etwas abgeleitet
und **nicht** geprüft ist, steht es ausdrücklich dabei.
**Ersetzt:** einen ersten, engeren Entwurf („vier Chart-Spalten an `Widgets`"), der vom Konsumenten
zugunsten der hier beschriebenen Lösung verworfen wurde. Begründung unten unter „Warum nicht Chart-Spalten".

## Ziel

Heute ist die Darstellung einer Kachel **fest auf Scriban verdrahtet**: `DashboardHost` rendert je Kachel
einen `WidgetRenderer` (`DashboardHost.razor:702`, innerhalb `TileTemplate` ab Zeile 647), und der
interpretiert `Widgets.Template` als Scriban-Text.

Gewünscht ist stattdessen ein **austauschbarer Renderer**: Beim Anlegen eines Widgets wird ausgewählt,
*womit* es gerendert wird. Der Renderer bekommt — wie heute — die Query-Daten **und** den
Konfigurationstext des Widgets; was in diesem Text steht, entscheidet **der Renderer**. Im Scriban-Fall ist
es weiterhin ein Scriban-Template. Ein Diagramm-Renderer könnte dort stattdessen eine Deklaration erwarten,
im Stil

```
{ Type: ChartType.Pie, Labels: 'Status', Values: ['Anzahl'],
  TitleLocation: Position.Bottom, Colors: ['#2979ff', '#00acc1'] }
```

Registriert wird **über die WebPart-Konfiguration** (Abschnitt `WidgetRenderers`) und **zusätzlich
programmatisch** — so dass ein Konsument einen eigenen Renderer beisteuern kann, ohne dass das Toolkit
dafür ein Release braucht. Details in AP2.

## Warum das die bessere Schnittstelle ist

1. **Das Schema wächst nicht je Darstellungsart.** Der verworfene Entwurf hätte für Diagramme vier Spalten
   an `Widgets` gebraucht (`ChartType`, `ChartLabelColumn`, `ChartValueColumns`, `ChartPalette`) — und die
   nächste Kachelart (Kennzahl mit Sparkline, Ampel, Karte) wieder eigene. Mit einem Renderer-Schlüssel
   bleibt es bei **einer** Spalte, egal wie viele Arten dazukommen; die Konfiguration liegt im schon
   vorhandenen `Template`-Feld.
2. **Es ist ein Erweiterungspunkt, kein Feature.** Diagramme sind dann eine *mitgelieferte Implementierung*,
   nicht eine Eigenschaft des Dashboards. Ein Konsument mit einer Spezialdarstellung ist nicht mehr darauf
   angewiesen, dass das Toolkit sie vorsieht.
3. **Es passt zu dem, was das Toolkit ohnehin tut.** `ICustomCompanyInformationHandler` (§23) ist
   dieselbe Form: ein Vertrag, Implementierungen werden registriert, die Auswahl fällt in der Konfiguration.
4. **Es ist kein Umbau, sondern ein Einzug.** Der heutige `WidgetRenderer` wird die erste registrierte
   Implementierung. Leerer Schlüssel = Scriban = heutiges Verhalten.

## Mechanik — geprüft, nicht geraten

Der Entwurf ist in einem Wegwerf-Projekt gegen .NET 10 / MudBlazor 9.6.0 compile-verifiziert (0 Fehler):

- Eine **Razor-Komponente kann den Vertrag implementieren**: `@implements IWidgetRenderer` plus
  `[Parameter]`-Properties, die die Interface-Member erfüllen.
- Die **Doppel-Constraint funktioniert**: `AddRenderer<T>() where T : IComponent, IWidgetRenderer` — der
  Registrierungsaufruf lässt damit nichts durch, das entweder keine Komponente ist oder den Vertrag nicht
  erfüllt.
- Der **Versand über `DynamicComponent`** (`Type` + `Parameters`-Dictionary) kompiliert; die Schlüssel
  lassen sich als `nameof(IWidgetRenderer.TemplateSource)` schreiben, so dass ein Umbenennen im Vertrag am
  Versandort auffällt statt zur Laufzeit.

Skizze des Vertrags:

```csharp
public interface IWidgetRenderer
{
    string TemplateSource { get; set; }              // der Konfigurationstext des Widgets
    WidgetTemplateModel Data { get; set; }           // Rows / Row / Count / Params / Title
    EventCallback<WidgetAction> OnAction { get; set; }
    EventCallback<Exception> OnRenderError { get; set; }
}
```

Das ist exakt der Parametersatz, den `WidgetRenderer` heute schon hat
(`SharedComponents/Widgets/WidgetRenderer.razor:37-59`) — der Vertrag beschreibt also den Ist-Zustand,
er erfindet ihn nicht.

## Arbeitspakete

### AP1 — Eine Spalte

`Widgets.RendererKey nvarchar(64) NULL` (+ `DashboardWidgetDefinition.RendererKey`,
`EntityFramework/Models/DashboardWidgetDefinition.cs`). Leer/NULL = der eingebaute Scriban-Renderer.

Additiv, keine Datenmigration; nach Konvention aus §20/§24/§25 **manuell per SQL**, nicht über
`dotnet ef migrations add` (Snapshot-Drift im Toolkit-Repo).

### AP2 — Registrierung und Versand

**Entschieden (Konsument, 2026-08-11): Der WebPart-Weg ist der vorgesehene Weg — und die
programmatische Erweiterung bleibt zusätzlich offen.** Beide sollen dieselbe Registrierung füllen.

**a) Über die WebPart-Konfiguration (Hauptweg).** Der Basis-WebPart
`ITVComponents.WebCoreToolkit.Blazor.MudBlazor` ist der richtige Ort — dort leben `DashboardHost` und
`WidgetRenderer`, und er hat bereits die Einzel-Key-Form `LoadOptions(IConfiguration, string path)` mit
Bindung auf `MudBlazorLib/Config/WebPartConfig.cs` (heute nur `UseViews`). Dorthin kommt ein Abschnitt:

```jsonc
"MudBasicViews": {
  "UseViews": true,
  "WidgetRenderers": [
    { "Key": "chart", "Type": "…" }
  ]
}
```

Zur Typauflösung gibt es bereits eine Hausform, die nicht neu erfunden werden muss: `ContextType` in
`SecurityContextOptions` ist genauso ein Typname aus der Konfiguration und wird über
`ExpressionParser.Parse(name, dic)` nach `Type` aufgelöst
(`Blazor.MudBlazor.AdminViews/WebPartInit.cs:80`). Derselbe Mechanismus hier bedeutet, dass ein Autor die
Schreibweise benutzt, die er aus `appsettings-parts.json` ohnehin kennt.

*(In MLM ist der Abschnitt heute `ITVenture:WebPartConfigurations:MudBasicViews` mit genau einem Eintrag
`"UseViews": true` — die Liste wäre dort eine Ergänzung von wenigen Zeilen.)*

**b) Programmatisch (bleibt offen).** `AddRenderer<T>(key, displayName)` mit
`where T : IComponent, IWidgetRenderer` für Konsumenten, die den Renderer im Code registrieren wollen
(z.B. weil er Konstruktor-Abhängigkeiten oder Feature-Flags hat, die in der Konfiguration schlecht
abbildbar sind). Die toolkit-eigenen Renderer registrieren sich auf diesem Weg selbst.

**Der Unterschied zwischen beiden ist eine Falle und gehört behandelt:** Weg (b) prüft über die
Doppel-Constraint schon beim Kompilieren, dass der Typ Komponente **und** Vertrag ist. Weg (a) kann das
nicht — dort steht ein String. Ein konfigurierter Typ, der nicht auflösbar ist oder den Vertrag nicht
erfüllt, muss deshalb **beim Start** mit Schlüssel und Typname im Log abgelehnt werden, nicht erst beim
Rendern der Kachel. Dasselbe gilt für einen doppelt vergebenen `Key`: nicht stillschweigend „der letzte
gewinnt", sondern eine Meldung — sonst sucht man den Grund später an der falschen Stelle.

**Versand.** `DashboardHost.TileTemplate` ersetzt das feste `<WidgetRenderer …>` durch
`<DynamicComponent>` mit dem zum `RendererKey` registrierten Typ. Unbekannter Schlüssel → Fallback auf
Scriban **plus Log-Eintrag** (eine Kachel, die nach einem Tippfehler wortlos etwas anderes zeigt, ist
genau die Sorte Fehler, die später teuer wird).

### AP3 — Der mitgelieferte Diagramm-Renderer

`MudBlazor 9.6.0` bringt alles Nötige mit — reines Blazor/SVG, kein JS, keine neue Abhängigkeit. Per
Reflection und Compile-Check gegen 9.6.0 verifiziert:

- `ChartType`: `Donut, Line, Pie, Bar, StackedBar, Timeseries, HeatMap, Rose, Radar, Sankey, ScatterPlot`
- `MudChart`: `ChartType`, `ChartLabels` (`string[]`), `ChartSeries` (`List<ChartSeries<T>>`),
  `ChartOptions`, `LegendPosition`, `Width`, `Height`, `SelectedIndex`, `SelectedIndexChanged`,
  `TooltipTemplate`, `CanHideSeries`
- `ChartSeries<T>`: `Name`, `Data` (`ChartData<T>`, implizite Konvertierung aus `double[]`), `Visible`
- `ChartOptions`: `ShowLegend`, `ShowToolTips`, `ChartPalette` (`string[]`)
- `T` muss nicht angegeben werden, es wird aus der Serie abgeleitet.

Drei Stellen, die erfahrungsgemäß wehtun:

1. **Der Zeilentyp ist nicht einheitlich.** `WidgetTemplateModel.Rows` ist `IReadOnlyList<object?>`, und was
   darin liegt, hängt an der Datenquelle: über einen DbContext läuft die Query als **LINQ-/C#-Skript**
   (`NativeScriptHelper.RunLinqQuery`, `EntityFramework/Extensions/ContextExtensions.cs:257-275`) und
   liefert, was das Skript zurückgibt — typischerweise anonyme Objekte mit echten Properties; über
   `WrappedDynamicDataAdapter` (`DataSources/Impl/WrappedDynamicDataAdapter.cs:40-50`) dagegen dynamische
   Zeilen. Ein Spaltenzugriff muss **beide** Formen bedienen. Scriban löst das heute für sich — jeder neue
   Renderer erbt dieses Problem, es gehört daher in einen **gemeinsamen Helfer**, nicht in jeden Renderer.
2. **Beschriftungen durch `Translate`.** Eine Label-Spalte kann Kultur-JSON enthalten — dieselbe Form, die
   seit PRE171 auch `DisplayName`/`TitleTemplate` dürfen (§25.4.2, `DashboardWidgetRunner.RenderCaption`).
   Sonst steht rohes JSON in der Legende.
3. **Nicht-numerische Werte nicht schlucken.** Ein Wert, der sich nicht nach `double` wandeln lässt, gehört
   mit Widget- und Spaltenname ins Log; die Kachel geht in den bekannten Fehlerzustand, statt lautlos eine
   Kategorie wegzulassen.

Klicks: `SelectedIndexChanged` sollte dasselbe `WidgetAction` auslösen, das der Scriban-Renderer aus
`data-widget-action` baut (Vorschlag: Aktion `select`, Argument = Label der angeklickten Kategorie). Dann
funktioniert das `OnWidgetAction` einer Anwendung unverändert für beide Kachelarten — MLM hat den Handler
auf der Startseite schon verdrahtet.

### AP4 — Konfigurationsformat: bewusst Sache des Renderers

Der Konsument schlägt für den Diagramm-Renderer ein **CScript-Objektliteral** vor. Dafür spricht, dass
CScript im Toolkit schon etabliert ist (die Diagnostics-Queries laufen darüber) und dass Enum-Bezüge wie
`ChartType.Pie` und berechnete Werte damit natürlich schreibbar sind. Dagegen spricht, dass eine reine
Deklaration dafür ausgeführt werden muss — mit Kosten je Rendervorgang.

Empfehlung: **Das Toolkit soll kein Format vorschreiben.** Der Vertrag reicht einen `string` durch, jeder
Renderer liest ihn nach seinen Regeln. Für den mitgelieferten Diagramm-Renderer ist die Formatwahl dann
eine lokale Entscheidung. Falls sie auf CScript fällt: das Ergebnis pro Konfigurationstext **cachen** —
`WidgetRenderer` macht das heute schon sinngemäß, er kompiliert nur bei geänderter Quelle neu
(`WidgetRenderer.razor:67-74`).

Zur Sicherheitslage: Konfigurationstexte stammen wie die heutigen Scriban-Templates und die
Diagnostics-Queries von Sysadmin/Mandanten-Admin. Das Vertrauensniveau ändert sich also nicht — aber es
bleibt auch nicht *hinter* dem heutigen zurück, was bei einem ausführbaren Format ausdrücklich geprüft
gehören würde.

### AP5 — Editor

`/Util/DashboardWidgets` braucht eine Auswahl der registrierten Renderer (Schlüssel + Anzeigename) statt
eines Freitextfelds. Zwei sinnvolle Ergänzungen, beide optional am Vertrag:

- eine **Prüfmethode**, damit eine kaputte Konfiguration beim Speichern auffällt und nicht erst als leere
  Kachel;
- ein **Sprach-Hinweis für den Editor** (Scriban/JSON/CScript), da BlazorMonaco im AdminViews-Zweig ohnehin
  eingebunden ist.

Beschriftung des Konfigurationsfelds entsprechend neutral („Konfiguration") statt „Template".

## Warum nicht Chart-Spalten (der verworfene Entwurf)

Der erste Vorschlag legte `ChartType`/`ChartLabelColumn`/`ChartValueColumns`/`ChartPalette` an `Widgets` und
liess `DashboardHost` anhand von `ChartType` zwischen `WidgetRenderer` und einem `ChartRenderer` wählen. Das
hätte für Diagramme funktioniert, aber: es macht *Diagramm* zu einem Sonderfall im Datenmodell, es wächst
mit jeder weiteren Kachelart, und ein Konsument mit einer eigenen Darstellung bleibt aussen vor. Die
Renderer-Variante liefert dasselbe Ergebnis mit einer statt vier Spalten und öffnet den Punkt für alles
Weitere.

## Was ausdrücklich NICHT gewünscht ist

- **Keine JS-Chart-Bibliothek** als neue Abhängigkeit. MudBlazor kann es und ist schon da; eine zweite
  Diagramm-Welt bedeutet zwei Paletten, zwei Tooltip-Stile und zwei Stellen für Hell/Dunkel.
- **Keine Custom Elements / `Blazor.rootComponents.add`.** Das war der geprüfte Umweg, um aus einem
  Scriban-Template heraus doch eine Komponente zu bekommen — er ist versperrt: der `WidgetRenderer` schiebt
  das Template über `@((MarkupString)renderedHtml)` ins DOM (`WidgetRenderer.razor:27`), und so eingefügtes
  Markup führt `<script>` nie aus. Custom Elements würden zwar auch aus `innerHTML` aufgewertet, aber die
  Microsoft-Doku (*Use Razor components in JavaScript apps and SPA frameworks*, ASP.NET Core 10) rät für
  `blazor.web.js` ausdrücklich davon ab, die Registrierungswege hängen an
  `AddServerSideBlazor`/WASM-`RootComponents` (die eine Blazor Web App mit `AddRazorComponents()` nicht
  bedient), und `dotnet/aspnetcore#53920` ist seit Februar 2024 offen, Milestone *Backlog*.
  **Der Renderer-Ansatz macht diesen ganzen Umweg gegenstandslos** — ein Renderer ist eine ganz normale
  Komponente im bestehenden Provider-Kontext.
- **Kein Ersatz des Scriban-Wegs.** Freies Template und deklarativer Renderer sollen nebeneinander stehen.

## Priorität

**Nicht blockierend.** MLM kann Diagramme heute über Inline-SVG im Scriban-Template liefern — durchgespielt
und funktionsfähig (Tortendiagramm über `stroke-dasharray` auf Kreisen mit Radius `100/(2π)`, so dass der
Umfang exakt 100 ist und eine Prozentzahl direkt die Strichlänge wird). Für eigene Seiten ausserhalb der
Dashboard-Fläche steht `MudChart` ohnehin direkt zur Verfügung. Der Nutzen liegt darin, Geometrie nicht je
Widget von Hand zu schreiben — und vor allem darin, die Darstellungsart überhaupt zu einem Erweiterungspunkt
zu machen.

## Verifikations-Notizen

Was für diesen Issue tatsächlich ausgeführt wurde:

- **Renderer-Muster compile-geprüft** (.NET 10, MudBlazor 9.6.0): Komponente implementiert
  `IWidgetRenderer` mit `[Parameter]`-Properties, `AddRenderer<T> where T : IComponent, IWidgetRenderer`,
  Versand über `DynamicComponent` mit `nameof`-Schlüsseln — 0 Fehler.
- **`MudChart`-API** gegen MudBlazor 9.6.0 per Reflection ausgelesen, Razor-Syntax für Pie und Bar (mit und
  ohne explizites `T`) compile-geprüft.
- **Scriban** 7.2.5 mit nachgebautem `WidgetTemplateFunctions.CreateGlobals`/`CreateContext` unter `de-CH`,
  `de-DE` und invariant gerendert: Zahlen kommen in **allen drei** Kulturen mit Dezimalpunkt — SVG aus einem
  Template ist also sprachunabhängig gültig.
- **Toolkit-Quellen** gegen `5.0.0-PRE171` gelesen: `WidgetRenderer.razor`, `DashboardHost.razor`,
  `DashboardWidgetRunner.cs`, `WidgetTemplateFunctions.cs`, `DashboardWidgetDefinition.cs`,
  `DiagnosticsQueryService.cs`, `ContextExtensions.cs`, `WrappedDynamicDataAdapter.cs`.
- **Nicht geprüft:** das Laufzeitverhalten von `DynamicComponent` im Bearbeiten-Modus der Fläche (die
  Kacheln hängen dort in einem `MudDynamicDropItem`-Wrapper) und das Verhalten von MudBlazor-Providern
  innerhalb eines Custom Elements (Letzteres nur als Ableitung, siehe oben).
