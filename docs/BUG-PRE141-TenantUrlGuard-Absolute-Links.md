# BUG (PRE141): `TenantUrlGuard` ist **kein** Sicherheitsnetz gegen root-absolute `<a href>` — Doku verspricht zu viel

> **Gemeldet aus der MLM-Konsumenten-Session, 2026-07-27.** Nachgang zum Workflow-Routing-404
> (`BUG-PRE141-Workflow-Integration.md`). Die Toolkit-Session hatte als Nebenbefund vermutet, es gebe
> mit `TenantUrlGuard` bereits ein Sicherheitsnetz gegen root-absolute Navigationen, und dass trotzdem
> ein 404 kam, heisse, im Host sei `<TenantUrlGuard />` nicht in der Wurzel platziert oder
> `RouteOverrideParam` nicht gesetzt. **Beide Verdächtigungen sind widerlegt** — das Host-Wiring ist
> vollständig. Die tatsächliche Ursache ist eine strukturelle Lücke im Guard, die kein Host-Wiring
> schliessen kann.

## Übersicht

| | |
|---|---|
| **Betroffen** | `ITVComponents.WebCoreToolkit.Blazor/Security/TenantUrlGuard.cs` (Doku + Wirkbereich), alle Blazor-View-Pakete, die eigene Links/Navigationen emittieren |
| **Kern** | Der Guard hängt ausschliesslich an `RegisterLocationChangingHandler`. Ein `<a href="/foo">` mit `<base href="/{tenant}/">` erreicht den Circuit **nie** — Blazor's JS fängt den Klick gar nicht erst ab. Der Guard kann diesen Fall prinzipiell nicht sehen. |
| **Abgedeckt** | Programmatisches `NavigationManager.NavigateTo("/foo")` — das läuft nachweislich durch den Handler und wird korrekt umgeschrieben. |
| **Nicht abgedeckt** | Jeder Anker-Klick auf ein root-absolutes Ziel ausserhalb der Base-URI, sowie alles, was den Circuit verlässt (Full Reload, Form-Post, `window.location`). |
| **Folge** | `TenantPathPrefixMiddleware` sieht `/Workflow/…` als erstes Segment, findet es nicht unter den eligible Scopes → **404**. |

## Symptom

In-Page-Navigation aus den WorkflowViews (nicht aus dem Menü) landete auf 404, obwohl der Nutzer im
gültigen Tenant-Kontext war und die Menüeinträge alle funktionierten.

Antwort erzeugt hier — `ITVComponents.WebCoreToolkit.Blazor/Security/TenantPathPrefixMiddleware.cs:112-118`:

```csharp
if (!eligible.Any(s => string.Equals(s.ScopeName, firstSegment, StringComparison.Ordinal)))
{
    // Same response whether the tenant doesn't exist or the user just isn't eligible — no
    // information leak about which tenants exist.
    logger.LogInformation("TenantPathPrefix: segment '{Segment}' not in user's eligible scopes; responding 404 for {Path}", firstSegment, path);
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    return;
}
```

Dass diese Zeile antwortet, beweist bereits: **der Request war real am Server**, es war also keine
In-Circuit-Navigation — der `LocationChanging`-Handler des Guards hatte gar keine Gelegenheit zu laufen.

## Root Cause

### 1. Anker-Klicks auf Ziele ausserhalb der Base-URI erreichen den Circuit nie

Der Guard registriert sich ausschliesslich hier — `TenantUrlGuard.cs:69`:

```csharp
handlerRegistration = Navigation.RegisterLocationChangingHandler(OnLocationChanging);
```

Blazor ruft `LocationChanging`-Handler bei einem Anker-Klick nur, wenn die JS-Seite den Klick
**abfängt**. Sie fängt ihn genau dann ab, wenn das aufgelöste Ziel innerhalb des Base-URI-Raums liegt
— `dotnet/aspnetcore`, `release/10.0`, `src/Components/Web.JS/src/Services/NavigationUtils.ts:28-48`:

```typescript
const absoluteHref = toAbsoluteUri(anchorHref);

if (isWithinBaseUriSpace(absoluteHref)) {
  event.preventDefault();
  callbackIfIntercepted(absoluteHref);
}
…
export function isWithinBaseUriSpace(href: string) {
  const baseUriWithoutTrailingSlash = toBaseUriWithoutTrailingSlash(document.baseURI!);
  const nextChar = href.charAt(baseUriWithoutTrailingSlash.length);

  return href.startsWith(baseUriWithoutTrailingSlash)
  && (nextChar === '' || nextChar === '/' || nextChar === '?' || nextChar === '#');
}
```

Konkret im PathSegment-Modus: `document.baseURI = "https://host/adm/"` → Präfix `"https://host/adm"`;
`href="/Workflow/Definitions"` → `"https://host/Workflow/Definitions"` → beginnt **nicht** mit dem
Präfix → kein `preventDefault` → **normaler Browser-Full-Load** → Middleware → 404.

Das ist keine Fehlkonfiguration und kein Bug in Blazor, sondern die Definition des Base-URI-Raums.
**Kein** Code im Guard kann das reparieren: die Navigation existiert serverseitig erst als neuer
HTTP-Request, der alte Circuit ist zu diesem Zeitpunkt bereits tot.

### 2. Programmatisches `NavigateTo` ist dagegen abgedeckt — die XML-Doku ist nur für diesen Fall korrekt

`TenantUrlGuard.cs:20-23` sagt:

```text
PathSegment: relies on the host emitting <base href="/{tenant}/"> so relative navigations automatically
carry the prefix; only catches absolute-path navigations (e.g. NavigateTo("/users")) that would escape
the tenant prefix and rewrites them under the current base path.
```

Für `NavigateTo` stimmt das nachweislich — Blazor Server ruft die Handler **vor** jedem JS-Interop und
**ohne** jede Base-URI-Prüfung, `src/Components/Server/src/Circuits/RemoteNavigationManager.cs:128-136`:

```csharp
var shouldContinueNavigation = await NotifyLocationChangingAsync(uri, options.HistoryEntryState, false);
if (!shouldContinueNavigation) { Log.NavigationCanceled(_logger, uri); return; }
await _jsRuntime.InvokeVoidAsync(Interop.NavigateTo, uri, options);
```

`src/Components/Components/src/NavigationManager.cs:355-378` (`NotifyLocationChangingAsync`) enthält
keinerlei Base-URI-Test; einziger Early-Out ist „kein Handler registriert". `PlanPathSegmentRewrite`
(`TenantUrlGuardLogic.cs:41-79`) schreibt danach korrekt auf `basePath + absolutePath` um.

**Konsequenz für die Formulierung des Fixes:** der Satz „der Guard fängt absolute Navigationen ab" ist
nur für den programmatischen Pfad wahr. In der Doku fehlt der Halbsatz, der den Anker-Fall ausnimmt —
und genau daraus entstand die Fehldiagnose „dann muss beim Konsumenten das Wiring fehlen".

### 3. Widerlegte Zwischenhypothese: per-Page-`@rendermode` erzeugt **keine** eigene Insel

Jede WorkflowViews-Seite deklariert einen eigenen Render-Mode
(`@rendermode @(RenderMode.InteractiveServer)` in `WorkflowHome.razor:2`, `WorkflowDefinitions.razor:2`,
`WorkflowDefinitionViewer.razor:2`, `WorkflowEditor.razor:3`, `WorkflowInstances.razor:2`), während der
Host (MLM) den Modus global an `<Routes @rendermode="…" />` setzt. Die naheliegende Vermutung — die
Seite laufe in einem eigenen Renderer, in dem der Guard (aus `MainLayout`) nicht registriert ist — ist
**falsch**. `src/Components/Server/src/Circuits/RemoteRenderer.cs:310-315`:

```csharp
protected override IComponent ResolveComponentForRenderMode(Type componentType, int? parentComponentId, IComponentActivator componentActivator, IComponentRenderMode renderMode)
    => renderMode switch
    {
        InteractiveServerRenderMode or InteractiveAutoRenderMode => componentActivator.CreateInstance(componentType),
        _ => throw new NotSupportedException(…),
    };
```

Innerhalb eines bereits interaktiven Server-Renderers wird der deklarierte Modus **nur per Typ-Pattern**
geprüft (das `prerender`-Flag geht gar nicht ein, `InteractiveServerRenderMode(prerender:false)` am
Vorfahren und `RenderMode.InteractiveServer` am Kind kollidieren also nicht) und die Komponente **im
selben Renderer** instanziiert. Kein Boundary, kein zweiter Circuit. Der Guard war live.

Die per-Page-Deklarationen sind damit unschädlich, aber auch wirkungslos, solange der Host global setzt.

### 4. Warum das Menü trotzdem funktioniert — nicht dank des Guards

Alle `Navigation.Url`-Werte liegen root-absolut in der DB (`/Security/Tenants`, …) und werden vom
Konsumenten roh als `<a href>` gerendert. Dass sie funktionieren, liegt **nicht** am Guard (siehe §1),
sondern am Präfix, das der Navigationsaufbau selbst setzt —
`ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity/Shared/Navigation/DbNavigationBuilder.cs:136`:

```csharp
Url = !string.IsNullOrEmpty(item.Url) ? $"{(!string.IsNullOrEmpty(explicitTenant) ? $"/{explicitTenant}" : "")}{(!item.Url.StartsWith("/") ? "/" : "")}{item.Url}" : ""
```

Der Menü-Href ist zur Renderzeit also bereits `/adm/Security/Tenants` und liegt damit im Base-URI-Raum.
Das ist die richtige Bauart — und zugleich der Beleg, dass jede Komponente, die ihre Links **selbst**
baut, dieselbe Verantwortung trägt.

## Aktueller Stand / Umgehung

Die WorkflowViews sind im Toolkit-Checkout bereits auf relative Ziele umgestellt (kein führender Slash,
löst gegen `<base href="/{tenant}/">` auf) — `WorkflowDefinitions.razor:116/121`,
`WorkflowEditor.razor:753/763`, Links in `WorkflowHome.razor:28`. Das ist für den konkreten Fall die
korrekte Behebung. Der Konsument musste nichts ändern; das Host-Wiring war und ist vollständig:

- `<TenantUrlGuard />` in `MainLayout.razor:15` (Wurzel, direkt nach `<ContextUserInitializer />`)
- `options.RouteOverrideParam = "tenant"`, `options.TenantSource = TenantSource.PathSegment` (`Program.cs:111/115`)
- `app.UseTenantPathPrefix()` (`Program.cs:302`), `<TenantBaseHref />` (`App.razor:14`)

## Vorschläge

**a) Doku am `TenantUrlGuard` präzisieren (minimal, aber der eigentliche Punkt dieses Reports).**
Der XML-Kommentar `TenantUrlGuard.cs:20-23` sollte explizit sagen, dass der Guard nur In-Circuit-
Navigationen sieht (`NavigateTo`, abgefangene Links **innerhalb** der Base-URI) und dass root-absolute
`<a href>` ausserhalb der Base-URI, Form-Posts und `window.location` strukturell **nicht** abgedeckt
sind. Sonst wird bei jedem künftigen 404 wieder zuerst beim Host-Wiring gesucht.

**b) Regel für alle Toolkit-View-Pakete:** Ziele relativ (ohne führenden Slash) emittieren. Root-absolut
ist nur dort korrekt, wo der Pfad bewusst **tenant-neutral** ist, also unter
`ScopedPermissionScopeOptions.AuthPathExclusions` fällt (`/Account/`, `/Identity/Account/`, `/Login`,
`/Logout`, `/signin-`, `/signout-`, `ScopedPermissionScopeOptions.cs:51-58`).
Ein Scan der übrigen Blazor-Pakete zeigt aktuell nur solche legitimen Fälle:

| Fundstelle | Bewertung |
|---|---|
| `AdminViews/OnboardingViews/…/CreateTenant.razor:36,80`, `Invitation.razor:33`, `Join.razor:53,61,64`, `JoinRegister.razor:44,143`, `MyTenants.razor:21`, `Start.razor:24,48` | **legitim** — alle unter `/Account/…`, per Default excluded, Middleware lässt sie unangetastet durch |
| `MudBlazor.IdentityPages/…/Manage/DeletePersonalData.razor:127` (`NavigateTo("/", forceLoad: true)`) | **legitim** — `/` wird von der Middleware auf den Default-Scope umgeleitet (`TenantPathPrefixMiddleware.cs:102-110`) |

Es besteht also kein akuter weiterer Handlungsbedarf; die Regel wäre für neue Views festzuhalten
(ggf. als Test/Analyzer, der `Href="/…"` bzw. `NavigateTo("/…")` in View-Paketen gegen die
Exclusion-Liste prüft).

**c) Optionales serverseitiges Sicherheitsnetz — schliesst die Lücke dort, wo sie behebbar ist.**
Da die Anker-Navigation den Circuit verlässt, kann nur die Middleware sie retten. Vorschlag für
`TenantPathPrefixMiddleware`, **vor** dem 404 in Zeile 112-118: wenn (1) es eine Top-Level-
Dokumentnavigation ist (`Sec-Fetch-Mode: navigate` bzw. `Accept: text/html`, GET), (2) ein same-origin
`Referer` vorliegt, dessen **erstes Segment ein eligible Scope des aktuellen Users** ist, und (3) der
Zielpfad nicht ohnehin mit diesem Scope beginnt → `302` auf `/{scopeAusReferer}{path}` statt 404.
Kein Info-Leak (es werden nur Scopes verwendet, für die der User bereits eligible ist), keine
Redirect-Schleife (nach dem Redirect matcht das erste Segment). Wer das nicht will, deaktiviert es über
ein Flag in `ScopedPermissionScopeOptions`. Damit wäre der Konsumenten-seitige Effekt (404 statt
funktionierender Seite) auch dann abgefangen, wenn irgendein Paket künftig wieder root-absolut linkt.

## Was verifiziert ist

- Host-Wiring des Konsumenten vollständig — Datei/Zeile oben, aus dem Code gelesen.
- 404-Quelle `TenantPathPrefixMiddleware.cs:112-118` — aus dem Code, inkl. der Schlussfolgerung
  „Request war am Server, also keine In-Circuit-Navigation".
- Anker-Interception-Regel (§1), `NavigateTo`-Handler-Reihenfolge (§2) und Render-Mode-Auflösung (§3)
  — gegen `dotnet/aspnetcore`, Branch `release/10.0`, Originalquellen gelesen und oben zitiert.
- Menü-Präfixierung (§4) — `DbNavigationBuilder.cs:136` aus dem Toolkit-Checkout.
- Scan der Blazor-View-Pakete auf root-absolute Ziele (Tabelle unter b) — vollständig über
  `AdminViews`, `BillingViews`, `MudBlazor.IdentityPages`, `MudBlazor`, `WorkflowViews`.

## Was **nicht** verifiziert ist

- **Kein eingeloggter E2E.** Es gab in der Konsumenten-Session keine Credentials für einen Lauf mit
  echtem Tenant-Präfix; alles oben stammt aus Code-Lektüre, nicht aus einem beobachteten Request.
- **Welcher der beiden Aufrufwege den konkret beobachteten 404 erzeugt hat, ist nicht bewiesen.** Für
  die Anker-Links in `WorkflowHome` ist der 404 die zwingende Folge (§1). Für die `NavigateTo`-Aufrufe
  in `WorkflowDefinitions`/`WorkflowEditor` müsste der Guard nach §2 gegriffen haben — falls der Nutzer
  den 404 auf **genau** einem dieser `NavigateTo` gesehen hat, gäbe es einen zweiten, noch unerklärten
  Effekt. Der naheliegendste Kandidat wäre dann ein `basePath`, der im Guard als `/` erfasst wurde
  (`TenantUrlGuard.cs:61/95` — Guard ist dann komplett inaktiv), etwa weil `<TenantBaseHref />` beim
  betreffenden Request auf den Fallback `/` gelaufen ist. Das liesse sich nur mit einem eingeloggten
  Lauf entscheiden (Base-Href im gerenderten HTML prüfen).
- Der Vorschlag (c) ist ein Entwurf, kein geprüfter Patch — insbesondere die Interaktion mit
  Antiforgery/POST-Redirects wurde nicht durchdacht (Vorschlag beschränkt sich bewusst auf GET).
