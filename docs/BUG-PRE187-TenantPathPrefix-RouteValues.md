# BUG (PRE187): Der Fix aus `710e8a81` greift bei Hosts mit `UseTenantPathPrefix()` nicht — die Routenwerte tragen den Mandanten dort nie

> **Nachtrag zu `BUG-PRE186-PermissionScope-NonBlazor-Endpoints.md`.** Der dortige Fix ist umgesetzt und
> im Ansatz richtig, bleibt aber für die Host-Bauart wirkungslos, aus der er gemeldet wurde. Am
> laufenden MLM auf PRE187 nachgemessen: `CurrentTenant` meldet weiterhin den Default-Mandanten.
>
> **Der Fehler liegt beim Melder.** Der ursprüngliche Report hat `DefaultContextUserProvider.RouteData`
> (`HttpContext.GetRouteData().Values`) als „die richtige Quelle" benannt, ohne zu prüfen, ob die
> Routenwerte den Mandanten bei aktivem `UseTenantPathPrefix()` überhaupt noch führen. Sie tun es
> nicht. Der Fix ist dieser Angabe gefolgt.

## Übersicht

| | |
|---|---|
| **Betroffen** | `ITVComponents.WebCoreToolkit.Blazor/Security/BlazorContextUserProvider.cs`, `RequestRouteValues()` (Zeile 137-138) und die beiden Aufrufstellen im `RouteData`-Getter (Zeile 106 und 115-122) |
| **Kern** | `TenantPathPrefixMiddleware` **entfernt** das Mandanten-Segment aus `Request.Path` und hängt es an `PathBase`, **bevor** das Endpoint-Routing läuft. `GetRouteData().Values` kann den Mandanten danach nicht mehr enthalten. |
| **Folge** | Der Rückfall läuft ins Leere, `ResolvingPermissionScope` fällt weiterhin auf `DefaultScopeExpression` zurück. Der gemeldete Fehler besteht unverändert. |
| **Wo der Mandant steht** | `HttpContext.Items["ITVComponents.WebCoreToolkit.Blazor.TenantSegment"]` und als **letztes** Segment von `Request.PathBase` — beides von derselben Middleware gesetzt, im selben Assembly. |
| **Nicht betroffen** | Hosts **ohne** `UseTenantPathPrefix()`. Dort ist der Mandant ein echter Routenwert und der Fix wirkt wie beabsichtigt. |

## Messung

Sonden-`DiagnosticsQuery` gegen denselben `WorkflowContext`, den die Abfrage bekommt, aufgerufen unter
`/440304d1004f415e95ca89053d5363cd/diagnostics/WfCtxProbe`. Laufende Instanz nachweislich auf
`5.0.0-PRE187+636007e9`:

```json
{"CurrentTenant":"ADM",
 "InstFiltered":17, "InstUnfiltered":21,
 "TokensFiltered":947, "TokensUnfiltered":951,
 "Path":"/diagnostics/WfCtxProbe",
 "PathBase":"/440304d1004f415e95ca89053d5363cd",
 "ItemsTenantSegment":"440304d1004f415e95ca89053d5363cd",
 "RouteKeys":"diagnosticsQueryName"}
```

Das ist der ganze Fall in einer Zeile: der Mandant steht **zweifach** bereit (`PathBase`,
`ItemsTenantSegment`), `RouteKeys` führt ihn **nicht**, und `CurrentTenant` bleibt beim Default.

Nebenbei mitgemessen und **in Ordnung**: der Mandantenfilter auf `TokenRow` aus `dd92268d` arbeitet —
947 gegen 951, die Differenz sind exakt die vier Tokens des anderen Mandanten.

## Root Cause

`TenantPathPrefixMiddleware.cs:121-130`:

```csharp
context.Items[TenantSegmentItemKey] = firstSegment;

// Strip the tenant segment from Request.Path and append it to Request.PathBase — same
// shape as app.UsePathBase, but per-tenant. Without the strip, endpoint routing would
// … PathBase set, Blazor's NavigationManager / link-generation stay tenant-aware downstream
var prefix = "/" + firstSegment;
context.Request.PathBase = context.Request.PathBase.Add(new PathString(prefix));
context.Request.Path = path.Length > prefix.Length …
```

Die Middleware läuft **vor** `UseRouting`. Wenn das Endpoint-Routing den Pfad sieht, ist das
Mandanten-Segment weg. Damit ist der Mandant in `GetRouteData().Values` strukturell nicht erreichbar —
nicht „meistens nicht", sondern nie.

### Die tenant-tragende Routen-Variante ist bei solchen Hosts unerreichbar

`WebPartInit.RegisterNetDefaultEndPoints` registriert unter `!string.IsNullOrEmpty(options.TenantParam)
&& options.WithTenants` die Variante `/{tenant:permissionScope}/Diagnostics/…` (Zeile 51-101) und unter
`options.WithoutTenants` zusätzlich die mandantenlose (Zeile 104ff., `Register(builder, null, …)`).

Der gemessene `Path` hat nach dem Strippen nur noch zwei Segmente — die dreisegmentige, tenant-tragende
Variante **kann** also gar nicht mehr matchen. `RouteKeys = "diagnosticsQueryName"` bestätigt genau das:
gematcht hat die mandantenlose Route. Ein Host, der `UseTenantPathPrefix()` einsetzt, erreicht die
`{tenant:permissionScope}`-Endpunkte nie; er lebt vollständig von der mandantenlosen Variante plus
`PathBase`.

Das ist zugleich die Antwort auf die Frage, warum der Fix nicht „manchmal" danebengeht, sondern immer:
`RouteOverrideParam` kann in dieser Bauart aus Routenwerten prinzipiell nicht kommen.

## Fix-Vorschlag

Die Quellen-Reihenfolge im `RouteData`-Getter um die Middleware-Ablage ergänzen — eine Stufe **vor** den
Routenwerten:

1. **Basis-URI** (lebender Circuit) — unverändert vorne, wie in `710e8a81` begründet.
2. **`HttpContext.Items[TenantPathPrefixMiddleware.TenantSegmentItemKey]`** — neu. Gesetzt von der
   Middleware im **selben Assembly** (`ITVComponents.WebCoreToolkit.Blazor/Security/`), die Konstante
   ist bereits `public const`. Keine neue Abhängigkeit, keine Zeichenketten-Literale.
3. **Routenwerte** — der bestehende Rückfall aus `710e8a81`, weiterhin richtig für Hosts ohne die
   Middleware.

Zwei Punkte, die dabei leicht zu übersehen sind:

- **Nicht das erste Segment von `PathBase` nehmen.** Die Middleware **hängt an**
  (`context.Request.PathBase.Add(prefix)`). Läuft die Anwendung in einem virtuellen Verzeichnis, ist
  das erste Segment die Anwendung und der Mandant das **letzte**. Die `Items`-Ablage hat diese
  Mehrdeutigkeit nicht — deshalb sie und nicht `PathBase`.
- **Der Wert aus `Items` ist bereits geprüft.** Die Middleware setzt ihn erst, nachdem sie das Segment
  gegen die `EligibleScopes` des Benutzers validiert hat (`TenantPathPrefixMiddleware.cs:112-121`,
  sonst 404). `ResolvingPermissionScope` prüft ohnehin erneut — die Mandanten-Grenze wird also nicht
  aufgeweicht, sondern doppelt gehalten.

### Mitzuprüfen: `RequestPath`

`710e8a81` hat `RequestPath` denselben Rückfall auf die Anfrage gegeben. Bei aktiver Middleware
liefert `HttpContext.Request.Path` den **gestrippten** Pfad (`/diagnostics/WfCtxProbe`), nicht den, den
der Aufrufer in der Adresszeile hatte. Ob das für die Konsumenten von `RequestPath` das gewünschte
Verhalten ist — insbesondere dort, wo Anfragedaten für Hintergrundarbeit konserviert werden —, wäre
einmal zu entscheiden; gegebenenfalls gehört `PathBase` davorgesetzt.

## Reproduktion

Host mit `AddBlazorContextUser()` + `AddBlazorPermissionScope(TenantSource.PathSegment,
RouteOverrideParam = "tenant")` **und** `app.UseTenantPathPrefix()`, Net-WebPart mit
`UseDiagnostics = true`. Diese `DiagnosticsQuery` anlegen und unter einem Mandanten aufrufen, der nicht
der Default-Mandant des angemeldeten Benutzers ist:

```csharp
/*#@#{U:System.Linq;U:Microsoft.EntityFrameworkCore;U:Microsoft.AspNetCore.Http;U:Microsoft.AspNetCore.Routing;}#@#*/
var dc = new Dictionary<string,object>();
dc.Add("CurrentTenant", db.CurrentTenant);
IHttpContextAccessor hca = (IHttpContextAccessor)Services.GetService(typeof(IHttpContextAccessor));
HttpContext http = hca == null ? null : hca.HttpContext;
dc.Add("Path", http.Request.Path.Value);
dc.Add("PathBase", http.Request.PathBase.Value);
object seg = http.Items["ITVComponents.WebCoreToolkit.Blazor.TenantSegment"];
dc.Add("ItemsTenantSegment", seg == null ? "<null>" : seg.ToString());
RouteData rd = http.GetRouteData();
dc.Add("RouteKeys", rd == null ? "<null>" : string.Join(",", rd.Values.Keys));
return new[]{ dc };
```

`RouteKeys` enthält den `RouteOverrideParam` nicht, `PathBase` und `ItemsTenantSegment` schon,
`CurrentTenant` meldet den Default-Mandanten.

Ein Test für die Fix-Recherche liesse sich ohne Web-Host bauen: `IHttpContextAccessor` mit einem
`DefaultHttpContext` bestücken, dessen `Items` den Schlüssel trägt, `PathBase` auf `/{tenant}` steht und
dessen Routenwerte den Mandanten **nicht** enthalten — genau die Konstellation, die die vorhandenen
Tests aus `710e8a81` nicht abdecken, weil sie den Mandanten in die Routenwerte legen.

## Umsetzung

Umgesetzt wie vorgeschlagen. Die Quellen-Reihenfolge im `RouteData`-Getter lautet jetzt Basis-URI →
`HttpContext.Items[TenantPathPrefixMiddleware.TenantSegmentItemKey]` → Routenwerte; die Konstante kommt
aus der Middleware im selben Assembly, es gibt kein Zeichenketten-Literal und keine neue Abhängigkeit.

Zwei Punkte, die beim Umsetzen dazukamen:

- Der Rückfall im uninitialisierten Zweig gab bisher die `RouteValueDictionary` **der Anfrage selbst**
  zurück. Den Mandanten dort hineinzuschreiben hiesse, dass das Lesen dieses Getters die Routenwerte der
  laufenden Anfrage verändert. Der Zweig baut jetzt eine Kopie und legt den Mandanten darauf.
- `RequestPath` liefert im Rückfall `PathBase + Path` statt nur `Path` — siehe unten.

`RequestPath` (der offene Punkt oben) ist damit entschieden: **`PathBase` gehört davor.** Bei aktiver
Middleware meldete der Rückfall sonst `/diagnostics/Q` für einen Aufrufer, der `/TenantA/diagnostics/Q`
angefragt hat — ein Pfad, der die Anfrage nicht mehr benennt und beim Wiederholen (etwa aus konservierten
Anfragedaten heraus) in einem anderen Mandanten landet. Der Circuit-Zweig darüber hat den ganzen Pfad
ohnehin, weil das `<base href>` das Präfix trägt; beide Zweige beantworten damit wieder dieselbe Frage.
Mitgezogen: das `catch` dort fängt jetzt gezielt `InvalidOperationException` (den fehlenden Circuit)
statt pauschal alles.

Tests: 6 neue in `BlazorContextUserProviderTests` — die Anfrage hinter dem Präfix (Routenwerte **ohne**
Mandanten, `Items` und `PathBase` mit), mit und ohne initialisierten `NavigationManager`, der Vorrang der
Basis-URI im Circuit, der Vorrang der Middleware-Ablage vor einem Routenwert, die Unversehrtheit der
Routenwerte beim Lesen und der Pfad mit Präfix. 43 Tests wurden 49, alle grün.
