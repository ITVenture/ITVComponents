# BUG (PRE186): Der Blazor-`IPermissionScope` sieht den Mandanten aus der URL bei **Nicht-Blazor-Endpunkten** nie — `/{tenant}/Diagnostics`, `/ForeignKey` und `/DBW` laufen still unter dem Default-Mandanten

> **BEHOBEN** in `BlazorContextUserProvider` (Toolkit). Analyse und Fix-Vorschlag des Reports haben
> Zeile für Zeile getragen; umgesetzt ist genau die vorgeschlagene Bewegung. Einzelheiten unten unter
> „Behebung".

> **Gemeldet aus der MLM-Konsumenten-Session, 2026-08-21**, direkt nach dem Bump auf PRE186.
> Ausgangsbeobachtung war „die Query-Filter des `WorkflowContext` greifen bei
> `/{tenant}/diagnostics/PendingTasks` nicht". Diese Vermutung ist **widerlegt**: die Filter greifen
> einwandfrei. Falsch ist der Mandant, unter dem sie greifen — und der kommt nicht aus dem Workflow-
> Subsystem, sondern aus der Scope-Auflösung.

## Übersicht

| | |
|---|---|
| **Betroffen** | `ITVComponents.WebCoreToolkit.Blazor/Security/BlazorContextUserProvider.cs` (Getter `RouteData`, Zeile 68-101) |
| **Kern** | `RouteData` leitet den Mandanten ausschliesslich aus `NavigationManager.BaseUri` ab, also aus dem `<base href>`, das erst beim **Blazor-Rendern** entsteht. Endpunkte, die kein Blazor rendern, haben kein `<base href>` und keinen initialisierten NavigationManager — der Getter liefert dort leere RouteData. |
| **Folge** | `ResolvingPermissionScope` findet keinen Route-Override, fällt auf `DefaultScopeExpression` zurück und liefert den **Default-Mandanten des Benutzers** statt des Mandanten aus der URL. |
| **Reichweite** | Alle tenant-skalierten Endpunkte aus `ITVComponents.WebCoreToolkit.Net/Extensions/RouteExtensions.cs`: `UseDiagnostics`, `UseAutoForeignKeys`, `UseWidgets`. Registriert werden sie von `WebPartInit.Register` (Zeile 158-174). |
| **Kein Rechte-Loch** | Der Scope prüft weiterhin gegen die `EligibleScopes` des Benutzers, und der Rückfall ist dessen **eigener** Default-Mandant. Es entstehen keine fremden Daten — aber durchgehend die **falschen**, ohne jede Meldung. |
| **Aufwand** | Klein und symmetrisch: `IHttpContextAccessor` ist in der Klasse **bereits injiziert** und wird im `User`-Getter für genau diese Lücke schon benutzt. |

## Symptom

Aufruf von `/440304d1004f415e95ca89053d5363cd/diagnostics/PendingTasks` als Benutzer, dessen
`DefaultTenant`-Claim `ADM` ist. Die Abfrage liest über den `WorkflowContext`.

Erwartet: die Daten des Mandanten `440304d1004f415e95ca89053d5363cd`.
Beobachtet: die Daten von `ADM`.

Gemessen mit einer Sonden-`DiagnosticsQuery` gegen **denselben** Kontext, den die Abfrage bekommt:

```json
{"ContextType":"ITVComponents.Workflow.EntityFramework.WorkflowContext",
 "ContextHash":10367251,
 "UseTenantFilter":true,
 "CurrentTenant":"ADM",
 "InstFiltered":17, "InstUnfiltered":21,
 "DefFiltered":8,  "DefUnfiltered":8,
 "TokensAll":925}
```

Das ist der entscheidende Beleg: `UseTenantFilter = true` beweist, dass der **Plugin-Ctor** benutzt
wurde (der options-only-Ctor liesse das Feld auf dem `bool`-Default), und `InstFiltered 17` gegen
`InstUnfiltered 21` beweist, dass der Query-Filter **arbeitet**. Er arbeitet nur mit
`CurrentTenant = "ADM"`.

`WorkflowContext.cs:816-818` bezieht diesen Wert direkt aus dem Scope:

```csharp
public string CurrentTenant => WorkflowExecutionScope.HasTenant
    ? WorkflowExecutionScope.CurrentTenant
    : (UseTenantFilter ? scopeProvider?.PermissionPrefix : null);
```

Ein `WorkflowExecutionScope` ist im Web nicht aktiv, `UseTenantFilter` ist `true` — es gilt also
`IPermissionScope.PermissionPrefix`, und der ist `ADM`. Damit ist das Workflow-Subsystem aus der
Ursachenkette raus; die Frage lautet nur noch, warum der Scope `ADM` sagt.

## Root Cause

### 1. Der Endpunkt ist Minimal-API, kein Blazor

`RouteExtensions.cs:65-79` mappt die Diagnose als reinen `MapGet` auf `DiagnosticsHandler.Process`:

```csharp
var tmp = builder.MapGet(
    $"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}{(forAreas ? "/{area:exists}" : "")}/Diagnostics/{{diagnosticsQueryName:required}}/{{fileHandler:alpha?}}",
    DiagnosticsHandler.Process);
```

Es rendert **keine** Razor-Komponente. Damit gibt es kein `<base href>` und keinen initialisierten
`NavigationManager`. Für `UseWidgets` (`RouteExtensions.cs:81-94`) und `UseAutoForeignKeys`
(`RouteExtensions.cs:102-125`) gilt dasselbe.

### 2. `BlazorContextUserProvider.RouteData` kennt nur die Base-URI

`BlazorContextUserProvider.cs:68-101` — der Mandant kommt aus `navigation.BaseUri`, nicht aus der
Anfrage:

```csharp
public IDictionary<string, object> RouteData
{
    get
    {
        // Outside a live circuit/request — e.g. plugin initialization at startup (UsePluginsInit),
        // …there is no route context to read in that window, so yield empty route data and let scope
        // resolution fall back to its default (no route-based tenant override without a request).
        string uri, baseUri;
        try
        {
            uri = navigation.Uri;
            baseUri = navigation.BaseUri;
        }
        catch (InvalidOperationException)
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        var result = ParseQuery(uri);
        var opts = scopeOptions.Value;
        if (opts.TenantSource == TenantSource.PathSegment
            && !string.IsNullOrEmpty(opts.RouteOverrideParam))
        {
            var segment = ExtractFirstBaseSegment(baseUri);
            if (!string.IsNullOrEmpty(segment))
            {
                result[opts.RouteOverrideParam!] = segment;
            }
        }
        return result;
    }
}
```

Beide Zweige enden bei einem Minimal-API-Aufruf im selben Ergebnis:

- **wirft** der uninitialisierte `NavigationManager` auf `.Uri`/`.BaseUri`, greift der `catch` in
  Zeile 83-86 und gibt leere RouteData zurück;
- **wirft er nicht**, ist die Base-URI die Anwendungswurzel, `ExtractFirstBaseSegment`
  (Zeile 135-150) liefert `null`, und der `tenant`-Schlüssel wird nie gesetzt.

Der Kommentar in Zeile 72-76 beschreibt diesen Rückfall als bewusst — gemeint war aber ersichtlich das
Startfenster (`UsePluginsInit`), **nicht** ein bedienter tenant-skalierter HTTP-Endpunkt.

### 3. Die Scope-Auflösung fällt deshalb auf den Default zurück

`ResolvingPermissionScope.cs:200-224`:

```csharp
var routeData = contextUser.RouteData;
if (!string.IsNullOrEmpty(RouteOverrideParam) &&
    routeData != null &&
    routeData.TryGetValue(RouteOverrideParam, out var ovRaw) &&
    ovRaw is string ov && !string.IsNullOrEmpty(ov))
{
    // The route may only override to a scope the user is actually eligible for. …
    if (scopeToken.EligibleScopes.Any(n => n.ScopeName.Equals(ov, StringComparison.OrdinalIgnoreCase)))
    {
        UpdateToken(ov, scopeToken, secc, isNew, false, true);
        IsScopeExplicit = true;
        return ov;
    }
}

IsScopeExplicit = false;
…
    retVal = DefaultScopeExpression(contextUser, scopeToken.EligibleScopes);
```

Ohne `tenant`-Schlüssel wird der ganze Override-Block übersprungen. `IsScopeExplicit` bleibt `false`,
und es gilt der Default — im Meldefall der `DefaultTenant`-Claim, also `ADM`.

### 4. Die Nicht-Blazor-Variante hätte den Wert

`DefaultContextUserProvider.cs:51-52` liest genau die richtige Quelle:

```csharp
public IDictionary<string, object> RouteData =>
    customRouteData ?? httpContext?.HttpContext?.GetRouteData()?.Values;
```

Für die Diagnose-Route steht dort `tenant = "440304d1004f415e95ca89053d5363cd"`. Nur wird dieser
Provider nicht benutzt: `AddBlazorContextUser()` ersetzt `IContextUserProvider` **app-weit** —
`ITVComponents.WebCoreToolkit.Blazor/Extensions/DependencyExtensions.cs:45`:

```csharp
services.AddScoped<IContextUserProvider>(sp => sp.GetRequiredService<BlazorContextUserProvider>());
```

gegen `ITVComponents.WebCoreToolkit/Extensions/DependencyExtensions.cs:117`:

```csharp
.AddScoped<IContextUserProvider>(sp => sp.GetRequiredService<DefaultContextUserProvider>());
```

Ein Host, der die Blazor-Strategie wählt, verliert damit die Route-Auflösung für **jede** Anfrage
ausserhalb eines Circuits — auch für die, die das Toolkit selbst mappt.

## Was ausgeschlossen wurde

Damit die Fix-Recherche nicht dieselben Wege noch einmal geht:

- **Host-Wiring ist vollständig.** `AddBlazorPermissionScope` ist mit
  `TenantSource = TenantSource.PathSegment` und `RouteOverrideParam = "tenant"` konfiguriert, der
  Net-WebPart mit `"TenantParam": "tenant"`. Die Namen decken sich, der Routenwert läge unter genau
  diesem Schlüssel. `app.UseTenantPathPrefix()` ist gesetzt und validiert das Segment auch — der
  Blazor-Scope-Provider liest diese Ablage nur nie.
- **Nicht der options-only-Ctor / die filterfreie DI-Fabrik.** `UseTenantFilter = true` in der Sonde
  beweist, dass der Plugin-Ctor mit `modelOptions` gelaufen ist.
- **Nicht der EF-Modell-Cache.** Der Plugin-Weg ersetzt über `ContextOptionsLoader.cs:37` den
  `IModelCacheKeyFactory`, die DI-Fabrik des Hosts nicht — es sind zwei getrennte interne
  Service-Provider. Und der Filter ist ohnehin nachweislich im Modell.
- **Nicht `WorkflowExecutionScope`.** Der ist `AsyncLocal` und wird im Web nicht gesetzt; `HasTenant`
  ist `false`.
- **Nicht die dritte Stufe von `ResolveSource`** (`ContextResolveOptionsExtensions.cs:92-106`,
  Rückfall auf das globale Plugin ohne Mandanten-Präfix). Der Verdacht lag nahe, weil die
  Dokumentation dort genau „die Daten des obersten Mandanten" ankündigt und das gemeldete `ADM`
  der oberste Mandant ist — aber das ist Koinzidenz: der Plugin-Ctor bekommt `$services` und daraus
  einen völlig regulären `IPermissionScope`. Der liefert `ADM`, weil die Route-Auflösung fehlschlägt,
  nicht weil das Plugin global registriert ist.

## Fix-Vorschlag

`BlazorContextUserProvider.RouteData` sollte auf `HttpContext.GetRouteData()` zurückfallen, wenn der
NavigationManager keine Route hergibt — also dieselbe Bewegung, die der `User`-Getter derselben Klasse
für dieselbe Lücke schon macht (`BlazorContextUserProvider.cs:53-60`):

> „Static SSR … renders without a circuit … The HttpContext is available exactly in that window (and
> null inside a live circuit), so fall back to the request's authenticated user".

`IHttpContextAccessor` ist bereits Konstruktor-Parameter (Zeile 31) und als Feld vorhanden (Zeile 28)
— es ist keine neue Abhängigkeit nötig. Skizze:

- im `catch (InvalidOperationException)` statt eines leeren Dictionary die HttpContext-Routenwerte
  liefern;
- und im Erfolgsfall, wenn `ExtractFirstBaseSegment` nichts hergibt, den `RouteOverrideParam` aus
  `httpContextAccessor.HttpContext?.GetRouteData()?.Values` nachziehen, bevor `result`
  zurückgegeben wird.

Die Reihenfolge sollte die Base-URI **vorne** lassen: im lebenden Circuit ist sie die richtige Quelle
(der HttpContext ist dort ohnehin null), der HttpContext ist der Rückfall. Damit bleibt das
Per-Tab-Verhalten unangetastet und die drei Endpunkt-Familien werden auf einen Schlag korrekt.

Zu prüfen wäre bei der Recherche noch, ob das Startfenster (`UsePluginsInit`), auf das sich der
Kommentar in Zeile 72-76 beruft, durch den Rückfall etwas Unerwünschtes bekommt — dort ist
`HttpContext` normalerweise ebenfalls null, der Rückfall liefe also weiterhin ins leere Dictionary.

## Reproduktion

Host-Konfiguration wie MLM: `AddBlazorContextUser()` + `AddBlazorPermissionScope` mit
`TenantSource.PathSegment` und `RouteOverrideParam = "tenant"`, Net-WebPart mit
`"TenantParam": "tenant"` und `UseDiagnostics = true`, dazu `app.UseTenantPathPrefix()`.

Eine `DiagnosticsQuery` anlegen, die den Kontext selbst befragt, und sie unter einem Mandanten
aufrufen, der **nicht** der Default-Mandant des angemeldeten Benutzers ist:

```csharp
/*#@#{U:System.Linq;U:Microsoft.EntityFrameworkCore;}#@#*/
var dc = new Dictionary<string,object>();
dc.Add("UseTenantFilter", db.UseTenantFilter);
dc.Add("CurrentTenant", db.CurrentTenant);
dc.Add("InstFiltered", db.WorkflowInstances.Count());
dc.Add("InstUnfiltered", db.WorkflowInstances.IgnoreQueryFilters().Count());
return new[]{ dc };
```

`CurrentTenant` meldet den Default-Mandanten statt des Segments aus der URL. Der Effekt braucht das
Workflow-Subsystem nicht — jeder tenant-abhängige Kontext hinter `UseDiagnostics`,
`UseAutoForeignKeys` oder `UseWidgets` zeigt dasselbe.

## Behebung

`BlazorContextUserProvider.RouteData` fällt jetzt auf die Routenwerte der laufenden Anfrage zurück —
`httpContextAccessor.HttpContext?.GetRouteData()?.Values`, also genau die Quelle, aus der der
`DefaultContextUserProvider` ausserhalb von Blazor liest. Zwei Stellen, wie vorgeschlagen:

1. im `catch (InvalidOperationException)` statt des leeren Dictionary;
2. im Erfolgsfall, wenn `ExtractFirstBaseSegment` nichts hergibt, für den `RouteOverrideParam`.

**Die Basis-URI bleibt vorne.** Im lebenden Circuit ist sie die richtige Quelle (und der HttpContext
dort ohnehin null); der HttpContext ist ausdrücklich der Rückfall. Das Per-Tab-Verhalten ist
unangetastet.

**Bewusst nur die Routenwerte, nicht die Query der Anfrage.** Ein Nicht-Blazor-Host gibt diesen
Endpunkten genau die Routenwerte — und ein `?tenant=` darf nicht zu einem Override-Kanal werden, den
derselbe Endpunkt ohne Blazor nicht hätte. (Der Getter nimmt die Query weiterhin auf, wenn der
NavigationManager läuft; das ist bestehendes Verhalten im Circuit.)

**Mitgezogen: `RequestPath`.** Dieselbe Lücke, derselbe Rückfall (`HttpContext.Request.Path`) — sonst
bliebe der Pfad bei genau diesen Endpunkten null, etwa wenn Anfragedaten für Hintergrundarbeit
konserviert werden.

**Das Startfenster bleibt unberührt**, wie im Report vermutet: bei `UsePluginsInit` gibt es keinen
HttpContext, der Rückfall läuft dort weiterhin ins leere Dictionary. Das ist als Test festgehalten.

`BlazorContextUserProviderTests` hat vier neue Fälle (bisher 5, jetzt 9, alle grün):

| Test | Prüft |
|---|---|
| `Uninitialized_Navigation_Falls_Back_To_Request_Route_Values` | der gemeldete Fall — kein Circuit, Mandant kommt aus der Route |
| `Uninitialized_Navigation_Without_Request_Yields_Empty` | das Startfenster bleibt leer |
| `Flat_Base_Falls_Back_To_Request_Route_Values` | initialisierter NavigationManager ohne Mandanten in der Basis-URI |
| `Base_Segment_Wins_Over_Request_Route_Values` | im Circuit gewinnt die Basis-URI |

### Was damit NICHT erledigt ist

Die beiden Nebenbefunde unten sind inzwischen **beide erledigt** — jeweils als eigene Arbeit, siehe
die Nachträge dort.

## Nebenbefunde (nicht Teil dieses Reports)

Bei der Analyse mitgefunden, jeweils ohne Auswirkung auf **diesen** Fehler:

1. **`TokenRow` hat keinen Query-Filter.** `WorkflowContextFilters.ConfigureFilters` konfiguriert nur
   `WorkflowDefinitionRow` (`TenantId == CurrentTenant || TenantId == null`) und
   `WorkflowInstanceRow` (strikt). Wer direkt über `db.Tokens` einsteigt, ist nur über den Join auf
   die Instanzen geschützt. Ob das Absicht ist, sollte einmal ausdrücklich entschieden und im
   Leitfaden festgehalten werden.

   > **Erledigt.** `TokenRow` hat jetzt denselben strikten Filter wie die Instanz. Die Arbeit lag beim
   > *Nicht*-Filtern: sechs Lesewege sind ausdrücklich ausgenommen — allen voran das Speichern (was es
   > nicht findet, legt es **neu** an → Schlüsselverletzung) und das Laden einer Instanz (die
   > Mandanten-Grenze zieht die Instanz; was an ihr hängt, gehört dazu). Dazu die Migration
   > `TokenTenantBackfill` für die Vorgänge, die seit vor der Spalte parken. Leitfaden §41.
2. **`ExpressionFixVisitor.RegisterExpression` ist „erster gewinnt"**
   (`if (!propertyReplacements.ContainsKey(name))`). Der über `ConfigureExpressionProperty(() => CurrentTenant)`
   registrierte Ausdruck ist eine `MemberExpression` über eine **Konstante** — die Kontext-Instanz,
   die als erste den Plugin-Ctor durchlaufen hat. Zusammen mit einem als Plugin `Transient = 0`
   registrierten Options-Provider und dem prozessweit einmal gebauten Modell (`CustomModelCache`
   liefert `"DEFAULT"`, solange der Kontext kein `ICustomModelIdProvider` implementiert) ist das eine
   latente Falle. Im Meldefall trägt sie nicht, weil EF Core Kontext-Referenzen in Query-Filtern auf
   die jeweils laufende Instanz umbindet — verlassen würde ich mich darauf ungern.

   > **Erledigt, in zwei Teilen — und der Befund war genau richtig gelesen.**
   >
   > **Erstens: die Zusage ist jetzt geprüft statt angenommen.** Die vorhandenen Tests konnten sie gar
   > nicht zeigen: sie geben jedem Kontext seinen **eigenen** Options-Provider, damit seine eigene
   > Registrierung — die Produktionsform (ein Provider als Plugin, ein Options-Objekt, ein Modell für
   > alle Kontexte) kam darin nie vor. Der neue Test
   > `SharedOptionsProvider_FilterFollowsTheRunningContext_NotTheFirstOne` baut genau sie nach: ein
   > geteilter Provider, `acme` registriert zuerst, `beta` fragt danach — und sieht seine eigenen Daten.
   > EF bindet die Konstante also wirklich um. Jetzt hält ein Test das fest, statt dass es jemand
   > wissen muss.
   >
   > **Zweitens: für alles ausser dem Kontext gilt die Zusage nicht** — und *das* ist die eigentliche
   > Falle. Wer `() => someService.Tenant` registriert, bekommt keine Umbindung, sondern genau diese
   > eine Instanz, dauerhaft, in jedem Filter, für jeden Benutzer; es übersetzt sauber und liefert
   > Ergebnisse, nur eben die des Ersten. `RegisterExpression` prüft deshalb jetzt, dass der Ausdruck
   > auf dem Kontext wurzelt (oder statisch ist), und wirft sonst mit Begründung. Ebenso wird ein
   > **anderes** Member unter demselben Platzhalter-Namen nicht mehr still verworfen, sondern
   > abgelehnt. „Erster gewinnt" bleibt — es trägt die mehrfache Konfiguration desselben
   > Options-Objekts —, ist aber jetzt dokumentiert und abgesichert.
