# Migrationsleitfaden — Branch `Future_10` (Phasen 2–5 + Onboarding-Flows)

> **Stand: `5.0.0-PRE130`** (Branch `Future_10`). Dieses Dokument deckt die Cross-cutting-Refactors
> (Phasen 2–5, §1–5), die Onboarding-Flows (2a/2b/2c, §6), die EntityWriteTracker-/EntityChangeSignal-
> Invalidierung (§7/7a), die Per-Operation-Contexts (§8), die Auto-Permission-Registration (§9) und die
> Onboarding-Tenant-Anlage/-Härtung (§10) ab — **plus die PRE118-Neuerungen: Paket-Versionen (§12),
> `ItvErrorBoundary` (§13), cross-tenant PermissionSet-Propagation (§14, Pflicht-Migration) und
> Tenant-Template `BasicTenantType`/Apply-Modes/Re-apply (§15)** — **sowie die neueren Punkte: Billing
> Add-ons n:m (§16, Pflicht-Migration), erweiterbarer Config-Export/Billing-Sektion (§17), typisierte
> TenantTemplate-Extensions (§18) und Navigations-Metadata/Help-Button (§19, `PRE130`, Pflicht-Migration)**.

Dieser Leitfaden beschreibt, was im MLM-Projekt anzupassen ist, um auf den `Future_10`-Stand der
ITVComponents-Toolkit zu wechseln. Es ist ein **Major-Release (5.0-PRExx)** mit bewussten Breaking
Changes (keine Shims). Die Umbauten ziehen MVC-/HTTP-Kopplung aus der Kernlogik heraus, damit MVC **und**
Blazor dieselben Services teilen. Die neue, framework-neutrale Assembly heißt
`ITVComponents.WebCoreToolkit.ServiceShared`.

Reihenfolge der Abschnitte = empfohlene Reihenfolge der Migration. Pro Abschnitt: **was bricht** →
**wie anpassen**.

> **Companion-Dokument:** Die **Paket-Konsolidierung** (NuGet-ID-Umbenennungen 103→71, `using`-Sweeps,
> WebPart-Config-Key-Änderungen) ist separat in
> [`Migration-Future_10-MLM-Packaging.md`](Migration-Future_10-MLM-Packaging.md) beschrieben. Für den
> aktuellen Stand (`5.0.0-PRE118`) **beide** Dokumente durcharbeiten.

---

## 0. Neue Assembly-Referenz

`ITVComponents.WebCoreToolkit.ServiceShared` ist die gemeinsame Basis für MVC und Blazor (Verträge +
DTOs + EF-nutzende Shared-Services). Sie kommt transitiv über `…WebCoreToolkit.Net` bzw.
`…WebCoreToolkit.Blazor.*` rein — eine direkte `<ProjectReference>`/`<PackageReference>` ist nur nötig,
wenn ihr neutrale Typen (z.B. `FileOperationResult`) direkt verwendet.

Host-neutrale Service-Registrierung (auch ohne `AddControllers`/Endpoint-Mapping aufrufbar):

```csharp
services.AddWebCoreToolkitServiceShared();   // Diagnostics-Query-Service etc.
```

---

## 1. FileHandler-Verträge (Phase 2) — **bricht jeden eigenen FileHandler**

`IFileHandler` / `IAsyncFileHandler` haben einen neuen Namespace und eine neue `AddFile`-Signatur:
der `ModelStateDictionary`-Parameter ist **weg**, stattdessen wird ein **`FileOperationResult`**
zurückgegeben.

| vorher | nachher |
|---|---|
| `using ITVComponents.WebCoreToolkit.Net.FileHandling;` | `using ITVComponents.WebCoreToolkit.ServiceShared.FileHandling;` |
| `void AddFile(…, ModelStateDictionary ms)` | `FileOperationResult AddFile(…)` |
| `Task AddFile(…, ModelStateDictionary ms)` (async) | `Task<FileOperationResult> AddFile(…)` |

**Anpassen:**

```csharp
// vorher
public void AddFile(string fileName, byte[] content, /*…*/ ModelStateDictionary ms)
{
    if (problem)
        ms.AddModelError("File", "Datei ungültig");
    // sonst: still durchlaufen
}

// nachher
public FileOperationResult AddFile(string fileName, byte[] content /*…*/)
{
    if (problem)
        return FileOperationResult.Fail("File", "Datei ungültig");

    return FileOperationResult.Ok();
}
```

`FileOperationResult` ist immutable: `Success` + `IReadOnlyList<FileError>` (`FileError { Key, Message }`).
Der Aufruf-Rand mappt das Ergebnis selbst (MVC → ModelState, Blazor → `ValidationMessageStore`) — euer
Handler kennt nur noch den neutralen Vertrag.

Der **Read-Pfad** ist auf einen einzigen Träger vereinheitlicht: sowohl der async- als auch der sync-Handler
geben jetzt `FileReadResult` zurück (Stream-basiert, mit `DeferredDisposals`). Der frühere sync-`ReadFile`
mit `ref`/`out byte[]` entfällt:

| vorher (sync) | nachher (sync) |
|---|---|
| `bool ReadFile(string id, IIdentity user, ref string downloadName, ref string contentType, ref bool fileDownload, out byte[] content)` | `FileReadResult ReadFile(string id, IIdentity user)` |

Ein sync-Handler wrappt seine Bytes einfach in einen `MemoryStream`; `Success = false` signalisiert „nicht gefunden".

---

## 2. Responding-FileHandler / Config-Exchange (Phase 4) — **bricht eigene Responding-Handler**

`IRespondingFileHandler` / `IAsyncRespondingFileHandler` liegen jetzt ebenfalls in ServiceShared, und
`GetUploadResult()` gibt statt eines MVC-`IResult` denselben neutralen **`FileReadResult`** zurück wie der
Read-Pfad (kein separates `FileUploadResponse` mehr).

| vorher | nachher |
|---|---|
| `using ITVComponents.WebCoreToolkit.Net.FileHandling;` | `using ITVComponents.WebCoreToolkit.ServiceShared.FileHandling;` |
| `IResult GetUploadResult()` | `FileReadResult GetUploadResult()` |
| `Results.Text(json)` / `Results.Bytes(…)` | `new FileReadResult { Success = true, FileContent = new MemoryStream(bytes), ContentType = … }` |

```csharp
// vorher
public IResult GetUploadResult() => Results.Text(diffJson, "application/json");

// nachher
public FileReadResult GetUploadResult() => new()
{
    Success = true,
    FileContent = new MemoryStream(Encoding.UTF8.GetBytes(diffJson)),
    ContentType = "application/json"
};
```

> `IFormProcessor` / `IAsyncFormProcessor` bleiben vorerst in `…Net.FileHandling` (kein Blazor-Pendant,
> bewusst zurückgestellt). Falls ihr die implementiert: Using bleibt `…Net.FileHandling`.

**Blazor-Bonus:** Die Toolkit-Komponenten `FileUpload.razor` / `FileDownload.razor` lösen FileHandler
jetzt **in-process** per DI auf (`Services.GetFileHandler(module)`), ganz ohne `/File`-MVC-Endpoint.
Wer die AssemblyDiagnostics-/Config-Exchange-Maske in Blazor nutzt, braucht den MVC-`/File`-Endpoint
nicht mehr mitzuhosten.

---

## 3. Diagnostics-Queries (Phase 3) — nur falls ihr DiagnosticsQuery-Texte habt

Der Skriptkopf stellt nicht mehr `HttpContext context = Global.HttpContext;` bereit, sondern direkt
`ClaimsPrincipal User` und `IServiceProvider Services`. In den **Query-Texten** umstellen:

| vorher | nachher |
|---|---|
| `context.User` | `User` |
| `context.RequestServices` | `Services` |
| `context.RequestServices.GetService<X>()` | `Services.GetService<X>()` |

Query-Texte, die echte Request/Response-Spezifika brauchten, müssen explizit auf `User`/`Services`
umgestellt werden (solche Fälle sind erfahrungsgemäß selten). Wer noch **keine** Diagnose-Queries hat
(aktueller MLM-Stand), muss hier nichts tun — nur künftig die neue Konvention verwenden.

---

## 4. Ambient-User-Vertrag (Phase 5.1–5.3) — falls ihr `IHttpContextAccessor` für den User nutzt

Es gibt jetzt eine host-neutrale Abstraktion **`IContextUserProvider`** (in der Basis), die in MVC **und**
Blazor funktioniert. Sie ersetzt die meisten direkten `IHttpContextAccessor`-Uses, die nur den
aktuellen Benutzer / die Services / die Route brauchen.

```csharp
public interface IContextUserProvider
{
    ClaimsPrincipal User { get; }              // MVC: HttpContext.User; Blazor: gecachter Principal
    IServiceProvider Services { get; }
    IDictionary<string, object> RouteData { get; }
    string RequestPath { get; }
}
```

- **Nur User/Services/Route gebraucht?** → injiziert `IContextUserProvider` statt `IHttpContextAccessor`.
- **Echten `HttpContext` gebraucht** (request-spezifisch, MVC-only)? → injiziert `IHttpContextUserProvider`
  (`: IContextUserProvider` + `HttpContext HttpContext { get; }`).

Wer eigene `IContextUserProvider`-Implementierungen hat: der `HttpContext`-Member ist aus dem
Basis-Vertrag in `IHttpContextUserProvider` gewandert.

Eigene `DbNavigationBuilder`-Ableitungen: ctor-Parameter `IHttpContextAccessor` → `IContextUserProvider`
(alle sind DI-registriert; falls ihr nicht selbst `new`t, übernimmt das die DI automatisch).
`TemplateHandlerFactory`: der `IHttpContextAccessor`-ctor-Parameter ist entfallen (Kultur kommt aus
`CultureInfo.CurrentUICulture`).

---

## 5. SwitchTenant / PermissionScope (Phase 5.4)

### 5a. `DefaultScopeExpression`-Signatur (MVC) — **bricht eure Cookie-Scope-Konfiguration**

`CookieScopeOptions.DefaultScopeExpression` bekommt statt `HttpContext` ein `IContextUserProvider`:

```csharp
// vorher
services.UseCookiePermissionScope(o =>
{
    o.ScopeCookie = "scope";
    o.RouteOverrideParam = "tenant";
    o.DefaultScopeExpression = (httpContext, eligibles) =>
    {
        var user = httpContext.User;
        // …
        return eligibles.FirstOrDefault()?.ScopeName;
    };
});

// nachher
services.UseCookiePermissionScope(o =>
{
    o.ScopeCookie = "scope";
    o.RouteOverrideParam = "tenant";
    o.DefaultScopeExpression = (contextUser, eligibles) =>   // <-- IContextUserProvider
    {
        var user = contextUser.User;                          // statt httpContext.User
        var services = contextUser.Services;                  // statt httpContext.RequestServices
        // …
        return eligibles.FirstOrDefault()?.ScopeName;
    };
});
```

Sonst ändert sich an der MVC-Seite **nichts** — `CookiePermissionScope` verhält sich identisch (das
Cookie bleibt der Speicher, der Request-Lebenszyklus bleibt gleich). Intern wurde die Auflösungs-Engine
in eine geteilte `ResolvingPermissionScope`-Basis gezogen; das ist für Konsumenten transparent.

### 5b. Blazor: Per-Tab-Tenant ohne Cookie

Für Blazor gibt es eine neue, **circuit-scoped** Strategie `ScopedPermissionScope` (In-Memory-Token +
Route-Tenant). Weil sie scoped registriert ist, lebt eine Instanz pro Circuit == pro Browser-Tab →
**verschiedene Tabs tragen gleichzeitig verschiedene Tenants**, ohne browserweites Cookie. Der Tenant
kommt aus Route/Query und wird serverseitig gegen die Eligible-Scopes geprüft (gleiches Isolations-Gate
wie beim Cookie).

**Verdrahtung im Blazor-Host:**

```csharp
// 1) Ambient-User für Blazor (circuit-scoped, seedet den Principal):
services.AddBlazorContextUser();

// 2) Per-Tab-PermissionScope:
services.AddBlazorPermissionScope(o =>
{
    o.RouteOverrideParam   = "tenant";   // Query/Route-Wert, der den Tenant trägt
    o.DefaultScopeExpression = (contextUser, eligibles) => eligibles.FirstOrDefault()?.ScopeName;
    o.RenewalMinutes       = 30;          // optional, default 30
});

// 3) Shared-Services (Diagnostics etc.):
services.AddWebCoreToolkitServiceShared();
```

**Zwei unsichtbare Wurzel-Komponenten platzieren** (jeweils genau einmal, z.B. im `MainLayout` oder direkt
in `App.razor`):

```razor
<ContextUserInitializer />
<TenantUrlGuard />
```

- `<ContextUserInitializer />` seedet den synchronen `User`-Getter nach dem ersten interaktiven Render
  und hält ihn über `AuthenticationStateChanged` aktuell.
- `<TenantUrlGuard />` hält die Tenant-Auswahl URL-seitig haftend: er merkt sich pro Circuit den zuletzt
  aus `?tenant=` gelesenen Wert und reinjiziert ihn in jede interne Navigation, die ohne den Parameter
  startet. Auth-Pfade (`/Account/`, `/Identity/Account/`, `/Logout`, `/Login`, `/signin-*`, `/signout-*`)
  bleiben unangetastet; weitere Hosts-spezifische Ausnahmen können über
  `ScopedPermissionScopeOptions.AuthPathExclusions` ergänzt werden.

**Folge:** ein F5 auf einer beliebigen Page (auch Profile-Pages mit Full-Reload) findet den Tenant noch
in der Adresszeile und löst korrekt auf; ein Click auf einen normalen `NavLink` verliert ihn nicht mehr.

**Tenant-Wechsel innerhalb eines Tabs (v1):** Der Tenant-Picker navigiert mit **`forceLoad: true`** auf
die tenant-tragende URL (z.B. `?tenant=Kunde42`). Dadurch wird die Circuit neu aufgebaut → frische
Auflösung, sauberer Permission-Stack.

```csharp
// im Tenant-Picker:
navigationManager.NavigateTo($"{basePath}?tenant={Uri.EscapeDataString(selectedTenant)}", forceLoad: true);
```

> Hinweis: Ein **nahtloser** In-Tab-Wechsel (ohne Reload) ist in v1 bewusst nicht enthalten — er würde
> einen Eingriff in den circuit-langlebigen SecurityRepository-Push-Stack erfordern und ist als separater,
> zur Laufzeit zu verifizierender Schritt vorgesehen. Verschiedene **Tabs** sind in jedem Fall sauber
> isoliert.

### Identity-Pages: static-SSR / Render-Mode-Grenze (Enhanced-Nav)

Die Identity-Pages (`/Account/...`) tragen `[ExcludeFromInteractiveRouting]` und rendern als **static
SSR ohne Circuit**. Liegt darum herum ein interaktives Shell-Nav (Root-Component aus dem `MainLayout`/
`OuterLayout`), dann versucht **Enhanced-Navigation** zwischen zwei Identity-Seiten die interaktiven
Root-Components per Circuit (`updateRootComponents`) zu reconcilen → SignalR-`send` schlägt fehl, sichtbar
als JS-Exception aus `blazor.web.js` (`updateRootComponents`/`refreshRootComponents`).

**Library-seitig teil-abgedeckt:** Das `ManageLayout` der IdentityPages-Lib kapselt seinen Inhalt
(inkl. `ManageNavMenu`) in `<div data-enhance-nav="false">`. Navigation *innerhalb* des Manage-Bereichs
(z.B. Profil → E-Mail) macht damit einen Full-Page-Load statt Enhanced-Nav. Das deckt aber **nur die
library-eigenen Manage-Links** ab — nicht das **Host-Shell-/Template-Menü**, das aus eurem `MainLayout`
kommt und außerhalb dieses `<div>` liegt.

**Pflicht für die volle Lösung (Host-seitig, in MLM):** Solange euer Shell-Nav als interaktive
Root-Component über den static-SSR-Identity-Seiten liegt, ist es dort **tot** (Menü erscheint, reagiert
aber nicht) und ein Klick auf einen Eintrag (z.B. die Template-Sample-Views) wirft genau die
`updateRootComponents`-Exception. Lösung: in der `App.razor` den Render-Mode für Identity-Pages auf
**static** setzen, sodass dort **gar keine** interaktive Root entsteht — dann ist das Menü auf Identity-
Seiten statisches HTML (Links funktionieren als normale Anchor), und der Klick landet auf einer normalen
Seite mit frischem Circuit.

Das Toolkit liefert dafür die Extension **`HttpContext.AcceptsInteractiveRouting()`** (in
`ITVComponents.WebCoreToolkit.Blazor.Extensions`, ab PRE048) — true für normale Seiten, false für die
`[ExcludeFromInteractiveRouting]`-Identity-Pages:

```razor
@* App.razor *@
@using ITVComponents.WebCoreToolkit.Blazor.Extensions
@using static Microsoft.AspNetCore.Components.Web.RenderMode

<!DOCTYPE html>
<html>
<head>
    @* … *@
    <HeadOutlet @rendermode="RenderModeForPage" />
</head>
<body>
    <Routes @rendermode="RenderModeForPage" />
    @* … *@
</body>
</html>

@code {
    [CascadingParameter] private HttpContext HttpContext { get; set; } = default!;

    private IComponentRenderMode? RenderModeForPage =>
        HttpContext.AcceptsInteractiveRouting() ? InteractiveServer : null;
}
```

> **Voraussetzung:** Das Shell-Nav darf **nicht** per *explizitem* `@rendermode="InteractiveServer"` auf
> der Komponente selbst (im `MainLayout`) erzwungen sein — ein per-Component-Render-Mode überschreibt die
> seiten-bezogene Entscheidung, und die interaktive Root taucht auf den Identity-Seiten wieder auf. Den
> Render-Mode **global über `<Routes>`/`App.razor`** steuern, nicht am Menü.

Mit diesem Host-Fix wird der library-seitige `data-enhance-nav`-Schutz redundant (schadet aber nicht).

### Optional: Pfad-Modus statt Query (`/{tenant}/...`)

Wer das MVC-Muster `/{tenant}/modul/...` in Blazor abbilden will, statt `?tenant=…` im Query zu führen,
schaltet `ScopedPermissionScopeOptions.TenantSource` auf `PathSegment`. Die gesamte Validierung
(Segment ↔ Eligible-Scopes, Default-Redirect, 404 bei Unbekannt/Ineligibel) übernimmt das Toolkit —
der Host braucht nur die folgenden drei Stellen:

**1) DI-Option setzen** (einzige Konfig-Zeile):

```csharp
services.AddBlazorPermissionScope(o =>
{
    o.RouteOverrideParam   = "tenant";
    o.TenantSource         = TenantSource.PathSegment;   // <- statt default Query
    o.DefaultScopeExpression = (contextUser, eligibles) => eligibles.FirstOrDefault()?.ScopeName;
});
```

`AddBlazorPermissionScope` registriert intern `AddHttpContextAccessor()` mit; der `<TenantBaseHref />`
ist damit ohne extra Setup einsatzbereit.

**2) Middleware in der Pipeline** (eine Zeile, NACH der Authentifizierung, VOR `MapRazorComponents`):

```csharp
app.UseAuthentication();    // ← falls dein Host sie nicht schon implizit durch
app.UseAuthorization();     //    Endpoint-Routing/[Authorize] aktiviert
app.UseTenantPathPrefix();  // ← NEU
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
```

`UseTenantPathPrefix` muss laufen, NACHDEM `HttpContext.User` populiert ist (Cookie-Schema o.ä. wurde
ausgewertet). In den meisten Blazor-Server-Setups passiert das durch explizite
`UseAuthentication`/`UseAuthorization`-Aufrufe; einige Hosts haben das Cookie-Schema implizit verdrahtet
und brauchen die Aufrufe nicht — der Middleware-Aufruf bleibt in beiden Fällen identisch. Wenn der User
zum Zeitpunkt des Middleware-Runs noch anonym ist, wird durchgereicht und `[Authorize]` löst die
Challenge aus; nach erfolgter Anmeldung trifft die nächste Request wieder die Validation.

Was die Middleware bei einem eligible Segment tut: sie strippt es aus `Request.Path` und hängt es an
`Request.PathBase` (Standard-`UsePathBase`-Muster). Endpoint-Routing matched dann gegen die flachen
`@page "/foo"`-Routes der Library, NICHT gegen `@page "/{tenant}/foo"`. Ohne diesen Strip würde jede
Page einen 404 produzieren.

`UseTenantPathPrefix` ist im Query-Modus ein No-Op — die Aufruf-Zeile kann also fest stehen bleiben,
selbst wenn `TenantSource` später wieder umgestellt wird.

**3) `<base href>` über die Toolkit-Komponente** statt statischem `<base href="/" />` in `App.razor`:

```razor
<head>
    <!-- ... -->
    <TenantBaseHref />
    <!-- ... -->
</head>
```

Liest den vom Middleware validierten Segment aus `HttpContext.Items` und gibt entweder `/{tenant}/`
oder (Query-Modus / Default-Pfad) `/` aus. Auch dieser Tag ist modus-tolerant — eine `App.razor`,
die `<TenantBaseHref />` einbindet, läuft in beiden Modi.

**Was das Toolkit damit für dich automatisch macht:**

| Request | Toolkit-Verhalten |
|---|---|
| `/` (angemeldet) | Redirect 302 → `/{first-eligible}/` |
| `/Kunde42/...` (eligible) | `PathBase=/Kunde42`, `Path=/...`, `<base href="/Kunde42/" />` |
| `/EvilCorp/...` (nicht eligible, oder existiert nicht) | **404** (kein Info-Leak) |
| `/` oder beliebig (User hat 0 eligible Scopes) | **403** |
| `/Identity/Account/Login`, `/_blazor`, `/_framework/...` | Pass-through (Auth/Blazor-Internals) |
| Anonymous | Pass-through; `[Authorize]` darunter handelt Challenge ab |

Die Skip-Liste (`AuthPathExclusions`) ist konfigurierbar — Standardwerte decken die üblichen
ASP.NET-Identity-Endpunkte ab; Hosts können eigene Callback-Pfade ergänzen.

**Tenant-Picker** (im Host):

```csharp
navigationManager.NavigateTo($"/{selected}/", forceLoad: true);
```

**Was bleibt:** `<TenantUrlGuard />` (fängt absolute Links wie `NavigateTo("/users")` ab und biegt sie
unter den aktuellen Tenant-Pfad um), `<ContextUserInitializer />` (Principal-Seed). Der
Eligibility-Gate des `ScopedPermissionScope` ist als zweite Verteidigungslinie weiterhin aktiv — das
Middleware blockiert ineligible Segmente schon vor dem Render, der Scope schützt zusätzlich für
Fälle, in denen ein Konsument am Middleware vorbei kommt (z.B. Background-Tasks).

**Was wegfällt:** Query-Parameter `?tenant=…`, eigenhändige Host-Validierung, und mögliche
Kollisionen mit modul-eigenen Query-Strings.

---

## 6. Onboarding-Flows (2a/2b/2c) — **neue EF-Tabellen + Config**

Nach der Konsolidierung kam ein integrierterer Tenant-Onboarding-Flow dazu (nur **Blazor/MudBlazor**,
**Tree-Szenario**; der Telerik-MVC-Onboarding-Wizard bleibt unangetastet). Drei Bausteine:
**2a** Direkt-Onboarding (Account + Tenant in einem Schritt, deferred über E-Mail-Bestätigung),
**2b** Tenant-Einladungen per Token-Link + vereinheitlichtes Invite-GUI, **2c** konfigurierbarer
Zwangs-/Default-Parent. Alles in `…Blazor.MudBlazor.AdminViews` (OnboardingViews) bzw.
`…EntityFramework.Onboarding`.

### 6.1 EF-Migrationen — **Pflicht, sonst läuft der Flow nicht**

Zwei **neue Entities** werden über die Onboarding-Context-Interfaces exponiert, die euer Security-DbContext
implementiert — EF nimmt sie automatisch ins Modell auf:

| Entity | aus | Interface (exponiert DbSet) | Hinweis |
|---|---|---|---|
| `PendingOnboarding` | 2a | `IOnboardingPendingContext` (beide Onboarding-Interfaces erben es) | Pre-Tenant-Onboarding-Intent, by-E-Mail |
| `TenantInvitation` | 2b | `IHierarchySecurityContextWithOnboarding` (**nur Tree**) | **unique Index** auf `Token` |

Zusätzlich hat das Enum `InvitationStatus` einen neuen Wert `Expired` bekommen — **als int gespeichert,
ans Enum-Ende angehängt → kein Schema-Change**, bestehende Werte bleiben stabil.

#### `ConfigureOnboardingModel()` im `OnModelCreating` aufrufen — **Pflicht, sonst fehlen Keys/FKs**

Euer Onboarding-Context muss das **strukturelle** Onboarding-Modell (Keys + Foreign-Keys) **unbedingt**
selbst verdrahten — und zwar **bedingungslos** in `OnModelCreating`, unabhängig davon, ob die globalen
COB-Query-Filter aktiv sind:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ConfigureOnboardingModel();   // Flat:  …EntityFramework.Onboarding.Flat.Extensions
                                                // Tree:  …EntityFramework.Onboarding.Tree.Extensions
}
```

**Warum getrennt:** Die schema-formende Konfiguration (FK `EmployeeRole→EmployeeRoleMapping` non-cascading,
`EmployeeRoleMapping→Tenant/Role`, im Tree zusätzlich `TenantInvitation.ParentTenantId`) ist bewusst aus der
Filter-Aktivierung herausgelöst, damit sie **auch zur Design-Time läuft** (`dotnet ef migrations`) — sonst
erzeugt EF eine Migration ohne diese FKs (bzw. mit falschem Cascade-Verhalten → SQL-Server-1785). Die globalen
COB-Query-Filter werden **separat** (WebPart-/Runtime-seitig) aktiviert; nur der strukturelle Teil gehört
zwingend, bedingungslos, ins `OnModelCreating`. (Spiegelt das `ConfigureBilling()`-Muster.)

> **Filter-Replacer `UserId`/`UserMail`:** Die COB-Filter lösen zur Query-Zeit `UserId`/`UserMail` auf. Diese
> liefert die Basis (`AspNetSecurityContext<T>.CurrentUserId`/`CurrentUserMail`, in deren Runtime-Ctor via
> `ConfigureExpressionProperty` registriert) — wenn euer Context von ihr ableitet, ist **keine eigene
> Deklaration nötig**. Eine vollständige, minimale Vorlage ist die interne Referenz-Klasse `fubar`
> (`…Onboarding.Flat`).

**Migration erzeugen + anwenden** (gegen euren konkreten Security-/Onboarding-Context):

```bash
dotnet ef migrations add OnboardingPendingAndInvitations \
    --context <EuerHierarchySecurityContext> --project <EuerMigrationsProjekt>
dotnet ef database update --context <EuerHierarchySecurityContext>
```

> **Gegencheck:** Die generierte Migration muss `CreateTable("PendingOnboarding")` **und**
> `CreateTable("TenantInvitation")` (inkl. `CreateIndex` auf `Token`, `IsUnique: true`) enthalten. Fehlt
> `TenantInvitation`, implementiert euer Context die **Tree**-Variante (`IHierarchySecurityContextWithOnboarding`)
> nicht — im Flat-Szenario gibt es keine Tenant-Einladungen (by design).

(Beide Tabellen sind für MLM neu. Falls ihr für `PendingOnboarding` aus 2a bereits separat migriert habt,
bleibt jetzt nur `TenantInvitation` als Delta.)

### 6.2 Config (2c) — `TenantSetupOptions`

Der Onboarding-Flow liest die DB-gestützte GlobalSetting **`TenantSetup`** (Tabelle `GlobalSetting`,
key/JSON-value) via `IGlobalSettings<TenantSetupOptions>`. **Neu in 2c**:

| Feld | Typ | Default | Wirkung |
|---|---|---|---|
| `AllowRootTenantCreation` | bool | `true` | `false` → es entstehen keine neuen Root-Tenants; Onboarding ohne auflösbaren Parent wird **abgelehnt** |
| `DefaultParentTenant` | string (TenantName) | – | Wenn gesetzt: Auto-/Zwangs-Parent, wenn weder Einladung noch freie Auswahl greift |

(unverändert vorhanden: `BasicTenantTemplate`, `AdminUserRole`, `SubscriptionAssetKey`)

Beispiel-`GlobalSetting`-Wert (`SettingName = "TenantSetup"`):

```json
{ "BasicTenantTemplate": "Standard", "AdminUserRole": "TenantAdmin",
  "AllowRootTenantCreation": false, "DefaultParentTenant": "RootKunde" }
```

Parent-Präzedenz im Tree-Handler: **Einladungs-Token > freie Auswahl > `DefaultParentTenant`**. Bei
`AllowRootTenantCreation:false` **und** keinem auflösbaren Parent wird abgelehnt. **Keine Aktion nötig**,
wenn ihr bei den Defaults bleibt (Root erlaubt, freie Parent-Auswahl).

### 6.3 DI + Mail

- `AddMudBlazorHierarchyOnboardingViews<TContext>()` registriert jetzt **zusätzlich** den
  `ITenantInvitationHandler` (Backend für Erstellen/Auflisten/Widerrufen + Token-Annahme) — **kein extra
  Aufruf nötig**. Die Flat-Variante (`AddMudBlazorOnboardingViews<TContext>()`) bekommt ihn bewusst nicht.
- **Mail:** Die Einladungs-Mail läuft über die neue Core-Abstraktion **`IAppMailSender`**. Sie wird von
  IdentityShared automatisch registriert, wenn `UseDefaultMailSender` aktiv ist (derselbe Schalter, der schon
  `IEmailSender`/Bestätigungsmail verdrahtet) — dann **keine Aktion**. Wer einen eigenen Mailversand fährt,
  registriert eine eigene `IAppMailSender`-Implementierung. `…AdminViews` zieht dafür **kein** Identity.UI.

### 6.4 Neue Routen + Navigation

Die Pages leuchten über das Routing-Assembly automatisch auf — der Host muss nur ggf. Nav-Links setzen:

| Route | Auth | Zweck | Nav nötig? |
|---|---|---|---|
| `/Account/Onboarding/Start` | anonym | Direkt-Onboarding (Account+Tenant), deferred (2a) | optional (z.B. von Login) |
| `/Account/Onboarding/Invitation/{token}` | anonym | Einladungs-Annahme (2b) | nein (kommt per Mail-Link) |
| `/Account/Onboarding/CreateTenant` | `[Authorize]` | Tenant anlegen (bestehend; akzeptiert jetzt `?invitation=`) | bestehend |
| `/Account/Onboarding/MyTenants` | `[Authorize]` | eigene Tenants + Einladungen annehmen (bestehend) | bestehend |

Der deferred 2a-Abschluss passiert idempotent beim ersten Login-Landing (`MyTenants`) bzw.
E-Mail-Confirm — Voraussetzung ist nur, dass eure `/Account/ConfirmEmail`-Page erreichbar ist (Standard).

### 6.5 Tenant-Admin-View konsolidiert (`/Onboarding/BillingProfile`) — **Routen + Permissions geändert**

Die drei getrennten Admin-Seiten **`/Onboarding/BillingProfiles`** (Liste), **`/Onboarding/EmployeeRoleMappings`**
und **`/Account/Onboarding/Invitations`** wurden zu **einer** tab-basierten Detail-View
**`/Onboarding/BillingProfile`** (Singular) zusammengeführt — für den Tenant-Admin seinen *eigenen* Tenant.
Ein Tenant hat max. 1 Rechnungsprofil, daher kein Listing mehr. Vier Tabs:

| Tab | Inhalt | Gate (View/Write, Write ⊇ View) |
|---|---|---|
| Rechnungsprofil | Profil/Adresse (leer = impliziter Create) | `Onboarding.Admin.BillingProfile.View` / `.Write` |
| Benutzer & Einladungen | Mitarbeiter + Employee-Einladungen, aufklappbar → Rollen-Zuweisung | `Onboarding.Admin.Employees.View` / `.Write` |
| Rollendefinitionen | EmployeeRoleMappings, DirectRole aufklappbar → PermissionSets | `Onboarding.Admin.RoleMappings.View` / `.Write` + Kind-Gate `.DirectRole` / `.PermissionSet` |
| Sub-Tenant-Einladungen (nur Tree) | Kind-Tenant-Einladungen | `Onboarding.Admin.SubTenants.View` / `.Write` |

**Nav umbiegen (Pflicht):** Ersetzt eure Menü-/Nav-Links auf die drei alten Routen durch **`/Onboarding/BillingProfile`**.
Die alten Routen existieren nicht mehr (404).

**Neue Permissions seeden (Pflicht), alte entfernen:** In einer Migration (`migrationBuilder.InsertData("Permissions", …)`):

```
Onboarding.Admin.BillingProfile.View      Onboarding.Admin.BillingProfile.Write
Onboarding.Admin.Employees.View           Onboarding.Admin.Employees.Write
Onboarding.Admin.RoleMappings.View        Onboarding.Admin.RoleMappings.Write
Onboarding.Admin.RoleMappings.DirectRole  Onboarding.Admin.RoleMappings.PermissionSet
Onboarding.Admin.SubTenants.View          Onboarding.Admin.SubTenants.Write
```

> `.Write` impliziert Lese-Zugriff (Tab sichtbar bei View **oder** Write). Bei Rollendefinitionen heißt **anlegen/
> bearbeiten je Kind**: man braucht `.Write` *oder* das passende Kind-Recht (`.DirectRole` / `.PermissionSet`) —
> so lässt sich ein Tenant-Admin auf nur eine Art beschränken. Das Gate wird serverseitig im Handler **und** im
> UI (SecureView) erzwungen.

**Entfallen:** die alten Permissions **`ManageEmployees`** und **`Invitations.ViewSub` / `.CreateSub` / `.ViewEmp` /
`.CreateEmp`** werden nicht mehr ausgewertet — sie können aus euren Rollen/Seeds entfernt werden (Aufräumen optional,
schaden tun sie nicht). Feature-Gate bleibt `ITVAdminViews`.

---

## 7. EntityWriteTracker — FK-Label-Cache-Invalidierung (opt-in)

Die Blazor-AdminViews cachen ForeignKey-**Labels** (die Klartext-Anzeige zu FK-IDs in Grids/Dropdowns)
pro Circuit/Request. Bisher war die Invalidierung an einen einzelnen, global registrierten Tracker
gebunden, der nie automatisch befüllt wurde — die Labels wurden faktisch nur über die TTL frisch.

Neu ist eine generische, **pro-DbContext** arbeitende Infrastruktur in `ITVComponents.EFRepo`
(`IEntityWriteTracker<TContext>`) plus ein EF-`SaveChanges`-Interceptor
(`EntityWriteTrackerInterceptor`), der bei **jedem** `SaveChanges`/`SaveChangesAsync` die geschriebenen
Tabellen markiert. Der FK-Label-Cache holt sich den Tracker jetzt **pro Connection**
(`IServiceProvider.TrackerForContext(...)`) und invalidiert betroffene Tabellen sofort statt erst nach TTL.

**Aktivierung — ein Schalter im TenantSecurity-WebPart** (`ActivationSettings`-Config):

```json
{ "ActivationSettings": { "UseEntityTracker": true } }
```

Das bewirkt zweierlei (beides nur, wenn `true`):
- Registrierung des Singletons `IEntityWriteTracker<>` → `EntityWriteTracker<>` (pro konkretem Context-Typ),
- Einhängen des `EntityWriteTrackerInterceptor` in den DbContext (`ConfigureDbInterceptors`).

| Punkt | Verhalten |
|---|---|
| Default (`UseEntityTracker` weggelassen / `false`) | **kein** Verhaltenswechsel — FK-Labels bleiben TTL-basiert frisch |
| `true` | sofortige Cache-Invalidierung der geschriebenen Tabellen, zusätzlich zur TTL-Obergrenze |

**Blazor-seitig keine Aktion:** `AddToolkitForeignKeyCache()` (vom Blazor.MudBlazor-WebPart ohnehin
aufgerufen) registriert den alten `IForeignKeyWriteTracker` **nicht mehr** — der Typ ist entfallen.
Wer ihn direkt referenziert hat (unüblich, war `internal`), wechselt auf
`ITVComponents.EFRepo.Helpers.IEntityWriteTracker`.

> **Hinweis:** Die Invalidierung greift nur für Contexts, für die der Tracker registriert ist (also bei
> `UseEntityTracker:true`). Für Contexts ohne Tracker fällt der FK-Cache stillschweigend auf reines
> TTL-Verhalten zurück — kein Fehler, nur etwas „langsamere" Frische.

**Auch die FK-Auswahl-Komponenten** (`ForeignKeySelect`, `ForeignKeyAutocomplete` mit `ServerFilter=false`)
laden bei aktivem Tracker ihre **Options-Liste** neu, sobald die hinterlegte Tabelle geschrieben wurde —
z.B. ein neuer Authentication-Type, der in einem anderen Fenster erfasst wird, erscheint im offenen Dialog,
ohne ihn neu zu öffnen. (Voraussetzung: der `Table`-Parameter entspricht dem DB-Tabellennamen, was bei den
Toolkit-FK-Quellen der Fall ist. `ServerFilter=true` fragt ohnehin pro Tastendruck live ab.)

### 7a. Permissions & Navigation ziehen sofort (gleicher Schalter)

> **Gilt ab `5.0.0-PRE059`.** (Die FK-Label-Cache-Invalidierung aus Abschnitt 7 ist bereits in `PRE058`
> enthalten; die hier beschriebene Permission-/Navigations-Invalidierung kam danach dazu.)

Derselbe `UseEntityTracker`-Schalter speist jetzt zusätzlich eine **host-neutrale Invalidierung** für die
beiden Puffer, die im Blazor-Betrieb dafür sorgten, dass **gewährte/entzogene Rechte und Menü-Änderungen
nicht sofort zogen**:

- **Cookie-Permission-Cache** (`UserScope`, 30-Min-TTL): wird zusätzlich invalidiert, sobald eine
  security-relevante Tabelle geschrieben wurde (Permissions, RolePermissions, Roles, RoleRole-Vererbung,
  UserRoles, GlobalRoles/-Permissions, GlobalToLocalRoles, TenantUsers, Tenants, FeatureActivations, Features).
- **Navigations-Menü** (`Navigator`, pro Circuit gecacht): wird neu gebaut, sobald Navigations- **oder**
  security-relevante Tabellen geschrieben wurden (Menü-Sichtbarkeit hängt an Permissions/Features).
- **`isAuthenticatedCache`** im SecurityRepository: wird bei security-relevanten Writes geleert.

Das passiert über eine Core-Abstraktion `IEntityChangeSignal` (EF-Impl `EntityChangeSignal<TContext>`,
Singleton, wird mit `UseEntityTracker:true` automatisch registriert). **Keine Konfiguration nötig** über den
`UseEntityTracker`-Schalter hinaus; die Puffer-Konsumenten ziehen das Signal selbst (no-op ohne Tracker).

**Topics sind frei konfigurierbar — pro DbContext.** Signal und Optionen sind **generisch über den Context**:
`IEntityChangeSignal<TContext>` und `EntitySignalOptions<TContext>`. So kann eine App mit **mehreren
DbContexten** je Context unabhängig Topics definieren. Das Toolkit seedet `Security`/`Navigation` für den
Security-Context; **jeder Consumer — auch außerhalb des Toolkits — kann für einen beliebigen Context additiv
eigene Topics/Entities registrieren**:

```csharp
// Voraussetzung für einen eigenen Context: dessen Writes müssen getrackt werden
optionsBuilder.AddEntityWriteTrackerInterceptor(services);          // EFRepo-Extension, im OnConfiguring/DbContextOptions
services.AddSingleton(typeof(IEntityWriteTracker<>), typeof(EntityWriteTracker<>));   // falls nicht schon offen registriert

services.Configure<EntitySignalOptions<MyContext>>(o => o.Add("MyTopic", typeof(MyEntity), typeof(OtherEntity)));
// danach: IEntityChangeSignal<MyContext>.GetLastChange("MyTopic") / .Changed-Event
```

Die **nicht-generische** `IEntityChangeSignal` bleibt als Alias auf den **Security-Context** registriert — die
context-agnostischen Verbraucher (Navigation, Permission-Scope, `EntityChangeRefresher`) nutzen sie unverändert.
Das Matching ist zuweisungsbasiert: ein registrierter Typ deckt eine Entity, wenn sie ihm gleicht, von ihm
erbt/ihn implementiert oder — bei einer offenen Generic-Definition (`typeof(Role<>)`) — ihn in der
Basis-/Interface-Kette trägt. Es lassen sich also Basistypen **und** konkrete Entity-Typen registrieren.

**Re-Select beim nächsten Zugriff ist automatisch.** Damit ein *bereits gerendertes* Nav-Menü (oder eine
Seite) sich **ohne** Nutzerinteraktion sofort aktualisiert, gibt es die opt-in-Komponente
**`EntityChangeRefresher`** (in `ITVComponents.WebCoreToolkit.Blazor`, also Telerik- **und** MudBlazor-tauglich).
Im Host das Menü (bzw. den zu aktualisierenden Bereich) im **interaktiven** Render-Mode umschließen:

```razor
@using ITVComponents.WebCoreToolkit.Blazor.SharedComponents
@using ITVComponents.WebCoreToolkit.Caching

<EntityChangeRefresher Watch="@EntityChangeTopics.Navigation">
    @* euer NavMenu / die berechtigungsabhängige UI *@
</EntityChangeRefresher>
```

`EntityChangeRefresher` abonniert das Singleton-Signal, re-rendert den Inhalt bei relevanter Änderung und
ruft vorab `IPermissionScope.Refresh()` (re-resolved Scope → frische Permissions/Features), sodass das
Re-Render bereits gegen die neuen Rechte prüft. Parameter: `Watch` (Default `Navigation`),
`RefreshPermissionScope` (Default `true`), `OnChanged` (EventCallback). Ohne aktiven Tracker ist die
Komponente ein transparenter Pass-Through. Sie hängt am non-generischen Signal (= **Security-Context**).

Für einen **anderen DbContext** gibt es die per-Context-Variante **`ContextEntityChangeRefresher<TContext>`**
(gleiche Parameter), die `IEntityChangeSignal<TContext>` abonniert:

```razor
<ContextEntityChangeRefresher TContext="MyOtherContext" Watch="Pricing" RefreshPermissionScope="false">
    @* UI, die auf Writes in MyOtherContext reagieren soll *@
</ContextEntityChangeRefresher>
```

(Ein **distinkter Name** ist nötig, weil Razor Komponenten-Tags über den einfachen Namen auflöst und eine
gleichnamige generische Variante mit der nicht-generischen verschmelzen würde → Build-Fehler `RZ10009`.)

> **Granularität:** Das Signal ist tabellen-/global-granular (nicht pro Tenant/User) — jeder relevante Write
> invalidiert die Puffer aller Circuits. Bewusst gewählt: lieber ein Re-Select zu viel als stale Rechte.

---

## 8. Per-Operation-Contexts für Plugins & Diagnostics/ForeignKeys (IDbContextFactory) — opt-in

**Hintergrund.** Toolkit-seitig laufen alle Security-Services jetzt pro Operation über eine frische, kurzlebige
Context-Instanz aus einer `IDbContextFactory` (statt eines geteilten, circuit-langlebigen `DbContext`). Das ist
Blazor-sicher (kein „second operation on this context", keine stale data). Drei **konsumentenseitige** Stellen
müssen nachgezogen werden, **falls** ihr dort den System-Context als Dependency konfiguriert habt — typischerweise
unter dem Namen `"sys"`.

> **Wichtig:** Methode ist `IDbContextFactory<T>.CreateDbContext()` (nicht `GetInstance()`). Der so erzeugte
> Context ist **tenant-korrekt**, weil ihn das Toolkit an den aktuellen `IPermissionScope`/`IContextUserProvider`
> bindet — **kein** roher DI-`CreateScope()` (das würde Mandant/User verlieren).

### 8.1 Plugin-Dependencies (`FactoryOptions.AddDependency`)
Bisher (geteilter Context):
```csharp
options.AddDependency("sys", p => p.GetService<ApplicationDbContext>());
```
Neu (frischer per-Operation-Context, am Plugin-Scope-Ende disposed):
```csharp
options.AddDependency("sys",
    p => p.GetService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext(),
    disposeWithScope: true);
```
Konsumenten, die Plugins laden, öffnen dafür eine **Operation-Scope** und laden die Plugins daraus:
```csharp
using var scope = pluginHelper.CreateOperationScope();   // IWebPluginHelper
var plugin = scope.LoadPlugin<IMyPlugin>(uniqueName, ctor);
// ... plugin nutzen (bekommt "sys" = per-Op-Context) ...
// Dispose der Scope → Scope-Plugins UND der per-Op-Context werden disposed
```
Default (`disposeWithScope: false`) = bisheriges Verhalten: der Wert überlebt den Scope (host-/DI-owned).

### 8.2 Diagnostics-/ForeignKey-Quelle, die ein **Plugin** ist
Gibt euer `RegisterService`-Delegate eine über ein Plugin erzeugte Quelle zurück, paart sie mit ihrer Scope via
`ScopedDataSource` — das Toolkit disposed die Scope nach der Query:
```csharp
diagOptions.RegisterService("sys", (sp, name, area) =>
{
    var scope = sp.GetService<IWebPluginHelper>().CreateOperationScope();
    var src   = scope.GetPlugin<MyContextPlugin>();   // Plugin liefert DbContext/Adapter/FK-Source
    return new ScopedDataSource(src, scope);          // owner = scope
});
```

### 8.3 Diagnostics-/ForeignKey-Quelle als schlichter per-Op-Context (ohne Plugin)
Owner ist dann der Context selbst (er ist `IDisposable`):
```csharp
diagOptions.RegisterService("sys", (sp, name, area) =>
{
    var ctx = sp.GetService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
    return new ScopedDataSource(ctx, ctx);
});
```
Gebt ihr (wie bisher) eine **schlichte, scoped** Quelle zurück (kein `ScopedDataSource`), bleibt alles beim Alten
(host-/DI-owned, nichts wird vom Toolkit disposed). Die toolkit-internen Diag/FK-Konsumenten disposen die Quelle
nach Gebrauch korrekt (auch bei lazy gestreamten Ergebnissen) — ihr müsst dafür nichts tun.

### 8.4 FileHandler — automatisch per-Operation, **keine zusätzliche Verdrahtung**
Der zentrale FileHandler-Dispatch (`IFileServiceHandler.ProcessFileUpload`/`ProcessFileDownload`, von MVC- **und**
Blazor-Edge genutzt) lädt den FileHandler-Plugin jetzt aus einer `CreateOperationScope()` statt aus der
ambient-Factory. Folge: Ein FileHandler, der den System-Context als **scope-owned** Dependency bezieht (also „sys"
mit `disposeWithScope:true` gemäß §8.1), bekommt **pro Up-/Download einen frischen, tenant-korrekten per-Op-Context**,
der am Operationsende disposed wird — **ohne dass ihr im FileHandler oder am Dispatch etwas ändern müsst**. Beim
Download wird die Scope-Disposal an `FileReadResult.DeferredDisposals` gehängt, damit ein lazy gestreamter Inhalt
(z.B. `VideoTutorialFileHandler` über `db.Database.UseConnection`) erst **nach** dem Servieren des Streams disposed
wird. Habt ihr „sys" **nicht** als scope-owned konfiguriert (§8.1 nicht aktiviert), ändert sich nichts: die
Operation-Scope ist leer, der Context bleibt geteilt wie bisher.

---

## 9. Auto-Permission-Registration — Admin-Rolle sammelt Modul-Rechte automatisch (opt-in, neu in `5.0.0-PRE098`)

**Problem (Anstoß aus MLM):** Auf einer **frischen** DB registriert das Toolkit nur einen Teil der globalen
Permissions (z.B. 47 statt 64 in der gewachsenen DB) — `Navigate`, `SwitchTenant`, `Onboarding.Admin.*` u.a.
fehlen, weil sie nicht statisch geseedet werden. Folge: Navigation-Einträge ohne Permission, leere Grants für
die nicht-Admin-GlobalRoles.

**Lösung:** Statt eine statische Permission-Seed-Liste zwischen Toolkit und MLM synchron zu halten,
materialisiert das Toolkit jetzt jede **genuin von einem Authorization-Gate angeforderte** Permission selbst
(global, `TenantId == null`) und grantet sie an eine konfigurierte GlobalRole. Eine frische DB füllt ihren
Permission-Katalog damit von selbst, sobald ein Admin durch die App navigiert.

**Aktivierung — zwei Schalter im TenantSecurity-WebPart** (`ActivationSettings`-Config):

```json
{ "ActivationSettings": {
    "AutoRegisterRequestedPermissions": true,
    "AutoRegisterPermissionsGrantRole": "<Name eurer globalen Admin-Rolle>"
} }
```

- `AutoRegisterRequestedPermissions` (bool, default `false`): schaltet das Feature ein.
- `AutoRegisterPermissionsGrantRole` (string): **Name der globalen Admin-Rolle**. Jede neu angelegte — und
  jede bereits existierende, aber neu angeforderte — Permission wird dieser GlobalRole gegrantet. Leer/`null`
  = Permissions werden nur angelegt, ohne Grant.

**Warum die Admin-Rolle das richtige Ziel ist:** Die Admin-GlobalRole ist **kein Bypass** — sie ist
permission-getrieben und hat nur die geseedeten Rechte. Der Auto-Grant an sie ist genau der Sinn: sie sammelt
jede modul-angeforderte Permission automatisch ein. Wer die globale Admin-Rolle erhält (z.B. der TenantOwner
des Admin-Tenants), erbt damit lückenlos **alle** Rechte, die irgendein Modul anfordert. Ein zusätzlicher Grant
an `TenantOwner`/`DefaultUser` ist dafür **nicht** nötig.

**Verhalten / Eigenschaften:**
- Eine Permission entsteht erst, wenn **irgendein authentifizierter User** das zugehörige Gate erstmals trifft
  → der Katalog füllt sich „lazy" beim Admin-Rundgang, nicht schon beim Start. Für Vollständigkeit muss ein
  Admin einmal die jeweiligen Bereiche besuchen.
- Je Permission-Name max. **ein** DB-Versuch pro Prozess (Claim-Dedup) → Authorization-Hot-Path bleibt billig.
- Nur **echte** Gates lösen aus; reine „known-only"-Proben (z.B. Plugin-Namen-Checks) legen **keine** Permissions an.
- Greift in **Flat- und Tree/Hierarchy-Context** gleichermaßen (MLM fährt Tree).
- Der Write läuft über einen frisch geleasten per-Operation-Context (kollisionsfrei zum circuit-scoped Context)
  und hebt das EntityChangeSignal → neue Grants wirken **in der laufenden Session**.

**Sync-Pflicht (wichtig):** Sobald aktiviert, den idempotenten Startup-Block in `MLMManager.Web/Program.cs`
(„Ensure permissions … NOT auto-registered on a fresh database", `INSERT … WHERE NOT EXISTS`) **entfernen**,
sonst Doppelpflege/Divergenz. Kurzfristig kollidiert nichts (beide idempotent, computed
`PermissionNameUniqueness` verhindert Duplikate ohnehin), aber die Permission-Verantwortung soll eindeutig auf
einer Seite liegen. Die übrigen ADM-Referenzdaten (GlobalRolePermission-**Grants** für `TenantOwner`/`DefaultUser`,
Navigation etc.) bleiben im MLM-Seed — das Feature legt nur die **Permissions selbst** (+ Admin-Grant) an, nicht
die fachlichen Grants der nicht-Admin-Rollen.

---

## 10. Onboarding Tenant-Anlage — IDENTITY_INSERT-Fix + Transaktions-Härtung (`5.0.0-PRE108`)

**Symptom (Anstoß aus MLM):** Der Abschluss des Tenant-Onboardings (nach Mail-Bestätigung → Login →
`MyTenants` → Registrierung abschließen) crashte beim Anwenden des TenantTemplates mit:

```
Cannot insert explicit value for identity column in table 'Tenants' when IDENTITY_INSERT is set to OFF.
Cannot insert explicit value for identity column in table 'TenantUsers' when IDENTITY_INSERT is set to OFF.
```

Folge: der Tenant wurde halb angelegt, das `PendingOnboarding` **nicht** auf `Committed` gesetzt, und jeder
Neuversuch legte einen **weiteren** (nicht wirklich berechtigten) Tenant für dasselbe Pending an.

**Ursache (toolkit-seitig behoben, kein MLM-Code nötig):** Der `afterApply`-Callback der Template-Anwendung lief
auf einem separat geleasten Context, bekam aber die Admin-`TenantUser`- (+ deren `Tenant`-) Navigation aus dem
Handler-Context — EF wollte beide mit **expliziter PK** neu INSERTen. Fix = Skalar-FK statt Navigation.

**Zusätzlich (Transaktions-Härtung):** Der Blazor-Onboarding-Abschluss läuft jetzt **atomar** — Pending-Konsum,
Tenant/Admin/Profil-Anlage, Template-Anwendung und `pending.Committed` committen in **einer** Transaktion auf
**einer** Connection (via `IExecutionStrategy` → kompatibel mit `EnableRetryOnFailure`). Ein Fehler rollt alles
zurück: keine halben Tenants, keine Duplikate. Ergänzend gibt es eine fortsetzbare Resume-Logik über die neue
Spalte `PendingOnboarding.CreatedTenantId`.

### 10.1 EF-Migration — **Pflicht ab `5.0.0-PRE108`, sonst Runtime-Crash**

Mit dem Update auf **`5.0.0-PRE108`** hat `PendingOnboarding` eine neue, nullbare Spalte `CreatedTenantId (int?)`.
Die Spalte wird per Konvention gemappt (keine `OnModelCreating`-Änderung nötig), **aber** die Migration muss
host-seitig generiert und angewendet werden — sonst erwartet das Modell eine Spalte, die es in der DB nicht gibt:

```bash
dotnet ef migrations add PendingOnboardingCreatedTenantId   # im MLM-Host-Projekt (ApplicationDbContext)
dotnet ef database update
```

Additive, nullbare Spalte → unkritisch, keine Datenmigration.

### 10.2 Keine Config-/API-Änderung

Kein neuer Schalter, keine geänderte Signatur auf eurer Seite. Nach Paket-Bump auf `5.0.0-PRE108` + Migration
ist der Flow einfach robust. Bereits durch Fehlversuche entstandene **Alt-Orphan-Tenants** räumt das Feature
nicht rückwirkend auf (einmalig manuell bereinigen, falls noch vorhanden).

### 10.3 Scope-Hinweis

Gehärtet wurde der **Blazor**-Onboarding-Flow (`OnboardingHandler` / `HierarchyOnboardingHandler`). Der ältere
**MVC/Telerik**-Flow (`RegistrationController` / `CreateTenant.cshtml.cs`) hat nur den IDENTITY_INSERT-Crash-Fix
erhalten, **keine** Transaktions-Härtung. Wer den MVC-Pfad noch nutzt, meldet sich für eine gleichwertige Härtung.

### 10.4 Rollendefinitionen-Tab (`/Onboarding/BillingProfile`) — UX + neuer opt-in Schalter

Verbesserungen am Rollendefinitionen-Grid (EmployeeRoleMappings), **keine** Migration/Config-Pflicht:

- **Haupt-Grid zeigt nur bearbeitbare Zeilen:** DirectRole-Zeilen nur bei `Onboarding.Admin.RoleMappings.DirectRole`
  **oder** `.Write`, PermissionSet-Zeilen nur bei `.PermissionSet` **oder** `.Write`. Ein reiner
  `.View`-User (ohne Kind-Write) sieht das Haupt-Grid **leer** — bewusst so. Im aufgeklappten Sub-Grid werden die
  Permission-Sets **immer** angezeigt (nötig zum Zuweisen), unabhängig vom Schreibrecht.
- **Anzeigename bevorzugt:** Haupt-Liste + Unter-Listen zeigen den (optional mehrsprachigen JSON-)`DisplayName`
  via Toolkit-`Translate` statt des rohen Rollennamens; Fallback = Rollenname.
- **Dritter Mapping-Typ „Delegation-Rolle"** (`EmployeeRoleMappingKind.Delegation = 2`, additiver Enum-Wert,
  **keine** Migration): strukturell wie eine DirectRole (klappt auf, PermissionSets werden per RoleRole-Vererbung
  aktiviert), aber mit **zwei Berechtigungsstufen**. Zwei neue gated Permissions:
  - `Onboarding.Admin.RoleMappings.Delegation` — **Voll-Edit** (anlegen/bearbeiten/löschen/umbenennen + Sets zuweisen),
    analog zu `.DirectRole` / `.PermissionSet` (oder generisch `.Write`).
  - `Onboarding.Admin.RoleMappings.DelegationAssign` — **schwächere Stufe**: Delegation-Rolle **sehen** und
    PermissionSets an-/abwählen, aber **nicht** neu anlegen/löschen/umbenennen.

  **Seeding:** die beiden Permission-Namen anlegen/granten wie die übrigen `Onboarding.Admin.RoleMappings.*`
  (bzw. via Auto-Permission-Registration §9, falls aktiv — sie werden beim Betreten des Tabs angefordert).
- **Neuer opt-in Schalter** `TenantSetup.ForceDedicatedRoleForMappings` (bool, default `false`): auf `true`
  gesetzt, blendet der „Neues RoleMapping"-Dialog den „bestehende Rolle wählen"-Picker aus und legt **immer** eine
  neue TenantRole an; der Handler weist das Verknüpfen einer bestehenden Rolle zusätzlich ab. Verhindert, dass im
  Produktivbetrieb versehentlich eine bestehende (rechtetragende) Rolle als DirectRole umfunktioniert wird. Bei
  Namenskollision wird hochgezählt (`Name` → `Name_1` … `Name_5`); sind Basis + 5 Suffixe belegt, schlägt das
  Speichern fehl. Ein Umbenennen des Mappings ändert nur das Anzeige-Label — die zugrundeliegende SecurityRole
  behält ihren bei Erstellung vergebenen Namen (stabiler Template-Key).

```json
{ "TenantSetup": { "ForceDedicatedRoleForMappings": true } }
```

---

## 11. Verifikation auf eurer Seite

- Build der gesamten Solution grün (alle eigenen FileHandler + Cookie-Scope-Config angepasst).
- **Onboarding:** Migration angewendet (Tabellen `PendingOnboarding` + `TenantInvitation` existieren);
  Admin erstellt unter `/Account/Onboarding/Invitations` eine Sub-Tenant-Einladung → Mail/Link kommt an →
  Annahme über den Link legt einen Child-Tenant unter dem einladenden Parent an.
- **Manueller Zwei-Tab-Check (Blazor):** zwei Tabs mit unterschiedlichem `?tenant=…` öffnen, in jedem
  eine tenant-spezifische Liste laden → jeder Tab zeigt ausschließlich seine Tenant-Daten, kein Überlauf.
- **Negativ-Check:** `?tenant=` mit einem Tenant, für den der User **nicht** berechtigt ist → es darf der
  Default-Tenant greifen, niemals der eingeschleuste. (Das Gate ist toolkit-seitig durch Unit-Tests
  abgesichert; der Host-Check bestätigt die End-to-End-Verdrahtung über den realen SecurityRepository.)

---

## 12. Paket-Versionen anheben (`5.0.0-PRE118`)

Beim Hochziehen der `ITVComponents.*`-NuGet-Referenzen auf **`5.0.0-PRE118`** solltet ihr die folgenden Framework-
und Fremdpaket-Versionen **mitziehen** — das Toolkit ist gegen diese gebaut (sonst Versions-Divergenz / NU-Warnungen):

| Paket(gruppe) | Version |
|---|---|
| `Microsoft.EntityFrameworkCore*`, `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*`, `System.*` (Runtime), `Microsoft.AspNetCore.Identity.*` | **`10.0.9`** |
| `Npgsql.EntityFrameworkCore.PostgreSQL` (nur PostgreSQL-Hosts) | **`10.0.2`** |
| `MudBlazor` | **`9.6.0`** |
| `BlazorMonaco` | **`3.5.0`** |
| `Scriban` | **`7.2.5`** |
| `Microsoft.OpenApi` | **`3.8.0`** |
| `Microsoft.IdentityModel.Tokens.Saml` (nur SAML) | **`8.19.1`** |
| `Azure.Storage.Blobs` (nur Azure-Blob) | **`12.29.1`** |
| `Google.Protobuf` / `Grpc.Tools` (nur gRPC/IPC) | **`3.35.1`** / **`2.82.0`** |
| `PrettyPrompt` / `System.Management.Automation` (nur PowerShell) | **`6.0.4`** / **`7.6.3`** |
| `Extended.Wpf.Toolkit` (nur WPF) | **`5.1.2`** |
| `Stripe.net` (nur Billing) | **`52.1.0`** ⚠️ Major-Sprung 51→52 — prüft eure Stripe-API-Nutzung |

**Bewusst NICHT angehoben** (übernehmt das ebenfalls **nicht**):
- **`Microsoft.CodeAnalysis.*` bleibt `5.0.0`.** `5.6.0` ist inkompatibel mit `Microsoft.EntityFrameworkCore.Design 10.0.9`, das transitiv `CodeAnalysis.CSharp.Workspaces 5.0.0` zieht und `Common` auf exakt `5.0.0` pinnt → `NU1107` in Projekten, die Scripting **und** EFCore.Design kombinieren.
- **Test-Stack** (`Microsoft.NET.Test.Sdk` 17, `MSTest.*` 3) unverändert (Major-Sprung, nicht getestet).

MudBlazor `9.4 → 9.6` ist nur ein Minor — prüft eure eigenen MudBlazor-Verwendungen kurz auf Deprecation-Warnungen.

## 13. `ItvErrorBoundary` — Circuit vor Komponenten-Fehlern schützen (opt-in, empfohlen)

Unter **Blazor Server** reißt **jede unbehandelte Exception den ganzen SignalR-Circuit ab** (→ Reconnect-Overlay bzw.
Ctrl+F5). Neu im Toolkit: die wiederverwendbare Komponente **`ItvErrorBoundary`** (Namespace
`ITVComponents.WebCoreToolkit.Blazor.SharedComponents`, in `Blazor.MudBlazor`). Sie fängt Render-/Lifecycle-**und
Event-Handler**-Fehler (also auch fehlgeschlagene Saves) im umschlossenen Bereich ab und **hält den Circuit am Leben** —
ohne dass ihr überall try/catch braucht.

**Einbau (ein Ort, schützt alle Toolkit-Seiten):** im Host-Layout `@Body` umschließen:
```razor
@using ITVComponents.WebCoreToolkit.Blazor.SharedComponents
<ItvErrorBoundary>
    @Body
</ItvErrorBoundary>
```
Optional die Meldung/den Retry überschreiben (beide Slots bekommen `ItvErrorContext` = `Exception` + `Recover`):
```razor
<ItvErrorBoundary>
    <Actions Context="err"><MudButton OnClick="@(() => err.Recover())">Erneut versuchen</MudButton></Actions>
    <ChildContent>@Body</ChildContent>
</ItvErrorBoundary>
```
`Notification` = ganze Meldung selbst gestalten, `Actions` = nur der Recover-Bereich. Weitere Parameter: `Title`,
`RecoverText`, `Severity`, `ShowDetails` (Default false), `RecoverOnNavigation` (Default true → alter Fehler wird beim
Navigieren automatisch zurückgesetzt), geerbtes `MaximumErrorCount`. Zusätzlich empfehlenswert: das Blazor-**Reconnect-
Overlay** im Host konfigurieren, damit ein tatsächlich abgerissener Circuit automatisch neu verbindet.
**Grenze:** echte fire-and-forget-async-Exceptions (nicht awaitet) entkommen jeder Boundary — die müssen an der
Startstelle behandelt werden.

## 14. PermissionSet-Aktivierung propagiert jetzt cross-tenant — **Pflicht-Migration (`ConfigureViews`)**

Die Rollen-/Tenant-Baum-Auflösung propagiert jetzt auch die **cross-tenant Reichweite** einer per `PermissionSet`
aktivierten Rolle (intra-tenant Closure der effektiven Rollen). Dafür wurden die generierten SQL-Objekte geändert
(neue Inline-TVF `GetEffectiveTenantUserRoles(@tenantUserId)`; die Rollen-Tree-Prozeduren/-Funktionen ankern auf die
effektive statt nur die direkt zugewiesene Rollenmenge).

- **Pflicht:** eine **neue Konsumenten-Migration**, die `SqlColumnsSyntaxHelper.ConfigureViews(migrationBuilder)` erneut
  ausführt (idempotent: `DROP … if exists` + `CREATE`), damit die neue TVF **und** die regenerierten Prozeduren
  deployt werden. Ohne diese Migration bleiben die alten Prozeduren aktiv und die Downline-Propagation greift nicht.
- Der LINQ-Zwilling (`DbSecurityRepository.GetRootTenants`/`GetChildTenants`, Tenant-Switcher) braucht **keine**
  Migration — er ist im Toolkit angepasst.
- Schema **unverändert** (keine EF-`migrations add`-Tabellenänderung nötig, nur die View/Proc-`Sql()`-Ausführung).

Details/Repro: `docs/ISSUE-MLM-PermissionSet-CrossTenant-Propagation.md`.

## 15. Tenant-Template: `BasicTenantType`, Apply-Modes, Re-apply (opt-in, kein Schema-Change)

- **`TenantSetupOptions.BasicTenantType`** (neu, bevorzugt): Onboarding wählt das Template über den **TenantType**
  (`TenantType.TenantTemplate`) statt über den Template-Namen. Vorrang: Invitation-Override > `BasicTenantType` >
  `BasicTenantTemplate` (legacy, bleibt als Fallback). Der onboardete Tenant wird mit dem `TenantTypeId` getaggt.
  Aktion: optional die `TenantSetup`-GlobalSetting um `BasicTenantType` erweitern (sonst greift weiter `BasicTenantTemplate`).
- **Apply-Modes** (`Auto`/`Additive`/`Forced`) — pro Bereich am Template (`ApplyModeForRoles`, `…ForPlugIns`, …) und/oder
  als Methoden-Default. `Auto` (Default) erbt: an der Methode → `Forced`, am Template → aufgelöster Methoden-Wert.
  **Bestandsschutz:** ohne gesetzte Modi = `Forced` = bisheriges (löschendes Sync-)Verhalten, kein Bruch.
  `Additive` = reines Upsert (nichts löschen).
- **`TenantTemplateMarkup.Extensions`**: Modell-Änderung (`Dictionary<string,string>` → `Dictionary<string,
  TemplateExtensionMarkup>` mit `Payload`+`ApplyMode`). ⚠️ **Payload-Form seit §18 auf typisiert-polymorph umgestellt
  (Clean Cut)** — siehe §18; die frühere string-Back-Compat entfällt.
- **Template erneut anwenden:** Extension `services.ApplyTenantTypeTemplate(dbContext, tenant, mode = Additive)` (lädt das
  Template des TenantTyps und wendet es auf den Tenant an) — bzw. im Blazor-Tenants-Grid der neue Re-apply-Button (nur
  sichtbar, wenn dem Tenant ein TenantType **mit** Template zugewiesen ist; Permission `TenantTemplates.Write`).

Die neuen Settings-Editoren (GlobalSettings/TenantSettings mit CodeEditor + JSON/Plaintext-Switch) sind automatisch —
keine Host-Aktion, nur die Versionsanhebung (§12).

## 16. Billing: Add-ons sind jetzt plan-gebunden (n:m) — **Pflicht-Migration + `IBillingContext`-Anpassung**

Add-ons waren bisher **plan-unabhängig** (globale Preise + eigenes `BillingInterval` je Add-on). Neu: ein Add-on ist
nur noch **Identität** (Name/Beschreibung/Features), und seine **Buchbarkeit + Preis hängen an einer n:m-Verknüpfung
`PlanAddOn`**. Das Abrechnungs-Interval eines Add-ons wird jetzt **vom Plan geerbt** (ein Stripe-Abo ist single-interval)
— dadurch kann dasselbe Add-on unter einem Monats- **und** einem Jahresplan zubuchbar sein, mit je eigenem Preis.

**Schema-Änderungen (EF.Billing):**
- **NEU** `PlanAddOn` (`PlanAddOnId`, `PlanId`, `AddOnId`, unique `(PlanId, AddOnId)`) — die Buchbarkeits-Verknüpfung.
- **NEU** `PlanAddOnPrice` (`PlanAddOnPriceId`, `PlanAddOnId`, `Currency`, `Amount`, `ProviderPriceId`, unique
  `(PlanAddOnId, Currency)`) — **ersetzt** `AddOnPrice`.
- **ENTFERNT**: Tabelle `AddOnPrices`; Spalte `AddOn.BillingInterval`.
- `AddOn` behält `ProviderProductId` (Stripe-**Product** = Identität, eins je Add-on); die Stripe-**Prices** hängen
  jetzt an den `PlanAddOnPrice`-Zeilen und tragen das Plan-Interval.

**Pflicht auf eurer Seite:**
1. **`IBillingContext`-Vertrag geändert** — ersetzt im App-Context den DbSet `AddOnPrices` durch **`PlanAddOns`** +
   **`PlanAddOnPrices`** (sonst Compile-Break). Die 8 DbSets sind jetzt: `Plans`, `PlanPrices`, `PlanFeatures`,
   `AddOns`, `PlanAddOns`, `PlanAddOnPrices`, `AddOnFeatures`, `TenantSubscriptions`, `TenantSubscriptionItems`
   (+ euer `BillingFeatureGrant`). `ConfigureBilling()`-Aufruf bleibt unverändert (mappt die neuen Entities mit).
2. **Neue EF-Migration** (`dotnet ef migrations add BillingAddOnPlanLink` → `database update`): dropt `AddOnPrices` +
   `AddOn.BillingInterval`, legt `PlanAddOns` + `PlanAddOnPrices` an. Da Billing bei euch noch nie mit echten
   Produktivdaten lief, ist das ein sauberer Schnitt (alte Test-Add-on-Preise gehen verloren).
3. **Ersetzt das früher notierte Multi-Currency-Delta für Add-ons** (`AddOnPrices` existiert nicht mehr) — es gibt nur
   noch `PlanAddOnPrice`.

**Verhalten/UI (automatisch, keine Host-Aktion):** Add-ons werden jetzt **im Plan-Editor** (`/Billing/Plans`) gepflegt
— pro Plan ankreuzen, welche Add-ons buchbar sind, und je Add-on den Preis pro Währung setzen. Der Add-on-Editor
(`/Billing/AddOns`) pflegt nur noch Name/Beschreibung/Features. Der Checkout bietet dem Kunden je Plan nur die dafür
freigegebenen Add-ons an und lehnt planfremde Add-ons serverseitig ab. Der Synchronizer pusht Add-on-Prices mit dem
Interval des Plans; der Webhook mappt Stripe-Price → `PlanAddOnPrice` → Add-on.

## 17. System-Konfigurations-Export ist erweiterbar + Billing-Sektion (opt-in, kein Schema-Change)

Der herunterladbare System-Config (`Util/AssemblyDiagnostics` → „DownloadConfig", `sysCfg`) hatte eine **fest
verdrahtete** Sektionsliste (Permissions, Rollen, PlugIns, Navigation, TenantTemplates, …). Neu: **Feature-Libraries
können eigene Sektionen beisteuern** — ohne den Kern anzufassen. Dadurch exportiert/diff't der Config jetzt auch den
**Billing-Katalog** (Pläne, Add-ons, Verknüpfungen, Preise, Feature-Keys).

**Mechanik (für eigene Erweiterungen):**
- Neu in `ITVComponents.EFRepo.DataSync`: `IConfigExtension` (`Describe` = IST-Sektion als typisiertes Markup,
  `Compare` = liefert `Change[]`), polymorpher Basistyp `ConfigExtensionMarkup`, Attribut `[SystemConfigHandler(key,
  handlerType)]`, `ConfigExtensionOptions`, `services.AddSystemConfigExtension<TMarkup>()`.
- Der Markup-Typ wird per **System.Text.Json-Polymorphie** (`JsonHelper.ExtendNativeProtocolType`) registriert →
  die Sektion steht **typisiert** im Config-JSON (kein opaker Payload-String). Unbekannte Sektionen (Lib nicht
  installiert) werden beim Import **übersprungen** (`FallBackToBaseType`), nicht als Fehler.
- Apply ist geschenkt: die `Change`-Objekte laufen durch den bestehenden `SimpleDataApplyer` — kein eigener Apply-Code.

**Pflicht auf eurer Seite, damit die Billing-Sektion greift (rein Wiring, KEINE Migration):**
1. `services.AddBillingConfigExtension();` beim Startup (aus `ITVComponents.WebCoreToolkit.EntityFramework.Billing.Configuration`).
2. Euer Security-/Config-Handler-Context muss `IBillingContext` implementieren (tut er bereits fürs Billing).
3. **Das Config-Handler-Plugin muss den `IServiceProvider` in seinen Konstruktor gereicht bekommen** (die
   `SysConfigurationHandler`-Ableitungen nehmen ihn jetzt als optionalen 2. Ctor-Parameter). Fehlt er, bleibt die
   Erweiterung still wirkungslos (kein Crash), aber der Billing-Teil taucht dann nicht im Export auf.

**Bewusst NICHT im Export:** Stripe-Provider-IDs (umgebungsspezifisch → via „Push to Stripe" pro Umgebung neu) und
`TenantSubscription`s (Laufzeit-Zustand, kein Katalog).

**Beim ersten Host-Test besonders prüfen:** die Add-on-Preis-Sektion (`PlanAddOnPrice`) löst ihren Eltern-Datensatz
über `Plan.Name` + `AddOn.Name` auf (der einzige Navigations-basierte FK-Lookup) — Import einer Config mit Add-on-Preisen
gezielt gegentesten.

## 18. TenantTemplate-Extensions: typisierter polymorpher Payload statt opaquem String — **Clean Cut**

Die decoupled Template-Parts (`TenantTemplateMarkup.Extensions`, Mechanik aus §15) speicherten ihren Payload bisher als
**opaquen String** (jeder Part-Handler serialisierte sein Sub-DTO selbst in einen String → doppelt-escapetes JSON im
Template). Jetzt **typisiert-polymorph** — dasselbe Muster wie der erweiterbare Config-Export (§17): das Template-JSON
wird lesbar/direkt editierbar.

**Was sich änderte (library-seitig, schon erledigt):**
- `TemplateExtensionMarkup.Payload`: `string` → polymorpher Basistyp **`TemplateExtensionPayload`**
  (`[JsonPolymorphic]`, `FallBackToBaseType`). Der Legacy-`TemplateExtensionMarkupJsonConverter` (string-Back-Compat) **entfällt**.
- `ITenantTemplatePartHandler.Extract/Apply` arbeiten mit `TemplateExtensionPayload` statt `string`.
- Der EmployeeRoleMapping-Part-Handler nutzt jetzt `EmployeeRoleMappingTemplatePayload : TemplateExtensionPayload`
  (`Mappings`-Liste); der Payload-Subtyp wird in Onboarding-`WebPartInit` einmalig via
  `JsonHelper.ExtendNativeProtocolType<TemplateExtensionPayload, EmployeeRoleMappingTemplatePayload>(PartKey)` registriert
  (`ActivateFilters`-gegated, strategie-unabhängig).

**Konsequenz für euch:** ⚠️ **Bereits in der DB gespeicherte Templates, die eine `Extensions`-Sektion enthalten
(EmployeeRoleMappings), lassen sich nach dem Update nicht mehr deserialisieren** (das alte string-Payload-Format ist
inkompatibel). Templates **ohne** Extensions sind unberührt. Da Billing/Onboarding noch Preview sind: betroffene Templates
einfach **neu extrahieren** (Template aus Tenant neu erzeugen). Kein Schema-Change, keine Migration, kein API-Aufruf nötig
— nur ggf. Templates neu ziehen.

---

## 19. Navigations-Metadata + kontextsensitiver Help-Button (`5.0.0-PRE130`) — **Pflicht-Migration**

Navigationsmenü-Einträge können jetzt ein freies **JSON-Metadata-Objekt** tragen (neue Spalte
`NavigationMenu.Metadata`). Der Navigations-Builder reicht es ins Runtime-Modell durch, und `INavigator` bekommt
eine neue Property **`SelectedNavigationItem`** (der zur aktuellen Seite passende Nav-Eintrag). Darauf baut ein
neuer **`<HelpButton />`** auf, der die Hilfe zur aktuellen Seite in einem Popup zeigt.

**⚠️ Pflicht-Migration (sonst Runtime-Crash):** Das EF-Modell enthält jetzt die Spalte `NavigationMenu.Metadata`,
und der Navigations-Builder selektiert sie. Ohne Migration schlägt **jede Navigations-Query** mit
*„Invalid column name 'Metadata'"* fehl.

```
dotnet ef migrations add NavigationMenuMetadata
dotnet ef database update
```

Die Migration fügt nur eine **nullable** Spalte `Metadata` (nvarchar(max)) an der Navigations-Tabelle hinzu —
additiv, nicht-destruktiv. Bestehende Einträge haben `Metadata = NULL` (= keine Metadaten).

**Opt-in — Help-Button pro Seite (kein Zwang):**
1. Am jeweiligen Navigations-Eintrag im Nav-Admin ein JSON-Objekt mit `HelpSlug` hinterlegen, z.B.
   `{ "HelpSlug": "orders-overview" }` (Slug eines **published** Help-Topics, §…/Hilfesystem).
2. Im Host-Layout (z.B. AppBar) die Komponente platzieren:
   `@using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews` + `<HelpButton />`.

Der Button ist **fail-silent**: kein Nav-Eintrag für die Seite / kein `HelpSlug` / kein passendes published Topic /
Help- oder Navigation-Feature nicht verdrahtet → er rendert **nichts**. Voraussetzung fürs Anzeigen sind also nur
gepflegte Metadaten + vorhandene Hilfe; sonst bleibt alles wie bisher. Der Metadata-Key ist über den Parameter
`MetadataKey` überschreibbar; `MetadataValues` (case-insensitives Dictionary) steht für eigene Zwecke bereit.

**Ebenfalls in `PRE130` (automatisch, keine Migration/Config):**
- **Maximierbare Detail-/Editor-Dialoge:** User/Tenant-Detail, RoleMapping und die CodeEditor-Dialoge
  (HelpTopic, Tenant-/Global-Setting, Tenant-Template, HealthScript, DiagnosticsQuery, DashboardWidget) haben einen
  Maximieren/Wiederherstellen-Knopf (Vollbild). Rein UI, kein Eingriff nötig.
- **Hilfe im Tenant-Kontext:** Help-Navigation, `module:`-Links und eingebettete `resource:`-Medien bleiben jetzt am
  aktuellen Tenant (`/{tenant}/help/...`); grosse Bilder/Videos skalieren auf Container-Breite.
- **Config-Export:** ein leeres `catch` in den SecurityContext-Konstruktoren protokolliert jetzt (statt Konfig-Fehler
  still zu schlucken); der polymorphe Config-Export crasht nicht mehr, wenn **keine** Config-Extension registriert ist.
  Die Billing-Sektion im System-Config-Export ist jetzt per **WebPart-Flag** aktivierbar (statt manuellem Startup-Aufruf,
  s. §17): WebPart `…EntityFramework.Billing.WebPartInit`, Option `BillingConfigExportPartOptions.ActivateBillingConfigExport = true`.

---

## 20. System-Log: „Eintrag verfolgen" + Index auf `SystemLog` — **Migration empfohlen**

Die View `/Util/SystemLog` hat pro Zeile einen neuen Augen-Button **„Trace entry"**. Er öffnet einen Dialog, der
die Einträge **vor und nach** der gewählten Nachricht zeigt (je 20 als Default, frei einstellbar bis 500 pro
Seite), damit der Ablauf um einen Fehler herum nachvollziehbar wird. Die Filter der Hauptliste gelten im Dialog
bewusst **nicht** — sonst blendet man genau die Nachbar-Einträge aus, die man sehen will. Rein additiv: kein
Interface-Break auf Konsumentenseite, keine Config, keine neue Permission (es gilt weiterhin `SystemLog.View`).

**Index (empfohlen, kein Zwang):** Das EF-Modell deklariert auf `SystemEvent` jetzt

```
[Index(nameof(EventTime), nameof(SystemEventId), IsUnique = false, Name = "IX_SystemLogEventTime")]
```

`EventTime` ist nicht eindeutig (der Log-Provider schreibt gepuffert in Batches), deshalb ist `SystemEventId` als
zweite Schlüsselspalte im Index — nur so ist der Tie-Break seekbar statt ein Sort. Der Index bedient beide
Zugriffe: die gepagte Liste (`ORDER BY EventTime DESC`) und das Kontext-Fenster (Seek auf den Anker + TOP n in
beide Richtungen). Ohne Index funktioniert alles, sortiert aber über die ganze Tabelle — bei grossem `SystemLog`
spürbar.

**⚠️ Nicht per `dotnet ef migrations add` erzeugen lassen!** Der `SecurityContextModelSnapshot` im Repo hinkt dem
Modell deutlich hinterher (OAuth-Services, GlobalRoles, ServerCookies, `Navigation.Metadata`/`IsPublic`,
`WebPlugins.Transient` …). Eine auto-generierte Migration würde diesen ganzen Drift mitschleppen. Zieht den Index
wie gewohnt manuell nach:

```sql
CREATE NONCLUSTERED INDEX IX_SystemLogEventTime
    ON dbo.SystemLog (EventTime, SystemEventId);
```

PostgreSQL:

```sql
CREATE INDEX "IX_SystemLogEventTime" ON "SystemLog" ("EventTime", "SystemEventId");
```

Bei grosser Tabelle unter Last lohnt sich SQL Server Enterprise `WITH (ONLINE = ON)`. Nachträglich hinzugefügt
ist der Index **rein additiv** — keine Daten-, keine Verhaltensänderung.

---

## 21. Zustimmungen im Onboarding (AGB/Datenschutz/Newsletter) — **Pflicht-Migration (1 Tabelle)**

Bisher gab es in der Firmendaten-Erfassung genau **einen** Schalter („Ich akzeptiere die Nutzungsbedingungen"),
der als Pflichtfeld wirkte, aber **nirgends gespeichert** wurde — es gab also keinen Nachweis, wer wann wozu
zugestimmt hat. Und im Beitritts-Flow (`/Account/Onboarding/JoinRegister`, jemand folgt einer Einladung und legt
nur ein Konto an) wurde **gar nie** zugestimmt.

Neu sind die Zustimmungspunkte **konfigurierbar** (mehrere Schalter, AGB und Datenschutz getrennt, Newsletter
optional) und der Nachweis wird **abgelegt**.

### 21.1 Neue Tabelle `ConsentRecord`

Die beiden Kontext-Interfaces (`ISecurityContextWithOnboarding`, `IHierarchySecurityContextWithOnboarding`)
erweitern jetzt zusätzlich `IOnboardingConsentContext`. **Euer Kontext braucht deshalb ein neues DbSet:**

```csharp
public DbSet<ConsentRecord> ConsentRecords { get; set; }
```

Die Entität hat bewusst **keine Fremdschlüssel** (gleiches Muster wie `PendingOnboarding`): die Zustimmung fällt
beim anonymen Start, lange bevor es einen Mandanten gibt, und sie soll den Mandanten und das Konto überleben —
ein Nachweis, der beim Löschen des Mandanten mitverschwindet, ist keiner.

**⚠️ Nicht per `dotnet ef migrations add` erzeugen lassen** (Snapshot-Drift, siehe §20). Manuell:

```sql
CREATE TABLE dbo.ConsentRecord (
    ConsentRecordId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ConsentRecord PRIMARY KEY,
    ConsentKey      nvarchar(200)  NOT NULL,
    Version         nvarchar(100)  NULL,
    Accepted        bit            NOT NULL,
    AcceptedUtc     datetime2      NOT NULL,
    UserId          nvarchar(450)  NULL,
    Email           nvarchar(256)  NULL,
    Scope           int            NOT NULL,
    TenantId        int            NULL,
    Culture         nvarchar(35)   NULL,
    HelpSlug        nvarchar(200)  NULL,
    Origin          nvarchar(100)  NULL
);
CREATE INDEX IX_ConsentRecordUser   ON dbo.ConsentRecord (UserId);
CREATE INDEX IX_ConsentRecordTenant ON dbo.ConsentRecord (TenantId);
CREATE INDEX IX_ConsentRecordKey    ON dbo.ConsentRecord (ConsentKey);
```

PostgreSQL:

```sql
CREATE TABLE "ConsentRecord" (
    "ConsentRecordId" serial PRIMARY KEY,
    "ConsentKey"  varchar(200) NOT NULL,
    "Version"     varchar(100),
    "Accepted"    boolean      NOT NULL,
    "AcceptedUtc" timestamp    NOT NULL,
    "UserId"      varchar(450),
    "Email"       varchar(256),
    "Scope"       integer      NOT NULL,
    "TenantId"    integer,
    "Culture"     varchar(35),
    "HelpSlug"    varchar(200),
    "Origin"      varchar(100)
);
CREATE INDEX "IX_ConsentRecordUser"   ON "ConsentRecord" ("UserId");
CREATE INDEX "IX_ConsentRecordTenant" ON "ConsentRecord" ("TenantId");
CREATE INDEX "IX_ConsentRecordKey"    ON "ConsentRecord" ("ConsentKey");
```

`Scope` ist `0 = User`, `1 = Tenant`, `2 = Both`.

`Accepted` hält auch die **Ablehnung** fest, nicht nur die Zustimmung: dass jemand den Newsletter ausdrücklich
nicht wollte, ist genau die Auskunft, die man später braucht — sie unterscheidet sich von „wurde nie gefragt".

### 21.2 Konfiguration (GlobalSettings `Consent`)

Ohne Konfiguration ändert sich **nichts**: es bleibt beim einen eingebauten Schalter, und es wird weiterhin
nichts abgelegt. Sobald Punkte konfiguriert sind, ersetzen sie ihn.

```json
{
  "Points": [
    {
      "Key": "tos",
      "Label": "{\"de\":\"Ich akzeptiere die {0}.\",\"fr\":\"J'accepte les {0}.\"}",
      "LinkText": "{\"de\":\"Nutzungsbedingungen\",\"fr\":\"conditions d'utilisation\"}",
      "HelpSlug": "documents-agb",
      "Version": "2026-08",
      "Scope": "Both",
      "Required": true
    },
    {
      "Key": "privacy",
      "Label": "{\"de\":\"Ich habe die {0} gelesen.\"}",
      "LinkText": "{\"de\":\"Datenschutzerklärung\"}",
      "HelpSlug": "documents-datenschutz",
      "Version": "2026-08",
      "Scope": "User",
      "Required": true
    },
    {
      "Key": "newsletter",
      "Label": "{\"de\":\"Ich möchte den Newsletter erhalten.\"}",
      "Scope": "User",
      "Required": false
    }
  ]
}
```

- `Label` darf die Stelle `{0}` enthalten — dort wird der Verweis eingesetzt; fehlt sie, steht er dahinter.
  Damit lässt sich der Satzbau je Sprache anders legen.
- `Version` ist der Stand des Dokuments und wandert **in den Nachweis**. Ändern sich die AGB, ist an den alten
  Nachweisen ablesbar, welchem Wortlaut jemand zugestimmt hat. Leer ist erlaubt, aber eine vertane Gelegenheit.
- `Key` darf sich **nie mehr ändern**, sobald damit Zustimmungen erfasst wurden.

### 21.2.1 `Scope` — wen die Zustimmung betrifft

Das ist die zentrale Einstellung, und sie ist keine Formalie: eine Datenschutzerklärung betrifft die **natürliche
Person** hinter dem Konto, während Nutzungsbedingungen ein **Vertrag** sein können, den jeder Mandant für sich
schliesst. Welches zutrifft, hängt am Geschäftsmodell — deshalb steht es in der Konfiguration.

| `Scope` | Wo der Punkt erscheint | Wiederholung | `TenantId` im Nachweis |
|---|---|---|---|
| `User` (Default) | beim Konto-Teil der Maske | **einmalig** — wer in der geltenden Fassung geantwortet hat, wird nicht wieder gefragt | leer, auch wenn bei einer Mandanten-Anlage erteilt |
| `Tenant` | beim Firmen-Teil | bei **jeder** Mandanten-Anlage; beim blossen Anlegen eines Kontos gar nicht | gesetzt |
| `Both` | beim Konto-Anlegen im Konto-Teil, sonst im Firmen-Teil (dort **einmal**, nicht zusätzlich beim Konto) | bei **jeder** Mandanten-Anlage | gesetzt |

Wichtig bei `Both`: eine früher erteilte persönliche Zustimmung unterdrückt den Punkt bei einer Mandanten-Anlage
**nicht** — sie deckt den Vertrag für *diesen* Mandanten nicht ab. Wer will, dass die Nutzungsbedingungen nur ein
einziges Mal quittiert werden, setzt sie auf `User`.

Ein Klick bleibt **ein** Nachweis: bei `Both` entsteht eine Zeile mit UserId *und* TenantId, nicht zwei.

**Neue Fassung (`Version` geändert):** der Punkt taucht bei der nächsten Gelegenheit wieder auf und wird neu
quittiert; der alte Nachweis bleibt unangetastet stehen. Bestandsnutzer, die gerade nichts anlegen, werden
**nicht** zur Bestätigung gedrängt — ein Zwangs-Dialog beim Login wäre eine eigene Ausbaustufe.

**Unterdrückt wird nach „beantwortet", nicht nach „zugestimmt":** wer den Newsletter einmal abgelehnt hat, wird
nicht bei jeder Gelegenheit erneut gefragt. Bei Pflicht-Punkten macht das keinen Unterschied — ohne Zustimmung
kommt niemand durch, es kann also gar kein abgelehnter Nachweis entstanden sein. (Ein späteres „ich will den
Newsletter doch" braucht folglich eine Profilseite; die gibt es noch nicht.)

**Beim Beitritt zu einem bestehenden Mandanten wird nichts gefragt.** Wer eine Einladung mit bestehendem Konto
annimmt, erhält Zutritt zu fremden Daten und schliesst keinen eigenen Vertrag. `JoinRegister` fragt nur deshalb,
weil dort ein **neues Konto** entsteht.

### 21.3 Die Dokumente liegen als Hilfe-Themen

`HelpSlug` zeigt auf ein Thema des Hilfesystems (§ `HelpSystem-Setup.md`). Das ist bewusst so: Hilfe-Themen sind
ohnehin pro Sprache gepflegt, in Markdown verfasst und **ohne Anmeldung** lesbar — Voraussetzung dafür, dass sie
beim anonymen Start überhaupt aufgehen. Der Verweis öffnet in einem **neuen Tab**, damit das halb ausgefüllte
Formular nicht verloren geht.

Damit AGB und Datenschutz nicht mitten in der Produkthilfe stehen, hat `HelpTopic` neu das Flag **`ShowInMenu`**
(siehe §21.4): legt einen Container „Dokumente" mit `ShowInMenu = false` an und hängt die Dokumente darunter.

### 21.4 `HelpTopic.ShowInMenu` — **Migration (1 Spalte)**

```sql
ALTER TABLE dbo.HelpTopic ADD ShowInMenu bit NOT NULL CONSTRAINT DF_HelpTopic_ShowInMenu DEFAULT 1;
```

PostgreSQL:

```sql
ALTER TABLE "HelpTopic" ADD COLUMN "ShowInMenu" boolean NOT NULL DEFAULT true;
```

Default `true` — der Bestand verhält sich damit unverändert. Das Flag steuert **nur die Auflistung**, nicht die
Erreichbarkeit: ein Thema mit `ShowInMenu = false` ist weiterhin unter `/help/{slug}` abrufbar, taucht aber
weder im Navigationsbaum des Viewers noch im Teilbaum des Kontext-Popups auf — **zusammen mit allem, was unter
ihm hängt**. Wer ein Thema wirklich vom Netz nehmen will, nimmt `IsPublished` zurück. Im Admin-Baum sind solche
Themen mit dem Chip *not in menu* markiert.

### 21.5 Was sich im Code ändert

- Beide Onboarding-Handler haben `IConsentProvider` als **neuen Ctor-Parameter** — über
  `AddMudBlazor*OnboardingViews` automatisch, bei manueller Registrierung nicht.
- `BillingProfileViewModel.AcceptTos` hat seine Pflicht-**Datenannotation verloren**: sind Punkte konfiguriert,
  wird dieser Schalter gar nicht gezeigt, und die Annotation würde das Formular dann gegen etwas sperren, das
  niemand sehen kann. Geprüft wird jetzt beim Abschicken, wo bekannt ist, welcher Fall vorliegt. **Wer das
  Ansichtsmodell selbst verwendet, ohne über `BillingProfileForm` zu gehen, muss die Prüfung selbst aufrufen.**
- Neu am Ansichtsmodell: `Consents` (Schalterstellungen) und `ConsentAnswers` (der Nachweis). Beide reisen im
  geparkten Payload mit — der Nachweis hält den Zeitpunkt der **Zustimmung** fest, nicht den seiner Ablage; beim
  verzögerten Onboarding liegt die Mailbestätigung dazwischen.
- `BillingProfileForm` bekommt die anzuzeigenden Punkte neu als Parameter `ConsentPoints`; die Seite lädt sie
  (`IConsentProvider.DescribeAsync`), weil erst sie den Anlass kennt. Wer das Formular selbst einbindet, muss den
  Parameter setzen — sonst erscheint dort nur der eingebaute Rückfall-Schalter.

### 21.6 Die Nachweise ansehen — und ändern

Zwei Oberflächen, mit bewusst verschiedenem Zuschnitt:

**Verwaltung: `/Onboarding/BillingProfile`, Reiter *Zustimmungen*** — gegated durch die neue Berechtigung
**`Onboarding.Admin.Consents.View`**. Zeigt, was für den Mandanten erklärt wurde **und** was seine Mitglieder
persönlich erklärt haben; Nachweise von Personen ausserhalb des Mandanten sind nie dabei. Im hierarchischen
Betrieb bewusst nur die *direkten* Mitglieder: ein übergeordneter Verantwortlicher soll nicht beiläufig die
persönlichen Erklärungen aller nachgeordneten Personen einsehen.

Es gibt **kein Schreib-Gegenstück** — weder eine Berechtigung noch eine Handler-Methode. Ein Nachweis, den man
bearbeiten kann, ist keiner.

**Konto: `/Account/Onboarding/MyConsents`** — was der angemeldete Benutzer persönlich erklärt hat, mit Datum
und Fassung. Nur die Punkte mit `Scope = User`: was für einen Mandanten erklärt wurde, gehört in dessen
Verwaltung, auch wenn dieselbe Person geklickt hat.

- **Freiwillige Punkte sind umstellbar** — das schliesst die Lücke, dass ein einmal abgelehnter Newsletter
  nie wieder gefragt wurde und es keinen Weg zurück gab.
- **Pflicht-Punkte stehen nur zum Nachlesen.** Ihr Widerruf wäre kein Schalter, sondern eine Kündigung — wer
  die Nutzungsbedingungen nicht mehr trägt, kann den Dienst nicht weiter nutzen, und das lässt sich nicht
  sinnvoll als Häkchen abbilden.
- Eine Änderung schreibt einen **neuen** Nachweis; der alte bleibt stehen. Die Geschichte ist der Zweck der
  Ablage — einen erteilten Nachweis nachträglich umzuschreiben hiesse, ihn zu fälschen.
- Hat sich die Fassung seit der Antwort geändert, steht das dabei. Der alte Nachweis bleibt gültig, gefragt
  wird bei nächster Gelegenheit erneut.

Die Seite verlangt keine eigene Berechtigung (nur Anmeldung) und braucht keinen Navigationseintrag, wenn nichts
konfiguriert ist — sie zeigt dann schlicht, dass es nichts anzuzeigen gibt.

### 21.7 Wo welche Zustimmung erscheint

| Seite | Was entsteht | `User`-Punkte | `Tenant`/`Both` |
|---|---|---|---|
| `/Account/Onboarding/Start` (anonym) | Konto **und** Mandant | im Konto-Abschnitt (Kennwort) | im Firmen-Abschnitt |
| `/Account/Onboarding/CreateTenant` (angemeldet) | Mandant | nur was noch offen ist, im selben Feld | im selben Feld |
| `/Account/Onboarding/JoinRegister` | nur Konto | ja | `Tenant` gar nicht, `Both` als persönliche Zustimmung |
| Einladung annehmen (*Meine Mandanten*) | nichts | — | — |

---

## 22. Selbstregistrierung: `/Account/Register` + Standard-Mandant (opt-in, kein Schema-Change)

Die Anmeldeseite verlinkt seit jeher auf `Account/Register` — **diese Seite gab es nicht**, der Verweis lief ins
Leere. Sie existiert jetzt, ist aber standardmässig **abgeschaltet**.

### 22.1 Warum abgeschaltet

Ein Konto ohne Mandanten ist eine Sackgasse: der Benutzer registriert sich, bestätigt die Mail und landet in
einer leeren Mandanten-Übersicht. `RegisterAccountAsync` legt ausschliesslich den Identity-Benutzer an; die
Zuordnung geschah bisher nur über eine wartende **Employee-Einladung** (E-Mail-Abgleich in
`AcceptInvitationAsync`). Wer ohne Einladung kam, bekam nichts.

Deshalb neu in `TenantSetupOptions`:

```json
{
  "AllowSelfRegistration": true,
  "DefaultUserTenant": "PUBLIC",
  "DefaultUserTenantRole": "Gast"
}
```

- **`AllowSelfRegistration`** (Default `false`) — ohne dies zeigt `/Account/Register` nur den Hinweis, dass hier
  keine Konten angelegt werden können, **und der Verweis auf der Anmeldeseite verschwindet**. Bewusst opt-in:
  ein offenes Registrierungsformular ist eine Entscheidung des Betriebs und soll nicht mit einem Paket-Update
  hereinkommen. Der Einladungs-Weg (`/Account/Onboarding/JoinRegister`) und das Direkt-Onboarding sind davon
  **nicht** betroffen.

  Der Verweis hängt an **zwei** Bedingungen, die verschiedene Fragen beantworten und beide zutreffen müssen:
  `LoginOptions.RegistrationPage.AllowRegister` (gibt es überhaupt eine Registrierungsseite — das kann auch
  eine hosteigene sein) **und** `AllowSelfRegistration` (nimmt sie gerade Vorgänge an). Gelesen wird Letzteres
  über die neue, paket-neutrale Abstraktion **`ISelfRegistrationPolicy`** (`ITVComponents.WebCoreToolkit`,
  Ordner `Security`, neben `IAccountConfirmationMailer`): die Anmeldeseite liegt in `IdentityPages`, die
  Registrierungsseite in `AdminViews`, und keines der beiden Pakete darf das andere kennen. Die Anmeldeseite
  löst die Richtlinie **optional** auf — ein Host ohne Onboarding-Paket verhält sich unverändert.

  **Anzeige und Prüfung benutzen damit dasselbe Prädikat.** Zwei getrennte Einstellungen wären genau die
  Bauart, aus der ein Verweis entsteht, der beim Anklicken abgewiesen wird — oder eine offene Seite, zu der
  kein Weg führt.
- **`DefaultUserTenant`** — `TenantName` (ersatzweise `DisplayName`) des Mandanten, dem der neue Benutzer
  zugewiesen wird. Leer = er bekommt keinen, also wieder die Sackgasse (wird protokolliert).
- **`DefaultUserTenantRole`** — die Rolle, die er dort erhält. Leer = Mitglied ohne Rolle, was praktisch heisst:
  er sieht nichts (wird protokolliert).

### 22.2 Wann die Zuweisung fällt

`IOnboardingHandler.AssignDefaultTenantAsync` läuft auf der **Mandanten-Übersicht**, direkt neben der
Fertigstellung eines geparkten Onboardings — also auf der ersten angemeldeten Landung **nach** der
Mailbestätigung. Einen unbestätigten Benutzer einem Mandanten zuzuschlagen hiesse, jemandem Zutritt zu geben,
von dem noch nicht feststeht, dass ihm die Mailadresse überhaupt gehört.

Zugewiesen wird nur, wenn **beides** zutrifft:

1. der Benutzer gehört noch **keinem** Mandanten an, und
2. es wartet **keine** Employee-Einladung auf seine Adresse — eine Einladung hat Vorrang und führt ihn dorthin,
   wo er hingehört; ihn zusätzlich in den Standard-Mandanten zu setzen wäre ungewollter Zutritt.

Die Methode ist idempotent (jeder weitere Aufruf liefert `false`) und liest Mandant und Rolle mit
`IgnoreQueryFilters` — der Benutzer hat auf diesen Mandanten ja gerade noch keinen Zugriff, mit aktiven
Mandanten-Filtern fände die Abfrage nichts.

### 22.3 Register- und Join-Seite teilen sich eine Komponente

`RegisterAccountForm.razor` trägt Formular, Zustimmungen, die Same-Browser-Bindung (Nonce + Cookie), den
Mailversand und das Warten auf die Bestätigung. Darüber liegen zwei dünne Seiten, die sich **nur im
erklärenden Text** unterscheiden:

| Seite | Text | `AllowSelfRegistration` nötig |
|---|---|---|
| `/Account/Register` | „Konto anlegen" | **ja** |
| `/Account/Onboarding/JoinRegister` | „…weil Sie eine Einladung erhalten haben" | nein (unverändert) |

`?email=` wird auf beiden vorbelegt; `/Account/Register` nimmt zusätzlich `?returnUrl=` entgegen (die
Anmeldeseite reicht ihr eigenes Ziel durch). **Der Wert wird auf eigene Pfade eingeschränkt** — absolute
Adressen, protokoll-relative `//host` und Backslash-Varianten fallen protokolliert auf die Mandanten-Übersicht
zurück. Ungeprüft wäre das eine offene Weiterleitung, und zwar an zwei Stellen: im Bestätigungslink der Mail und
beim Neuladen nach der Bestätigung.

---

## 23. Zusatzangaben-Module in der Firmendaten-Erfassung (opt-in, kein Schema-Change)

Die Firmendaten-Erfassung lässt sich um eigene Angaben erweitern — pro aktiviertem Modul ein Reiter unter den
Adressen, sowohl beim Onboarding als auch später im Firmenprofil.

### 23.1 Ein Modul schreiben

Der Vertrag `ICustomCompanyInformationHandler` liegt **Blazor-frei** in
`EntityFramework.Onboarding/Shared/Extensibility` — ein Modul lässt sich schreiben, ohne die
Oberflächen-Bibliothek zu referenzieren. Es ist ein Plugin (`IPlugin`) und liefert:

- `Key` — der stabile Schlüssel, unter dem seine Angaben abgelegt werden. **Darf sich nie mehr ändern.**
- `Title` / `Icon` — Beschriftung des Reiters; `Title` darf Kultur-JSON sein.
- `GetFields(ctx)` — die Felder der generischen Maske (Text, Zahl, Ja/Nein, Datum, Auswahl …). Weil der Kontext
  übergeben wird, darf dieselbe Angabe im einen Fall Pflicht und im anderen freiwillig sein.
- `AppliesTo(ctx)` — ob das Modul in diesem Fall überhaupt zuständig ist.
- `ValidateAsync` / `PersistAsync` / `LoadAsync` — prüfen, ablegen, wieder laden. **Das Toolkit legt von diesen
  Angaben selbst nichts ab**: sie sind nicht sicherheitsrelevant und gehören nicht in die Security-Datenbank.
- `EditPermission` — siehe §23.3.
- `ViewKey` — leer = generische Maske. Sonst der Schlüssel einer eigenen Razor-Komponente, die der Host über
  `services.ConfigureCustomCompanyInfoViews(c => c.RegisterView<MeineMaske>("mein.schluessel"))` anmeldet und
  die `ICustomCompanyInfoView` erfüllt. Bewusst ein Schlüssel und kein Typ — sonst müsste der Modul-Autor doch
  wieder die Oberflächen-Bibliothek kennen.

Der Kontext (`CustomInfoContext`) nennt die Mandanten-Strategie **nirgends**: er trägt `Mode` (Create/Edit),
`Origin` (SelfService/Invitation), `ProfileType`, `TenantId` und `ParentTenantId`. Letzteres ist im flachen
Betrieb schlicht immer `null` — ein Modul, das nur die ersten drei auswertet, läuft in beiden Welten unverändert.

### 23.2 Aktivieren (GlobalSettings `CustomCompanyInfo`)

```json
{ "Handlers": [ "MeinNetzwerktypModul", "MeinBranchenModul" ] }
```

Eine Namensliste statt einer Typsuche: der Plugin-Bestand hat keinen Typ-Index, und die Reihenfolge gibt zugleich
die Reihenfolge der Reiter vor. **Die genannten Plugins müssen global sein (kein Tenant)** — während der Anlage
gibt es den Mandanten, zu dem die Angaben gehören, noch gar nicht.

Geladen wird über den **frischen** Plugin-Weg (`IFreshInjectablePlugin`), nicht den geteilten: ein Modul hält
typischerweise einen eigenen DbContext, und ein pro Scope geteiltes solches Objekt ist unter Blazor genau die
Bauart, aus der *„a second operation was started on this context"* entsteht. Der Provider hält zudem nie ein
Modul über die Dauer eines Formulars — jede Operation öffnet und schliesst es.

Ohne konfigurierte Module ist das alles **reiner Leerlauf**; der Plugin-Ladeweg wird erst aufgelöst, wenn wirklich
Namen in den Einstellungen stehen. Ein Host ohne Module braucht also kein `UseInjectablePlugins`.

### 23.3 Nachtragen im Firmenprofil

Die Reiter erscheinen jetzt auch in `/Onboarding/BillingProfile` (Tab 1, unter den Adressen), vorbelegt mit dem,
was `LoadAsync` liefert. Gespeichert wird **nach** dem Profil: die Angaben liegen in fremden Ablagen, mit denen es
keine gemeinsame Transaktion gibt. Scheitert ein Modul, bleibt das gespeicherte Profil bestehen und der Benutzer
bekommt einen Hinweis; welches Modul es war, steht im Protokoll.

**`EditPermission`** greift genau hier: nennt ein Modul eine Berechtigung, bekommt nur ein Benutzer mit dieser
Berechtigung den Reiter zu sehen — und geprüft wird **an derselben Stelle noch einmal beim Schreiben**. Eine
gefilterte Maske allein hielte niemanden davon ab, den Datensatz eines fremden Moduls einfach mitzuschicken. Leer
= es gilt die Berechtigung des Firmenprofils selbst (`Onboarding.Admin.BillingProfile.Write`).

Während der **Anlage** gilt `EditPermission` nicht: dort gibt es weder den Mandanten noch Rechte darauf.

### 23.4 Eine eigene Maske statt der generischen (`ViewKey`)

Reicht die Feld-Deklaration nicht — abhängige Felder, eine Tabelle, eine Karte — bringt das Modul eine eigene
Razor-Komponente mit. Es nennt dafür nur einen **Schlüssel**; welche Komponente dahinter steht, entscheidet der
Host. Ein `Type` im Modul wäre entweder eine falsche Abhängigkeit (Blazor im Vertrag) oder ein Typname als
Zeichenkette — und Letzteres machte Konfigurationspflege gleichbedeutend mit Code-Ausführung.

**1. Modul:** `GetFields` bleibt leer, `ViewKey` nennt den Schlüssel.

```csharp
public string ViewKey => "sample.sitesurvey";

public IReadOnlyList<CustomInfoField> GetFields(CustomInfoContext ctx)
    => Array.Empty<CustomInfoField>();

// values ist bei eigener Maske IMMER leer — die flache Sicht gibt es nur für die generische Maske.
// Wer eine eigene mitbringt, bestimmt die Form seines Datensatzes selbst und liest payload.
public Task<CustomInfoValidation> ValidateAsync(IReadOnlyDictionary<string, string> values,
    JsonNode payload, CustomInfoContext ctx, CancellationToken ct)
{
    int? sites = (payload as JsonObject)?["Sites"]?.GetValue<int?>();
    return Task.FromResult(sites is null or < 1
        ? CustomInfoValidation.Failed("{\"de\":\"Mindestens ein Standort ist noetig.\"}", "Sites")
        : CustomInfoValidation.Ok());
}
```

**2. Maske:** erfüllt `ICustomCompanyInfoView`, liest ihren Zustand über den Kaskaden-Wert — und bringt
**keinen Absende-Knopf** mit. Der Rahmen gehört der Erfassung: Reiter, Prüfung über alle Reiter hinweg und das
Anspringen des Reiters, an dem etwas fehlt, sind für jede Maske gleich. Zwei Knöpfe, von denen nur einer den
Vorgang abschliesst, wären eine Falle.

```razor
@implements ICustomCompanyInfoView

<MudNumericField T="int?" @bind-Value="sites" Label="Standorte"
                 Error="@(missing == nameof(sites))" ErrorText="Mindestens ein Standort ist noetig." />

@code {
    [CascadingParameter] public CustomCompanyInfoViewContext Ctx { get; set; } = default!;

    private int? sites;
    private string? missing;

    protected override void OnInitialized()
    {
        // Vorbelegung aus dem, was am Mandanten liegt. Beim Anlegen ist Existing null.
        if (Ctx.Existing is JsonObject obj) { sites = obj["Sites"]?.GetValue<int?>(); }
    }

    public Task<CustomInfoViewResult> ResolveValuesAsync()
    {
        missing = null;
        if (sites is null or < 1)
        {
            missing = nameof(sites);
            StateHasChanged();
            // Ohne Meldung: die Stelle ist bereits markiert, eine Einblendung wäre nur Lärm.
            return Task.FromResult(CustomInfoViewResult.Incomplete());
        }

        return Task.FromResult(CustomInfoViewResult.Complete(
            new JsonObject { ["Sites"] = JsonValue.Create(sites.Value) }));
    }
}
```

**3. Host:** verbindet Schlüssel und Komponente beim Start.

```csharp
services.ConfigureCustomCompanyInfoViews(c => c.RegisterView<SiteSurveyView>("sample.sitesurvey"));
```

`RegisterView<T>` verlangt `IComponent` **und** `ICustomCompanyInfoView` schon beim Übersetzen — eine Maske,
die den Vertrag verletzt, kommt gar nicht durch den Compiler und nicht erst dann, wenn ein Benutzer den Reiter
öffnet. Ein Schlüssel, zu dem nichts registriert ist, fällt auf die generische Maske zurück, aber mit einer
Log-Zeile, nicht still.

**Lauffähige Vorlage:** `ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test/Samples/` enthält Modul
und Maske vollständig — bewusst in einem Testprojekt und nicht im ausgelieferten Paket, wo sie Code wären, den
niemand benutzt, der aber bei jedem Kunden mitginge. Dort wird der Weg auch tatsächlich übersetzt und geprüft,
statt nur beschrieben.

### 23.5 Zwei Verhaltensänderungen im geteilten Feld-Renderer

Der Renderer aus den Workflow-Aufgabenmasken ist nach `Blazor.MudBlazor/SharedComponents/DeclaredFieldsForm.razor`
gewandert; `UserTaskFieldsForm` ist jetzt ein Adapter mit unveränderter API. Dabei zwei Korrekturen, die auch die
Workflow-Masken betreffen:

1. Ein **Ja/Nein-Pflichtfeld** startet auf `false`. Vorher galt ein unberührter Schalter als „nicht ausgefüllt" —
   man kam nur durch, indem man ihn ein- und wieder ausschaltete.
2. Die **Vorbelegung** lief nie, wenn der Aufrufer keinen `ResetKey` setzte. Im Bestand betraf das niemanden.

---

## 24. Anonym ladbare Plugins: `WebPlugin.AllowAnonymous` — **Pflicht: Spalte manuell nachziehen**

Der Berechtigungs-Torwächter im Plugin-Ladeweg stellt zugleich sicher, dass überhaupt jemand angemeldet
ist. Das ist gewollt und bleibt so. Für Abläufe, die es **vor** der Anmeldung gibt — allen voran das
Onboarding mit seinen Zusatzangaben-Modulen (§23) — braucht es aber Plugins, die auch anonym geladen
werden dürfen. Dafür gibt es neu eine ausdrückliche Kennzeichnung an der Zeile statt einer allgemeinen
Lockerung der Prüfung.

### 24.1 Spalte anlegen

Additive, nicht-nullable Spalte mit Default `0`. **Nicht** über `dotnet ef migrations add` — der Snapshot
driftet und würde fremde Änderungen mitschleppen (gleiche Begründung wie in §20):

```sql
ALTER TABLE [WebPlugins] ADD [AllowAnonymous] bit NOT NULL CONSTRAINT [DF_WebPlugins_AllowAnonymous] DEFAULT 0;
```

PostgreSQL:

```sql
ALTER TABLE "WebPlugins" ADD COLUMN "AllowAnonymous" boolean NOT NULL DEFAULT false;
```

Ohne die Spalte schlägt jede Plugin-Query mit *„Invalid column name 'AllowAnonymous'"* fehl.

### 24.2 Was zu setzen ist

Die Kennzeichnung greift **nur**, wenn niemand angemeldet ist. Ist jemand angemeldet, entscheidet wie
bisher allein die Berechtigung — ein anonym erlaubtes Plugin ist für angemeldete Benutzer also nicht
grosszügiger als für alle anderen.

Sie gilt ausserdem **nur für globale Plugins** (`TenantId IS NULL`). Ein Mandanten-Plugin ist ohne
angemeldeten Benutzer gar nicht sichtbar — es gibt dann keine Mandanten-Zugehörigkeit, über die es
ausgewählt würde. Die Regel steht in der Entität selbst: der Getter liefert für eine Mandanten-Zeile
immer `false`, auch wenn die Spalte gesetzt wurde. Die Maske bietet den Schalter entsprechend nur bei
globalen Plugins an, und der Admin-Handler setzt ihn beim Speichern auf `false`, wenn ein Mandant die
Zeile besitzt.

Setzen im Plugin-Editor (*Verwaltung → Plug-Ins*, Spalte **Anonymous**, Schalter **Allow anonymous** im
Dialog) oder direkt:

```sql
UPDATE [WebPlugins] SET [AllowAnonymous] = 1
WHERE [TenantId] IS NULL AND [UniqueName] = 'TenantNetworkInfo';
```

Für §23 gilt: **jedes Zusatzangaben-Modul, das im anonymen Onboarding greifen soll, braucht das Flag.**
Ohne es lädt das Modul erst nach der Anmeldung, und der Reiter fehlt im anonymen Ablauf — im Log
erkennbar an *„The plugin '…' configured as a custom-company-info module could not be loaded"*.

### 24.3 Für eigenen Code

`VerifyUserPermissions` hat eine neue Überladung mit zusätzlichem `out bool isUserAuthenticated`. Sie
beantwortet die Frage, die ein negatives Ergebnis bisher offen liess: *fehlt die Berechtigung, oder ist
schlicht niemand angemeldet?* Die bestehenden Überladungen sind unverändert — wer sie benutzt, merkt
nichts.

---

## 25. Dashboard in Blazor: Standard-Sammlung + eigene Anordnung — **Pflicht: 4 Spalten manuell nachziehen**

Bisher gab es zu den Dashboard-Widgets nur den Editor und den `WidgetRenderer` für eine einzelne Kachel;
die Fläche selbst existierte nur in der Telerik-Welt (`ViewComponents/Dashboard.cs` +
`lib/js/Tools/DashboardWidgets.js`). Neu gibt es sie als Blazor-Komponente, die die Daten **ohne
HTTP-Umweg** über `IDiagnosticsQueryService` holt.

### 25.1 Spalten anlegen

Alle vier additiv. **Nicht** über `dotnet ef migrations add` (Snapshot-Drift, gleiche Begründung wie §20/§24):

```sql
ALTER TABLE [Widgets] ADD [InitiallyActive] bit NOT NULL CONSTRAINT [DF_Widgets_InitiallyActive] DEFAULT 0;
ALTER TABLE [Widgets] ADD [SortOrder] int NOT NULL CONSTRAINT [DF_Widgets_SortOrder] DEFAULT 0;
ALTER TABLE [UserWidgets] ADD [ColSpan] int NOT NULL CONSTRAINT [DF_UserWidgets_ColSpan] DEFAULT 0;
ALTER TABLE [UserWidgets] ADD [ParamValues] nvarchar(max) NULL;
```

PostgreSQL:

```sql
ALTER TABLE "Widgets" ADD COLUMN "InitiallyActive" boolean NOT NULL DEFAULT false;
ALTER TABLE "Widgets" ADD COLUMN "SortOrder" integer NOT NULL DEFAULT 0;
ALTER TABLE "UserWidgets" ADD COLUMN "ColSpan" integer NOT NULL DEFAULT 0;
ALTER TABLE "UserWidgets" ADD COLUMN "ParamValues" text NULL;
```

`ColSpan = 0` heisst „nicht gesetzt" und wird als **eine** Spalte gelesen — bestehende Zeilen bleiben also
unverändert gültig. Ohne die Spalten schlägt jede Widget-Query mit *„Invalid column name 'InitiallyActive'"*
fehl.

### 25.2 Die Standard-Sammlung festlegen

Im Editor (*Verwaltung → Dashboard widgets*) gibt es neu den Schalter **Initially active** und das Feld
**Sort order**. Beides zusammen ist die Standard-Sammlung:

- Wer **noch keine eigenen** Widgets hat, sieht genau diese — **ohne dass dafür etwas gespeichert wird**.
- Sobald er den Stift drückt, wird die Sammlung **für ihn kopiert**. Ab dann hat er eigene Widgets, und
  späteres Ändern der Vorgabe geht ihn nichts mehr an.

Die Sammlung ist trotzdem mandantenabhängig: die Widget-Liste ist global auf Widgets gefiltert, deren
DiagnosticsQuery dem aktuellen Mandanten zugeordnet ist, und zusätzlich wird je Widget die Berechtigung
seiner Query geprüft. Ein Widget, das der Benutzer nicht sehen darf, kommt in der Kopie nicht vor.

**Widgets mit Pflichtparametern nicht als `InitiallyActive` markieren.** Für eine Kopie fragt niemand die
Parameter ab — die Kachel läuft dann mit den Platzhaltern ihres `CustomQueryString`, die zu leeren
Argumenten werden.

### 25.3 Die Seite einbinden

Fertig dabei ist `/Dashboard` (in den AdminViews). Eine eigene Seite ist eine Zeile:

```razor
@using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets

<DashboardHost Title="Dashboard" Columns="3" OnWidgetAction="OnWidgetAction" />
```

`OnWidgetAction` bekommt, was ein Template über `data-widget-action` / `data-widget-arg` auslöst — was
eine Aktion bedeutet, entscheidet die Anwendung. Das JS für die Klick-Weiterleitung registriert der
WebPart der AdminViews bereits (`AddToolkitClientScript(".../widget-actions.js")`); wer die AdminViews
nicht referenziert, muss es selbst registrieren.

### 25.4 Template-Syntax — **das ist der eine echte Bruch**

Die Blazor-Kachel rendert mit **Scriban**, nicht mit `ITVenture.Text.processMessage`. Die Klammerung ist
dieselbe (`{{ … }}`), einfache Platzhalter laufen also unverändert. **Nicht** übernommen werden:

| bisher | Bedeutung | in Scriban |
|---|---|---|
| `{{ ->Key }}` | Übersetzung nachschlagen | ersetzt durch `Translate(…)`, s. §25.4.1 |
| `{{ $expr }}`, `{{ !$expr }}` | JavaScript-Ausdruck | **nicht unterstützt** |

Dafür kann Scriban, was vorher fehlte: echte Schleifen und Bedingungen. Gerendert wird gegen:

| Ausdruck | Inhalt |
|---|---|
| `{{ Rows }}` | alle Zeilen der Query |
| `{{ Row.Spalte }}` | die erste Zeile — für die häufige Ein-Zahl-Kachel |
| `{{ Count }}` | Anzahl Zeilen |
| `{{ Params.Name }}` | die Parameter-Eingaben |
| `{{ Title }}` | der aufgelöste Titel |

Die Eigenschaftsnamen bleiben, wie sie in der Query heissen (kein snake_case). Beispiel:

```html
<div class="pa-2">
  <h3>{{ Title }}</h3>
  {{ for row in Rows }}
    <button data-widget-action="navigate" data-widget-arg="/Orders/{{ row.OrderId }}">
      {{ row.Customer }}: {{ row.Total }}
    </button>
  {{ end }}
</div>
```

Templates gelten als **vertrauenswürdig** (Sysadmin/Mandanten-Admin) und werden als rohes HTML gerendert.
Für Werte, die aus Benutzereingaben stammen, `{{ value | html.escape }}` benutzen.

#### 25.4.1 Mehrsprachigkeit im Template: `Translate(…)`

Statt `{{ ->Key }}` gibt es eine Funktion, die **dieselbe** Auswahl trifft wie der Rest des Toolkits
(Navigations-Beschriftungen, Aufgabenmasken) — `StringExtensions.Translate` bzw. `DictionaryExtensions.Translate`.
Sie nimmt zwei Formen:

```html
{{ Translate({"de":"Offene Aufträge","fr":"Commandes ouvertes","Default":"Open orders"}) }}

{{ Translate(Row.Caption) }}
```

Die erste ist das Objekt-Literal aus der Frage. Die zweite ist der häufigere Fall: eine Datenbankspalte, die
**entweder** Klartext **oder** ein Kultur-JSON-Datensatz enthält — genau wie überall sonst im Toolkit. Beides
geht durch dieselbe Funktion.

Auswahlreihenfolge: exakte Kultur → neutrale (`de-CH` → `de`) → Schlüssel `Default`. **Die Schlüssel sind
gross-/kleinschreibungsempfindlich und `Default` schreibt sich mit grossem D** — bewusst dieselben Regeln wie
sonst, damit ein Datensatz zwischen Navigations-Beschriftung und Widget wandern kann, ohne die Bedeutung zu
ändern. Zwei Zusätze:

- Findet sich beim **Objekt-Literal** überhaupt nichts, wird die **erste** Angabe genommen statt nichts. Wer
  zwei Sprachen ohne `Default` schreibt, meint den Text, nicht die Leere.
- Bei der **invarianten** Kultur (Kulturname leer) wird auf `Default` aufgelöst. Ohne das würde die
  String-Variante den Übersetzungspfad gar nicht betreten und der Leser sähe rohes JSON.

Alias-Schreibweisen: `translate` (Scriban-Konvention, klein) und `Translate` sind derselbe Aufruf. Wer eine
andere Kultur als die des Lesers braucht: `TranslateFor(wert, "fr-CH")` bzw. `translate_for`.

Die Funktion steht **auch im `TitleTemplate`** zur Verfügung — ein Kacheltitel lässt sich also genauso
übersetzen wie der Kachelinhalt.

Eigene Funktionen dazuhängen geht an einer Stelle: `WidgetTemplateFunctions.ImportFunctions`.

#### 25.4.2 `DisplayName` und `TitleTemplate` dürfen selbst ein Kultur-Datensatz sein

Es gibt zwei Wege, einen Kacheltitel mehrsprachig zu halten, und beide funktionieren jetzt:

1. eine **Lokalisierungs-Zeile** je Sprache (Reiter *Localizations* am Widget) — die gab es schon;
2. ein **Kultur-JSON direkt im Feld**, also `{"de":"Offene Aufträge","Default":"Open orders"}` in
   `DisplayName` bzw. `TitleTemplate`. Das ist die Form, die überall sonst im Toolkit gilt.

Aufgelöst wird beim **Anzeigen**, in dieser Reihenfolge: erst die Platzhalter (Scriban), dann die Sprache.
Die Reihenfolge ist Absicht — ein `TitleTemplate`, das nur aus `{{From}}` besteht, fängt mit `{` an und
hört mit `}` auf und sähe sonst aus wie ein Kultur-Datensatz. Umgekehrt darf damit jede Sprache im
Datensatz ihre eigenen Platzhalter tragen: `{"de":"Umsatz {{From}}","Default":"Revenue {{From}}"}`.

Zwei Dinge dazu:

- Der beim Einrichten aus dem `TitleTemplate` gebildete Kachelname wird **unübersetzt** gespeichert. Sonst
  wäre die Sprache des Tages, an dem die Kachel eingerichtet wurde, für immer festgeschrieben.
- Ist die Kultur des Lesers **leer** (invariante Kultur), wird jetzt auf `Default` aufgelöst statt gar
  nicht — vorher sah man in diesem Fall das rohe JSON, obwohl alles richtig hinterlegt war.

In der **Pflege-Ansicht** (`/Util/DashboardWidgets`) steht in der Spalte *Display name* weiterhin der
Rohwert. Das ist gewollt: dort wird der Datensatz bearbeitet, nicht gelesen.

### 25.5 Parameter-Masken: `InputConfig` bekommt eine neue Form

Die Parameter-Eingabe benutzt jetzt `DeclaredFieldsForm` — dieselbe Maske wie die Workflow-Aufgaben und
die Onboarding-Zusatzangaben. Das alte `InputConfig` war eine rohe Kendo-Widget-Konfiguration und ist für
Blazor nicht deutbar; **unbekannte Schlüssel werden ignoriert**, ein alter Eintrag fällt also auf ein
einfaches Eingabefeld zurück statt die Maske zu verhindern. Die neue, optionale Form:

```json
{ "label": "Von", "helpText": "Startdatum", "required": true, "multiline": false }
```

Für `Combo` kommt die Auswahlliste entweder fest mit oder aus einer FK-Tabelle:

```json
{ "choices": [ { "value": "A", "label": "Aktiv" }, { "value": "I", "label": "Inaktiv" } ] }
{ "fkTable": "Tenants" }
{ "fkTable": "Permissions", "connection": "sys" }
```

Zwei Einschränkungen: `MaskedText` wird zum normalen Textfeld (die Maskierung war eine Kendo-Eigenschaft),
und ein `Combo`, für das keine Auswahl zu ermitteln ist, fällt auf ein Textfeld zurück — mit einer Warnung
im Log, statt den Benutzer vor einem leeren Pflicht-Auswahlfeld sitzen zu lassen.

Die Eingaben werden neu **mitgespeichert** (`UserWidgets.ParamValues`), damit sie später änderbar sind.
Der `CustomQueryString` bleibt die Vorlage mit den Platzhaltern; eingesetzt wird erst beim Ausführen. Alte
Zeilen aus dem Telerik-Dashboard tragen dort einen bereits eingesetzten String und kein `ParamValues` —
die laufen unverändert weiter.

### 25.6 Wenn ihr das alte Telerik-Dashboard weiter benutzt

`/DBW` (Get/Set) reicht `ColSpan` und `ParamValues` jetzt mit durch. Das ist kein Selbstzweck: der Store
liest einen fehlenden Wert als *geleert*, ein Speichern von dort hätte sonst die in Blazor gesetzte Breite
und die Parameter-Werte verworfen. Wer eine eigene Kopie dieser Endpunkte hat, muss die beiden Felder
selbst mitschleifen.

### 25.7 Schmale Viewports

`Columns` ist die Spaltenzahl für **breite** Fenster. Die tatsächliche ergibt sich aus dem gemeldeten
Viewport: **Xs = 1 Spalte, Sm = die Hälfte (aufgerundet), darüber die konfigurierte Zahl.** Gemeldet wird
über einen `MudBreakpointProvider` um die Fläche, also nach C# und nicht über CSS-Umbrüche — reines CSS
könnte die konfigurierte Spaltenzahl nicht kennen, und der Breiten-Umschalter muss mit derselben Zahl
rechnen.

Der gespeicherte `ColSpan` bleibt davon **unberührt**: er ist die Absicht des Benutzers, nicht die
Darstellung eines bestimmten Fensters. Geklemmt wird nur beim Rendern. Ohne diese Trennung hätte ein
einziger Besuch vom Telefon alle Breiten dauerhaft auf 1 gesetzt.

**Umsortieren per Ziehen funktioniert auf Touch-Geräten nicht** — MudBlazors Drag&Drop hängt an den
HTML5-Drag-Ereignissen, die es dort nicht gibt. Das ist keine Einstellung, sondern die Technik darunter.
Deshalb hat das Kachel-Menü zusätzlich **nach vorn / nach hinten**; auf dem Desktop bleibt das Ziehen.

Der Template-Inhalt liegt in einem `overflow-x: auto`-Container: eine breite Tabelle im Template scrollt
**innerhalb** der Kachel, statt die Seite in die Breite zu ziehen. Damit ist das Layout geschützt — was ein
Template darüber hinaus tut, bleibt aber Sache seines Autors. Ein Template mit festen Pixelbreiten oder acht
Spalten sprengt die Seite nicht mehr, wird auf dem Telefon aber auch nicht lesbar. Wer Widgets für mobile
Nutzung schreibt: wenige Spalten, keine festen Breiten, `{{ Row.… }}` für die Ein-Zahl-Kachel statt einer
Tabelle.

### 25.8 Reihenfolge der Standard-Sammlung per Ziehen

In `/Util/DashboardWidgets` hat jede Zeile einen **Griff**: damit lässt sich die `SortOrder` durch Ziehen
setzen, statt die Zahlen von Hand zu vergeben — dieselbe Bedienung wie in der Navigations-Verwaltung, nur
ohne Schachtelung (eine Zeile kennt hier nur *davor* und *dahinter*). Beim Ablegen wird die ganze Liste in
Zehnerschritten neu durchnummeriert, damit die Zahlen im Gitter lesbar bleiben.

Zwei Grenzen, die man sieht statt sie zu erraten:

- Der Griff ist **stumpf**, solange nach einer anderen Spalte oder absteigend sortiert ist — sonst hätte
  man eine Reihenfolge vor Augen, die mit der geschriebenen nichts zu tun hat. Nach *Sort order* aufsteigend
  (oder ganz ohne Sortierung) ist er scharf.
- Gezogen wird **innerhalb einer Seite**; über eine Seitengrenze hinweg gibt es kein Ziel. Bei mehr Widgets
  als eine Seite fasst: Seitengrösse hoch oder die Zahl im Dialog setzen.

Das braucht `DashboardWidgets.Write`. Technisch: `wwwroot/grid-dnd.js` (`window.itvGridDnd`) — dieselbe
Datei bedient jetzt auch die Navigations-Verwaltung, die vorher ein eigenes `navigation-dnd.js` hatte. Wer
das alte Skript irgendwo von Hand als `<script>`-Tag gesetzt hat, muss den Pfad umstellen; über
`<ITVentureReferences />` passiert das von selbst.

### 25.9 Austauschbare Renderer — **Pflicht: 2 weitere Spalten**

Womit eine Kachel gezeichnet wird, ist neu **auswählbar**. Der eingebaute Scriban-Renderer ist dabei eine
Implementierung unter mehreren; ein Konsument kann eigene beisteuern, ohne dass das Toolkit dafür ein
Release braucht. Zwei additive Spalten, wieder **manuell** (Snapshot-Drift, wie §25.1):

```sql
ALTER TABLE [Widgets] ADD [RendererKey] nvarchar(64) NULL;
ALTER TABLE [Widgets] ADD [RendererOptions] nvarchar(max) NULL;
```

PostgreSQL:

```sql
ALTER TABLE "Widgets" ADD COLUMN "RendererKey" character varying(64) NULL;
ALTER TABLE "Widgets" ADD COLUMN "RendererOptions" text NULL;
```

`RendererKey` leer oder NULL heisst **Scriban** — bestehende Widgets verhalten sich unverändert. Ohne die
Spalten schlägt jede Widget-Query mit *„Invalid column name 'RendererKey'"* fehl.

Der **System-Config-Export** (§17) führt beide Felder mit — ohne das käme ein Diagramm-Widget aus einem
Import als Scriban-Widget zurück und schriebe seine Deklaration als Rohtext in die Kachel. Der alte
Telerik-Editor ist nicht betroffen: sein Formular kennt die Felder nicht und rührt sie beim Speichern
darum auch nicht an.

#### 25.9.1 Einen eigenen Renderer schreiben

Ein Renderer ist eine gewöhnliche Blazor-Komponente, die `IWidgetRenderer` erfüllt und ihren Schlüssel als
Attribut trägt:

```razor
@namespace MeineAnwendung.Widgets
@attribute [WidgetRenderer("ampel", DisplayName = "{\"de\":\"Ampel\",\"Default\":\"Traffic light\"}", EditorLanguage = "json")]
@implements IWidgetRenderer

@code {
    [Parameter] public string TemplateSource { get; set; } = "";
    [Parameter] public WidgetTemplateModel? Data { get; set; }
    [Parameter] public IReadOnlyDictionary<string, string?> Options { get; set; } = new Dictionary<string, string?>();
    [Parameter] public EventCallback<WidgetAction> OnAction { get; set; }
    [Parameter] public EventCallback<Exception> OnRenderError { get; set; }
}
```

Angemeldet wird er beim Start:

```csharp
services.ConfigureWidgetRenderers(c => c.RegisterRenderer<AmpelRenderer>());
```

Schlüssel, Beschriftung und Editor-Sprache kommen aus dem Attribut; Überladungen von `RegisterRenderer`
übersteuern sie, wenn derselbe Typ unter mehreren Schlüsseln laufen soll. `RegisterRenderer<T>` verlangt
`IComponent` **und** `IWidgetRenderer` schon beim Übersetzen — was durchkommt, kann die Fläche auch zeichnen.

**Der Konfigurationstext wird bei jedem Zeichnen neu gesetzt** (die Kachel hängt an einer
`DynamicComponent`, und die baut ihr Parameter-Wörterbuch jedes Mal neu auf). Teure Arbeit — Übersetzen,
Auswerten, Parsen — gehört deshalb gepuffert und nur bei geänderter Quelle wiederholt; der eingebaute
`WidgetRenderer` macht genau das seit jeher.

**Einstellungen** deklariert der Renderer bei der Registrierung als `DeclaredField`-Liste (`options:`); der
Editor zeigt dafür dieselbe generische Maske wie für die Widget-Parameter, und die Werte landen invariant
in `RendererOptions`.

Eine Prüfmethode (`validate:`) meldet sich beim **Speichern**. Sie prüft die **Syntax** — ob sich der Text
übersetzen lässt —, und **nur** die. Was eine Konfiguration *tut*, lässt sich ohne Daten nicht beurteilen:
ein völlig richtiges `Rows[0].Months` scheitert an einer leeren Zeilenliste, und eine Prüfung, die deswegen
das Speichern verweigert, ist schlimmer als keine. Inhaltliche Fehler — unbekannter Parameter, unlesbare
Deklaration, Wert keine Zahl — zeigt deshalb die **Kachel**, mit Meldung und Log-Eintrag.

Optional dazu `describeParameters:` — liefert einen Kommentarblock, den der Editor über den Knopf
**„Insert parameters"** unter die Konfiguration hängt. Gedacht für das, was **nur der Code weiß** (bei den
Diagramm-Renderern: die Parameter der Diagramm-Komponente samt Typen und zulässigen Werten, per Reflection
ermittelt). Erklärungen und Beispiele gehören dagegen ins Hilfesystem — eine generierte Liste kann sie
nicht liefern, und sie veralten nicht mit der nächsten Fassung einer Fremdbibliothek. Vorlagen dafür liegen
unter `docs/help/widget-chart-*.md`.

#### 25.9.2 Alternativ über die Teile-Konfiguration

Wer lieber konfiguriert als registriert, nennt den **Typ** im WebPart-Abschnitt — der Schlüssel kommt auch
dort aus dem Attribut:

```jsonc
"MudBasicViews": {
  "UseViews": true,
  "WidgetRenderers": [
    { "Type": "MeineAnwendung.Widgets.AmpelRenderer, MeineAnwendung" }
  ]
}
```

Dieser Weg kann nicht beim Übersetzen prüfen, was da steht. Deshalb wird jeder Eintrag **beim Start**
geprüft und ein fehlerhafter mit Schlüssel und Typname im Log abgelehnt — nicht auflösbar, keine Komponente,
Vertrag nicht erfüllt oder Schlüssel doppelt. Der Start bricht deswegen nicht ab; die betroffenen Kacheln
sagen es dann selbst (siehe unten).

#### 25.9.3 Die mitgelieferten Diagramm-Renderer

Zwei Stück, beide über `MudChart` (kein zusätzliches JS, keine neue Abhängigkeit) und beide mit derselben
Deklaration — sie unterscheiden sich **nur** darin, womit die Deklaration geschrieben wird:

| Schlüssel | Konfiguration ist … |
|---|---|
| `chart.scriban` | ein Scriban-Template, das eine **JSON**-Deklaration rendert |
| `chart.cscript` | ein **CScript**-Objektliteral (LINQ und native C#-Ausdrücke verfügbar) |

```jsonc
// chart.scriban
{
  "type": "pie",
  "labels": {{ json (column Rows "Status") }},
  "series": [ { "name": "Anzahl", "data": {{ json (column Rows "Anzahl") }} } ],
  "chartOptions": { "chartPalette": ["#2979ff", "#00acc1"] }
}
```

```csharp
// chart.cscript
{ type: ChartType.Pie,
  labels: column(Rows, "Status"),
  series: [ { name: "Anzahl", data: column(Rows, "Anzahl") } ] }
```

**`column(Rows, "Name")`** gibt es auf **beiden** Seiten: es zieht eine Spalte als Liste heraus und bedient
dabei beide Zeilenformen (Wörterbuch aus dem dynamischen Adapter, Objekt mit Eigenschaften aus einer
LINQ-Abfrage). Auf der Scriban-Seite kommt **`json(wert)`** dazu, das einen Wert JSON-gerecht schreibt —
Escaping, Zahlen invariant, Listen als Arrays.

**Drei CScript-Eigenheiten, die man einmal wissen muss** (alle drei gegen den Interpreter geprüft):

- **Text steht in doppelten Anführungszeichen.** Einfache bezeichnen in CScript einen **Typ**
  (`'System.TimeSpan'`), aus `'pie'` würde der Versuch, einen Typ namens *pie* zu laden.
- **Es gibt keine Lambda-Ausdrücke.** `Rows.Select(r => r.Status)` ist ein *Syntaxfehler*, und ein
  Funktions-Literal (`function(r) { … }`) nehmen die LINQ-Methoden nicht an (*„No capable Method found for
  Select"*). Genau dafür ist `column` da. Auf einzelne Werte greift man direkt zu — beides funktioniert,
  auch bei Wörterbuch-Zeilen:

  ```csharp
  Rows[0].Status        // Eigenschaft
  Rows[0]["Status"]     // Spaltenname
  Rows.Length           // Anzahl (auch Count über das Modell)
  ```

  Wer wirklich LINQ braucht, nimmt die **native Einbettung** — dort läuft echtes C#, und sie darf auch als
  Wert **mitten in der Deklaration** stehen:

  ```csharp
  { type: ChartType.Pie,
    labels: `E(Rows as Rows->DEFAULT)::@"Dictionary<string,object>[] rw =
                 ((object[])Global.Rows).Cast<Dictionary<string,object>>().ToArray();
               return (from t in rw select (string)t[""Topic""]).ToArray();" with {},
    series: [ { name: "Anzahl", data: column(Rows, "Anzahl") } ] }
  ```

  Dazu vier Dinge, die man wissen muss:

  - **`with { … }` ist Pflicht**, auch leer. Ohne das Anhängsel ist der Ausdruck ein Syntaxfehler.
  - Es gibt **zwei Formen**, und sie schreiben den Code unterschiedlich:
    - **mit Zielobjekt** — `` `E(ziel as name -> cfg)::"code" with {…} ``. Der Code ist ein
      **String-Literal**; für mehrzeiligen Code mit Anführungszeichen die verbatim-Form `@"…"` benutzen und
      innere Anführungszeichen verdoppeln (`""`).
    - **ohne Zielobjekt** — `` `E(#cfg)::@#code# with {…} ``. Hier steht der Code als **Block** zwischen
      `@#` und `#`; ein String-Literal ist dort nicht vorgesehen.

    In beiden Fällen liegen das Ziel und die `with`-Werte im C#-Code unter `Global.<name>`.
  - **`Global.Rows` ist ein `object[]`** — beim Prüfen wie beim Ausführen derselbe Typ, darauf kann man
    sich beim Casten verlassen. Die **Elemente** hängen dagegen an der Datenquelle: über den dynamischen
    Adapter sind es `Dictionary<string, object>`, über einen DbContext kommt, was die Abfrage zurückgibt
    (typischerweise anonyme Typen). `column(…)` nimmt beides — ein handgeschriebener Element-Cast nicht.
  - Die **Konfiguration** (`DEFAULT` oder ein eigener Name) bündelt `using`-Anweisungen und Referenzen:
    `` `U(cfg)"using System.Linq;"; `` bzw. `` `R(cfg)"Assembly" ``. Beides sind **Anweisungen** — sie gehen
    nur im Modus *Script block*, nicht in einem Ausdruck. **`System`, `System.Linq` und
    `System.Collections.Generic` sind in jeder Konfiguration automatisch dabei**; `` `U `` braucht man nur
    für weitere Namensräume. **Der Name ist gross-/kleinschreibungsempfindlich**: `Default` und `DEFAULT`
    sind zwei verschiedene Konfigurationen — wer per `` `U `` etwas anmeldet und bei `` `E `` anders
    schreibt, meldet es an einer Konfiguration an, die niemand benutzt.

- **Ein Ausdruck darf nicht mit `{` beginnen** — das verbietet die Grammatik. Das Objektliteral ist aber
  genau das, was hier steht; der Renderer klammert es deshalb selbst ein. Wer den Modus *Script block*
  wählt, schreibt ohnehin `return { … };`. Der Modus wird in den Renderer-Einstellungen **gewählt** und
  nicht geraten: ein Block ohne `return` liefert `null`, und ein Block, der als Ausdruck ausgewertet wird,
  wirft nicht, sondern liefert still etwas Falsches.

**Aufbereitet** werden nur `type`, `labels` und `series`. **Alles andere wird durchgereicht**: jedes weitere
Feld wird gegen die `[Parameter]`-Eigenschaften von `MudChart<double>` geprüft (Schreibweise egal) und auf
den Zieltyp gebracht — `width`, `height`, `legendPosition`, `canHideSeries`, `matchBoundsToSize` und was
MudBlazor künftig dazulegt, ohne dass hier eine Liste gepflegt werden muss. Ein Objekt-Wert auf einem
Objekt-Parameter (`chartOptions`) wird rekursiv nach derselben Regel befüllt.

Diese Prüfung ist der Grund, warum das tragfähig ist: MudBlazor-Komponenten fangen unbekannte Attribute
über `UserAttributes` ab — ein verschriebenes `legendPositon` würde also **nicht** auffallen, sondern
wirkungslos als HTML-Attribut enden. Der Renderer beanstandet es stattdessen **in der Kachel** (beim
Speichern wird nur die Syntax geprüft, s. §25.9.1) und schreibt den Grund ins Log. Gesperrt sind
`chartType`/`chartLabels`/`chartSeries` (die kommen aus der Deklaration) und
`selectedIndex`/`selectedIndexChanged` (die verdrahtet der Renderer).

**Klick = Navigation.** Ein Beschriftungs-Eintrag darf statt Text ein Objekt sein:

```jsonc
"labels": [ { "text": "Offen", "navigateTo": "Orders?status=open" }, "Erledigt" ]
```

Klick auf Segment oder Legendeneintrag springt dann dorthin; ohne Ziel wird wie beim HTML-Template ein
`WidgetAction("select", <Text>)` ausgelöst, das die Anwendung über ihr `OnWidgetAction` bekommt. **Ziele
relativ angeben** (`Orders?...`, nicht `/Orders`): relative löst der Renderer gegen die Basis der Anwendung
auf, und genau die trägt den Mandanten-Präfix — ein root-absolutes Ziel ginge daran vorbei (BUG-PRE141).

Nicht zusammenpassende Längen von `labels` und `series[].data`, nicht-numerische Werte, unbekannte
Diagrammtypen: alles Beanstandungen, keine stillen Korrekturen. Eine Kategorie lautlos wegzulassen wäre die
schlechteste Auskunft — die Zahlen sähen richtig aus und wären es nicht.

#### 25.9.4 Unbekannter Schlüssel

Eine Kachel, deren `RendererKey` nicht registriert ist, zeigt **eine Fehlermeldung mit dem verlangten
Schlüssel und der Liste der verfügbaren** — kein stiller Rückfall auf Scriban. Eine Kachel, die nach einem
Tippfehler wortlos etwas anderes zeichnet, ist genau die Sorte Fehler, die später teuer wird.

---

## 26. Mehrstufige Workflow-Vorgänge in einem Dialog (opt-in, kein Schema-Change)

Der Aufgaben-Dialog kann jetzt **fortlaufend** arbeiten: nach dem Abschluss hängt er sich auf die nächste
Aufgabe derselben Instanz um und wartet, wenn dazwischen noch eine automatische Aktivität läuft. Damit
bekommt ein mehrstufiger Vorgang aus einem Modul heraus den Charakter eines Assistenten, statt den
Benutzer nach jedem Schritt in **Meine Aufgaben** zurückzuschicken.

**Nichts davon ist Pflicht.** Ohne Zutun verhält sich alles wie bisher: `MyTasks` öffnet den Dialog
unverändert für genau eine Aufgabe.

### 26.1 Aus einem Modul öffnen

```csharp
var parameters = new DialogParameters<UserTaskDialog>
{
    { d => d.InstanceId, instanceId }    // aus dem eigenen Start des Workflows
    // Continuous bleibt null = „das Modell entscheidet" (siehe unten).
};
await DialogService.ShowAsync<UserTaskDialog>("Kunde einrichten", parameters, EditDialogDefaults.Detail);
```

`TokenId` bleibt leer — das heißt „die nächste offene Aufgabe dieser Instanz" und deckt den Moment
direkt nach dem Start ab, in dem es noch keine gibt. Details, Auswahlregeln und der Wartezustand stehen in
`docs/Workflow-Integration-Guide.md` §24.

**Welche Schritte geführt werden, entscheidet das Modell** — zwei neue Felder am Aufgaben-Knoten, beide im
Editor unter *General*:

- **`RunsInAssistant`** (Schalter): dieser Schritt gehört zu einem geführten Abschnitt. Das ist der Grund,
  warum der Dialog-Parameter `Continuous` jetzt `bool?` ist und **null** in der Vorgabe: wer den
  Assistenten mittendrin schließt und die Aufgabe später aus *Meine Aufgaben* wieder öffnet, bekommt
  denselben geführten Ablauf zurück. Die Arbeitsliste setzt dafür nichts.
- **`EndsAssistant`** (CScript → `true`/`false`): mit dieser Aufgabe ist der geführte Abschnitt zu Ende.
  Ausgewertet beim Abschluss **nach** dem Übernehmen der Ergebniswerte, darf also von der Eingabe
  abhängen; ein Fehler faultet die Instanz nicht. Ohne das Feld endet der Abschnitt von selbst, sobald ein
  Schritt kein `RunsInAssistant` mehr trägt — nur wartet der Dialog dann erst auf diesen Schritt.

Bestehende Definitionen sind unberührt: ohne `RunsInAssistant` verhält sich jede Aufgabe wie bisher.

### 26.2 Empfohlen: den Weckruf verdrahten

Ohne Verdrahtung fragt der Dialog beim Warten in seinem eigenen Takt (3 s) nach — funktionsfähig, aber
träge. Mit Weckruf reagiert er sofort. Drei Teile:

1. **`ActivationSettings.UseEntityTracker: true`** — habt ihr schon (§7/§8). Registriert
   `IEntityWriteTracker<>`/`IEntityChangeSignal<>` offen generisch, damit gelten sie auch für den
   `WorkflowContext`.
2. **Den Interceptor am `WorkflowContext`** — das ist der eine Handgriff, der fehlt. Der WebPart-Konfigurator
   erreicht diesen Kontext nicht (er wird über einen `ContextOptionsLoader` gebaut). Neu dafür:
   `EntityWriteTrackerInterceptorOptionsLoader<TContext>` (in `ITVComponents.EFRepo`, neben
   `SaveInterceptorOptionsLoader`) — nach demselben Schema wie
   `SetCurrentTenantInterceptorOptionsLoader`: über den vorhandenen Loader legen, den **äußersten** Loader
   dem Kontext geben. Auch per Plugin-Konfiguration verwendbar (Ctor `(parent, IServiceProvider)`).
3. **Die Themen** — erledigt `AddWorkflowViews()` selbst (`AddWorkflowChangeSignal()`).

> **Der Weckruf trägt nur im eigenen Prozess.** Im Web-Only-Betrieb und mit dem `WorkflowWorkerService`
> im Web-Prozess reicht das. Läuft der Runner separat, bleibt der eigene Takt — dann `WaitTimeout` am
> Dialog grösszügiger setzen.

### 26.3 Sammelfenster für den Weckruf (optional, wirkt auf **alle** Empfänger)

Eine Meldung gilt einer Menge von **Tabellen**, nicht einer Zeile. Wer einen Schwung Zeilen bewegt (ein
Runner, ein Import), erzeugt damit einen Schwung Meldungen — und jeder Empfänger sieht ebenso oft nach.
Dagegen gibt es ein **Sammelfenster je Thema**: die erste Meldung eröffnet es, an seinem Ende ergeht der
Weckruf einmal.

**Nur der Weckruf wird gesammelt, nie der Zeitstempel.** `GetLastChange` bleibt synchron, die puffernden
Verbraucher (Navigation, Berechtigungen, FK-Beschriftungen) sehen eine Änderung beim nächsten Zugriff also
weiterhin sofort. Verzögert wird ausschließlich das aktive Benachrichtigen — deshalb sitzt die Bremse im
Signal und nicht im Interceptor.

Drei Wege, in dieser Rangfolge:

| Weg | Wie |
|---|---|
| **WebPart** (Security-Kontext) | `ActivationSettings.EntityChangeSignal` — `{"DefaultMilliseconds": 0, "RefreshSeconds": 0, "Topics": {"WorkflowProgress": 250}}` |
| **Code** | `services.Configure<EntitySignalDebounceSettings>(o => o.Topics["…"] = 250)` |
| **GlobalSettings** (Plugin-Hosts) | JSON-Eintrag **`EntityChangeSignal`** mit demselben Aufbau — **gewinnt** gegen beides |

Der GlobalSettings-Weg ist für Hosts, die ihre Kontexte über das Plugin-System bauen: dort gibt es keine
`Services.Configure`-Gelegenheit mehr, wohl aber die DB-getriebenen Einstellungen. Gelesen wird über einen
eigenen Scope — es ist damit eine **prozessweite** Einstellung, ausdrücklich keine je Mandant. Ist der
Eintrag unlesbar, bleibt es bei der Code-Einstellung (mit Log-Zeile).

**Nachladen ohne Neustart:** `RefreshSeconds` (in der Host-Einstellung, siehe unten) lässt den DB-Eintrag
im angegebenen Takt neu lesen — praktisch, solange an den Werten noch gedreht wird; im eingeschwungenen
Betrieb gehört er auf `0` (die Vorgabe), denn jeder Zyklus ist eine Abfrage, die sonst nie nötig wäre.

Drei Dinge dazu, die man wissen sollte:

- **Nur das erste Lesen ist synchron.** Der Aufrufer ist der Thread, der gerade gespeichert hat — dort
  gehört kein DB-Zugriff hin. Aufgefrischt wird nebenher; bis das durch ist, gilt die bisherige Fassung.
- **`RefreshSeconds` wird nie aus der Datenbank übernommen**, nur aus der Host-Einstellung. Ein Eintrag mit
  `0` würde sich sonst selbst aussperren: das Nachladen wäre aus, und die einzige Stelle, an der man es
  wieder einschalten könnte, würde nicht mehr gelesen.
- **Ins Log geht die Änderung, nicht der Zustand** — sonst liefe dieselbe Zeile im Takt durch und wäre
  genau dann wertlos, wenn man wissen will, ob eine Änderung gezogen hat.

Vorbelegt ist nur `WorkflowProgress` mit 250 ms (durch `AddWorkflowChangeSignal`, und nur falls der Host
nichts dazu gesagt hat). Alles andere — insbesondere `Security` und `Navigation` — meldet **sofort**, wie
bisher.

### 26.4 Umzug: `EntityChangeSignal<TContext>`

Die Implementierung ist von `…EntityFramework.TenantSecurity.Shared.Caching` nach
`ITVComponents.WebCoreToolkit.EntityFramework.Caching` gewandert — sie ist nicht security-spezifisch und war
dort für andere Kontexte nur über eine Security-Abhängigkeit erreichbar. **Vertrag, Registrierung und
Verhalten sind unverändert** (`IEntityChangeSignal` lag ohnehin schon neutral in `WebCoreToolkit/Caching`);
`UseEntityChangeSignal<TContext>()` gibt es weiterhin und setzt weiterhin die Security-/Navigations-Themen.
Betroffen ist nur, wer die **Klasse selbst** namentlich verwendet — dann das `using` anpassen.

---

## 27. Export-Profile für den System-Config + Schutz vor Teil-Importen (opt-in, kein Schema-Change)

Der System-Config-Export (§17) war bisher alles-oder-nichts: ein Download, der jedes Mal die komplette
Grundkonfiguration mitnahm — auch wenn man nur den Billing-Katalog oder (künftig) die Hilfe-Inhalte von einem
System aufs andere bringen wollte. Neu gibt es **benannte Profile**, die entscheiden, welche Teile überhaupt
erzeugt werden.

### 27.1 Der Grund, warum das mehr ist als ein Filter

`PerformCompareInternal` verglich die Basis-Sektionen **bedingungslos**. Eine Datei ohne Basisdaten hätte für
jede Sektion „alles, was da ist, ist zu viel" ergeben — also Lösch-Einträge für Plugins, Permissions,
GlobalRoles, Navigation und Settings, im Dialog **vorausgewählt**. Ein Teil-Export wäre damit beim Einspielen
eine Löschbombe gewesen.

Abgesichert wird das doppelt:

- **`OmitBasicData` steht in der Datei.** Das ist eine Absichtserklärung, keine aus fehlenden Daten
  abgeleitete Vermutung — ein Serializer, der leere Arrays statt `null` schreibt, würde die abgeleitete
  Variante still aushebeln, und genau dann wäre der Schaden da.
- **Jede Basis-Sektion hat jetzt einen Null-Guard.** Fehlt eine Sektion in der hochgeladenen Datei, wird sie
  übersprungen statt gegen eine leere Menge verglichen. Bisher hatten diesen Schutz nur die beiden jüngsten
  Sektionen (`ExternalOAuthServices`, `TemplateModules`); jetzt gilt er für alle. Das härtet nebenbei auch den
  Import älterer Exportdateien.

Für die Extension-Sektionen galt das schon immer: `CompareExtensions` läuft über die **hochgeladenen**
Sektionen, fehlende bleiben unberührt.

Enthält eine Datei trotz `OmitBasicData` Basisdaten (von Hand editiert), gewinnt das Flag — aber nicht still:
das gibt einen Warn-Eintrag im Diff und eine Zeile im Log.

### 27.2 Profile konfigurieren

Profile kommen aus `ConfigExportProfileOptions` (neu in `ITVComponents.EFRepo.DataSync`), z.B. aus der
`appsettings.json`:

```json
"ConfigExportProfiles": {
  "Profiles": {
    "Help":    { "ActiveExtensions": [ "help" ],    "OmitBasicData": true, "Description": "Nur Hilfe-Inhalte" },
    "Billing": { "ActiveExtensions": [ "billing" ], "OmitBasicData": true, "Description": "Nur Abo-Katalog" }
  }
}
```

```csharp
services.Configure<ConfigExportProfileOptions>(configuration.GetSection("ConfigExportProfiles"));
```

- `ActiveExtensions` benennt **Section-Keys** (der stabile Polymorphie-Diskriminator aus der Registrierung),
  **nicht** Handler-Typnamen — die brechen beim Verschieben oder Umbenennen von Assemblies.
- `ActiveExtensions: null` (weglassen) = **alle** registrierten Extensions, auch später hinzukommende.
  Ein leeres Array = keine.
- **Bewusst nicht** über GlobalSettings/Datenbank: die Profile braucht man gerade dann, wenn man die
  Konfiguration eines Systems exportiert — eine Neuinstallation hätte noch keine Settings, aus denen sie zu
  lesen wären.

Eingebaut und immer vorhanden sind `Full` (alles, das bisherige Verhalten) und `BasicOnly` (Grunddaten ohne
beigesteuerte Sektionen). **Wer nichts konfiguriert, bekommt genau das bisherige Verhalten** — nur eben mit
einer Auswahlliste, die zwei Einträge hat.

### 27.3 Was in der UI passiert

`Util/AssemblyDiagnostics` → *Configuration exchange* hat neben dem Download-Knopf eine Auswahlliste mit den
Profilen. Das gewählte Profil hängt am Dateibezeichner (`sysCfg@Help`), landet als Herkunftsvermerk in der
Datei (`ExportProfile`) und im Dateinamen (`System_Help.json` statt dreimal `System.json`).

**Der Upload bleibt auf dem nackten `sysCfg`** — was verglichen wird, entscheidet der Inhalt der Datei und
nicht, was beim Download ausgewählt war. Bisher teilten sich Download und Upload denselben Options-Wert; wer
`ConfigDownloadIdentifier` gesetzt hat, muss nichts tun (der Profil-Anteil wird für den Upload automatisch
abgeschnitten), kann den Hinweis aber über den neuen `ConfigUploadIdentifier` explizit setzen.

Ein unbekannter Profilname führt zu einem vollständigen Export **und einer Warnung im Log** — nicht zu einer
stillen Teilmenge.

---

## 28. Hilfesystem im System-Config-Export (opt-in, kein Schema-Change)

Der Hilfe-Baum samt Inhalten und die Ressourcen-Bibliothek reisen jetzt als eigene Sektion (`help`) im
System-Config mit — Dokumentation lässt sich damit von der Entwicklungs- in die Produktivumgebung bringen,
ohne sie abzutippen. Mechanik wie bei Billing (§17): `IConfigExtension`, typisiert-polymorphe Sektion, Apply
über den generischen `SimpleDataApplyer`.

**Einschalten** — eines von beidem:

```json
"…EntityFramework.HelpSystem.WebPartInit": { "ActivateHelpConfigExport": true }
```

oder direkt `services.AddHelpConfigExtension()` beim Startup. Voraussetzung ist wie bei Billing, dass der
Context des Config-Handlers `IHelpSystemContext` implementiert und das Handler-Plugin den `IServiceProvider`
im Konstruktor bekommt. Ist der Context ohne Hilfe-Tabellen, beschreibt und vergleicht die Sektion nichts —
kein Crash.

Zusammen mit den Profilen aus §27 ergibt das den eigentlich nützlichen Fall:

```json
"Help": { "ActiveExtensions": [ "help" ], "OmitBasicData": true }
```

→ eine Datei, die **nur** die Hilfe enthält und beim Einspielen die Systemkonfiguration nicht anfasst.

**Was übertragen wird:** Themen (`Slug` als Schlüssel, Baum über `ParentSlug`), ihre Inhalte je Kultur
(Titel + Markdown-Rumpf), die Ordner der Ressourcen-Bibliothek, die Ressourcen-Einträge selbst
(Name, Beschreibung, Art, Ordner) — **und die Dateien samt Inhalt**.

### 28.1 Die Inhalte der Ressourcen

Die Bytes reisen **inline als Base64** mit. Das bläht sie um rund ein Drittel auf, und weil die Diff-Antwort
durch den Browser zurückläuft, wandert jede geänderte Datei zweimal. Deshalb greifen Grenzen — und deshalb
lohnt sich das Profil aus §27, das die Hilfe von den täglichen Konfigurations-Abgleichen trennt:

```json
"…EntityFramework.HelpSystem.WebPartInit": {
  "ActivateHelpConfigExport": true,
  "Contents": { "IncludeResourceContents": true, "MaxFileBytes": 2097152, "MaxTotalBytes": 20971520 }
}
```

Vorbelegt sind 2 MB je Datei und 20 MB insgesamt; `IncludeResourceContents` steht auf `true` (wer die
Hilfe-Sektion einschaltet, will die Bilder dabeihaben). Was über einer Grenze liegt, wird **gemeldet, nicht
still weggelassen**: der Diff bekommt einen gesammelten Eintrag mit Datei und Grund.

Drei Dinge, die man dazu wissen sollte:

- **Nur der eingebaute EF-Blob-Store.** Erkannt wird das nicht an der Registrierung, sondern daran, ob der
  Inhalt in `HelpResourceBlob` liegt. Wer einen eigenen `IHelpResourceStore` (Azure, DMS) betreibt, bekommt
  Metadaten plus die Meldung, welche Dateien deshalb fehlen — kein Crash, keine halbe Ressource.
- **Der `FileIdentifier` wird beim Import neu vergeben.** Er ist eine opake Kennung des jeweiligen Systems;
  die aus der Datei wird bewusst verworfen, damit es keine Kollision mit einem bestehenden Blob gibt.
- **SHA-256 entscheidet über das Schreiben.** Ein Blob mit gleichem Hash wird nicht angefasst; der Hash steht
  je Datei im Export. Er spart nicht die Übertragung, aber das unnötige Neuschreiben — und hält den Dialog
  frei von Rauschen.

Gelöschte Ressourcen räumen ihre Blobs mit ab (die Kennung ist kein Fremdschlüssel, sonst blieben Waisen).

### 28.2 Anzeige im Diff-Dialog: `ChangeDetail` trennt Anzeige und Nutzlast

Damit ein Base64-Block den Vergleichsdialog nicht unbrauchbar macht, hat `ChangeDetail` drei neue,
optionale Felder: **`DisplayValue`** und **`DisplayCurrentValue`** (was der Dialog anstelle des Rohwerts
zeigt) sowie **`ReadOnly`**. `NewValue` bleibt die Nutzlast, die angewendet wird — der `SimpleDataApplyer` ist
unverändert. Eine Datei erscheint damit als `logo.png · 412 KB · image/png · sha256 3f9a1b2c…` statt als
Zeichenkolonne, und das Feld ist gesperrt: **ein Tastendruck in einem Base64-Feld würde die Datei zerstören.**

Alle drei Felder sind `null`/`false`-vorbelegt, bestehende Extensions verhalten sich unverändert. Wer eigene
`IConfigExtension`s schreibt, kann sie ohne Vertragsänderung nutzen — `MakeDetail(...)` liefert das
`ChangeDetail`, die Felder werden danach gesetzt. Für lange Markdown-Rümpfe ist derselbe Weg offen.

**Ordner werden angelegt, aber nie geändert oder gelöscht.** Sie tragen nur Name und Elternteil — über einen
Pfad-Schlüssel sind „umbenannt" und „verschoben" nicht von „ein anderer Ordner" zu unterscheiden, und ein
Löschen würde fremde Ressourcen still an die Wurzel schieben. Da Ordner reine Ordnung sind (eine Ressource
wird immer über ihren flachen, global eindeutigen Namen angesprochen), ist das die harmlose Seite.

**Reihenfolge ist eingebaut:** Themen werden von der Wurzel abwärts angelegt und von den Blättern aufwärts
gelöscht, Ordner von flach nach tief — sonst scheitert der Elternteil-Verweis bzw. der Fremdschlüssel.

---

## 29. Workflows laufen von selbst an: Nachrichten-Start und Zeitplan-Start — **Pflicht: 1 Tabelle**

Bisher konnte eine Instanz nur *von aussen* entstehen: durch Code, den Start-Dialog oder einen
Subworkflow-Aufruf. Neu deklariert der **Start-Knoten** selbst, woraufhin sein Prozess anläuft — durch
eine eintreffende Nachricht oder nach einem Zeitplan. Der Unterschied zum bekannten Wartepunkt ist der
Kern: der weckt einen bereits *laufenden* Zweig, hier entsteht der Vorgang überhaupt erst.

### 29.1 Tabelle anlegen

Die Auslöser werden beim Speichern einer Definition aus ihr abgeleitet und in einer eigenen Tabelle
geführt. Sie muss sein: die beiden Fragen dahinter sind nur als Abfrage zu beantworten — bei *jeder*
eintreffenden Nachricht sämtliche Definitions-JSONs auszupacken, wäre eine Last, die mit der Zahl der
Prozesse wächst, und „welcher Zeitplan ist jetzt fällig?" liesse sich gar nicht indizieren.

**Der Regelweg ist die mitgelieferte Migration** — anders als beim Security-Kontext (§20, §24, §25) hat
der `WorkflowContext` gepflegte Migrationsprojekte, und der Snapshot ist sauber (die erzeugte Migration
enthält ausschliesslich diese Tabelle, keine Fremd-Änderungen):

```
dotnet ef database update -p ITVComponents.Workflow.EntityFramework.SqlServer   -s <euer Host>
dotnet ef database update -p ITVComponents.Workflow.EntityFramework.PostgreSql  -s <euer Host>
```

Migration: `20260819072748_WorkflowStartTriggers` (SQL Server) bzw. `20260819072826_WorkflowStartTriggers`
(PostgreSQL).

Das folgende SQL ist die **gleichwertige Handarbeit** für Installationen, die kein `database update`
fahren:

SQL Server:

```sql
CREATE TABLE [WorkflowStartTriggers] (
    [TriggerKey]             int            IDENTITY(1,1) NOT NULL,
    [DefinitionKey]          int            NOT NULL,
    [DefinitionId]           nvarchar(450)  NULL,
    [DefinitionVersion]      int            NOT NULL,
    [TenantId]               nvarchar(450)  NULL,
    [NodeId]                 nvarchar(450)  NULL,
    [Kind]                   int            NOT NULL,
    [SignalName]             nvarchar(450)  NULL,
    [Mode]                   int            NOT NULL,
    [AdoptCorrelationKey]    bit            NOT NULL,
    [Pattern]                nvarchar(450)  NULL,
    [VariablesJson]          nvarchar(max)  NULL,
    [SkipWhilePreviousRuns]  bit            NOT NULL,
    [NextDueUtc]             datetime2      NULL,
    [LastRunUtc]             datetime2      NULL,
    [LastInstanceId]         nvarchar(450)  NULL,
    [LeaseOwner]             nvarchar(450)  NULL,
    [LeaseUntilUtc]          datetime2      NULL,
    CONSTRAINT [PK_WorkflowStartTriggers] PRIMARY KEY ([TriggerKey]),
    CONSTRAINT [FK_WorkflowStartTriggers_WorkflowDefinitions_DefinitionKey]
        FOREIGN KEY ([DefinitionKey]) REFERENCES [WorkflowDefinitions] ([DefinitionKey]) ON DELETE CASCADE
);

CREATE INDEX [IX_WorkflowStartTriggers_Kind_SignalName]
    ON [WorkflowStartTriggers] ([Kind], [SignalName]);
CREATE INDEX [IX_WorkflowStartTriggers_Kind_NextDueUtc]
    ON [WorkflowStartTriggers] ([Kind], [NextDueUtc]);
CREATE INDEX [IX_WorkflowStartTriggers_TenantId_DefinitionId]
    ON [WorkflowStartTriggers] ([TenantId], [DefinitionId]);
CREATE INDEX [IX_WorkflowStartTriggers_DefinitionKey]
    ON [WorkflowStartTriggers] ([DefinitionKey]);
```

PostgreSQL:

```sql
CREATE TABLE "WorkflowStartTriggers" (
    "TriggerKey"            integer GENERATED BY DEFAULT AS IDENTITY,
    "DefinitionKey"         integer                  NOT NULL,
    "DefinitionId"          text                     NULL,
    "DefinitionVersion"     integer                  NOT NULL,
    "TenantId"              text                     NULL,
    "NodeId"                text                     NULL,
    "Kind"                  integer                  NOT NULL,
    "SignalName"            text                     NULL,
    "Mode"                  integer                  NOT NULL,
    "AdoptCorrelationKey"   boolean                  NOT NULL,
    "Pattern"               text                     NULL,
    "VariablesJson"         text                     NULL,
    "SkipWhilePreviousRuns" boolean                  NOT NULL,
    "NextDueUtc"            timestamp with time zone NULL,
    "LastRunUtc"            timestamp with time zone NULL,
    "LastInstanceId"        text                     NULL,
    "LeaseOwner"            text                     NULL,
    "LeaseUntilUtc"         timestamp with time zone NULL,
    CONSTRAINT "PK_WorkflowStartTriggers" PRIMARY KEY ("TriggerKey"),
    CONSTRAINT "FK_WorkflowStartTriggers_WorkflowDefinitions_DefinitionKey"
        FOREIGN KEY ("DefinitionKey") REFERENCES "WorkflowDefinitions" ("DefinitionKey") ON DELETE CASCADE
);

CREATE INDEX "IX_WorkflowStartTriggers_Kind_SignalName"
    ON "WorkflowStartTriggers" ("Kind", "SignalName");
CREATE INDEX "IX_WorkflowStartTriggers_Kind_NextDueUtc"
    ON "WorkflowStartTriggers" ("Kind", "NextDueUtc");
CREATE INDEX "IX_WorkflowStartTriggers_TenantId_DefinitionId"
    ON "WorkflowStartTriggers" ("TenantId", "DefinitionId");
CREATE INDEX "IX_WorkflowStartTriggers_DefinitionKey"
    ON "WorkflowStartTriggers" ("DefinitionKey");
```

Ohne die Tabelle schlägt jedes Speichern einer Workflow-Definition mit *„Invalid object name
'WorkflowStartTriggers'"* fehl — der Aufbau der Auslöser hängt daran.

**Nachträglich befüllen:** die Tabelle entsteht leer. Bestehende Definitionen bekommen ihre Auslöser,
sobald sie **einmal gespeichert** werden (Designer öffnen, speichern). Solange sie keine deklarieren, ist
das ohnehin gegenstandslos.

### 29.2 Was am Start-Knoten neu ist

**Nachrichten-Start** — Name der Nachricht plus die Frage, wie mit einer bereits laufenden Instanz
umzugehen ist. Drei Möglichkeiten, weil alle drei fachlich vorkommen:

| Modus | Verhalten | Wofür |
|---|---|---|
| `AlwaysStart` (Vorgabe) | legt immer eine neue Instanz an; wartende Empfangsknoten bleiben unberührt | „jede Bestellung ist ein neuer Vorgang" |
| `CorrelateOrStart` | hat die Nachricht eine passend korrelierte wartende Instanz erreicht, entsteht keine neue | „Vorgang fortsetzen oder eröffnen" |
| `StartIfNoneRunning` | verwirft die Nachricht, wenn schon eine Instanz mit demselben Korrelationsschlüssel läuft | Riegel gegen Doppelanlagen |

Die Vorgabe ist bewusst die vorhersagbare: was geschieht, steht im Modell und hängt nicht davon ab, ob
zufällig gerade jemand wartet.

**Zeitplan-Start** — ein Muster im Format der `TimeTable` (siehe §29.4), dazu feste Startwerte (es füllt
ja niemand ein Formular aus) und wahlweise „überspringen, solange der vorige Lauf noch läuft".

**Beides gilt für den Mandanten der Definition.** Öffentliche Definitionen lösen ausdrücklich **nicht**
von selbst aus — sie gehören allen, und „für alle einmal starten" wäre eine völlig andere Zusage. Wer
einen zeitgesteuerten Standardprozess braucht, legt ihn im eigenen Mandanten an.

### 29.2a Ein Auslöser schliesst den Start von Hand nicht aus

Ein Start-Knoten mit Auslöser verhält sich beim **manuellen** Start wie jeder andere: die Definition steht
weiterhin in *Instanzen → Neue Instanz*, und der Auslöser sagt nur zusätzlich, **wer sie ausserdem**
anstösst. Die Startmaske (`StartNode.FormFields`) hängt nicht am Auslöser — was ein Message-Start als
Nutzdaten mitbringt, kann bei einem Start von Hand also über dieselben Felder eingegeben werden. Auch der
Korrelationsschlüssel lässt sich im Start-Dialog setzen, damit spätere Nachrichten den Vorgang
wiederfinden.

**Eine Stelle brauchte dafür Nachhilfe:** die festen Startwerte eines Zeitplans
(`ScheduleStart.Variables`) bekam nur der zeitgesteuerte Lauf. Ein zeitgesteuerter Einstieg hat meist gar
keine Felder — der Dialog zeigte also nichts, und der Prozess startete ohne Werte, die er braucht (je nach
Signatur lief er anders oder faultete). Der Start-Dialog zeigt sie jetzt: deklarierte Felder werden damit
**vorbelegt**, alle übrigen erscheinen unter *From the schedule* als eigene, änderbare Zeilen. Wer von Hand
startet, sieht damit, womit ein zeitgesteuerter Lauf starten würde — und kann es anpassen, denn genau
deshalb startet er von Hand.

### 29.3 Betrieb: der Anspruch ist hier Pflicht, nicht Kür

Fällige Zeitpläne werden vom Runner bzw. vom Web-Worker aufgegriffen — mit einem **Anspruch** auf der
Zeile, wie bei den Timern. Der Unterschied ist wichtig: bei einem Timer verhindert der Anspruch nur
doppelte Arbeit, die Zusicherung trägt der versionsgeprüfte Commit. Ein *Start* hat nichts dergleichen —
es gibt noch keine Instanz, deren Version jemanden ausbremsen könnte. **Ohne Anspruch liefe derselbe
Mahnlauf in einem Verbund aus drei Knoten dreimal an.** Wer mehrere Prozesse betreibt, muss also nichts
konfigurieren, aber wissen, dass die Tabelle diese Rolle hat.

Zwei Eigenschaften, die im Betrieb auffallen:

- **Verpasste Termine werden einmal nachgeholt, nicht n-mal.** War der Dienst drei Tage aus, läuft der
  tägliche Auftrag einmal nach und ist dann wieder im Takt.
- **Ein gescheiterter oder übersprungener Start schiebt den Termin trotzdem weiter.** Sonst bliebe der
  Auslöser fällig und liefe im Takt des Runners heiss — aus einem einzelnen Fehler würde eine Last, die
  die Anlage lahmlegt. Beides steht im Protokoll der Instanz bzw. im System-Log.

### 29.4 Das Zeitplan-Muster

Verwendet wird die vorhandene `TimeTable` — **umgezogen** von
`ITVComponents.ParallelProcessing.TaskSchedulers` nach `ITVComponents.Scheduling` (siehe
Breaking-Change-Tabelle). Sie kann drei Dinge, die Cron nicht kann: den letzten Tag eines Monats (`-1`),
„jede n-te Woche, und zwar die geraden" (Modulus mit gewünschtem Rest) und einen festen Anker, ab dem
gerechnet wird.

**Gerechnet wird in Ortszeit, gespeichert in UTC.** Das ist Absicht: „jeden Tag um 8" meint acht Uhr vor
Ort, auch nach der Umstellung auf Sommerzeit — sonst verschöbe sie jeden Termin um eine Stunde.

Beispiel: `d20200101080001` = täglich um 08:00, gerechnet ab dem 01.01.2020. Ein angehängtes `t` bedeutet
„der erste Lauf sofort" und greift genau einmal — nämlich solange der Zeitplan noch nie gelaufen ist.
Damit lässt sich „soll beim Hochfahren einmal durchlaufen" abbilden, ohne dass daraus Dauerfeuer wird.

Das Muster ist ein **Maschinenformat**; niemand tippt so etwas von Hand. Im Knoten-Editor steht deshalb
eine Prüfung mit Klartext-Meldung dahinter — sie fängt nicht nur unlesbare Muster ab, sondern auch solche,
die formal stimmen und trotzdem nie zutreffen (der 30. Februar). Ein zusammenklickbarer Muster-Designer
ist in Arbeit.

---

## 30. Nachrichten-Empfang am Schritt (opt-in, kein Schema-Change)

Eine laufende Aufgabe liess sich bisher nur über eine **Frist** unterbrechen (`BoundaryTimerNode`), nicht
durch ein Ereignis. „Der Kunde storniert, während die Prüfung offen ist" war deshalb nicht modellierbar:
der Empfänger hätte an einem Wartepunkt stehen müssen — und genau das tut er nicht, es wird ja gearbeitet.

Neu gibt es dafür den `BoundaryMessageNode`. Er hängt wie der Fristen-Timer an einem Schritt, an dem ein
Token parkt (dieselbe Regel, dieselbe Quelle: `BoundaryTimerNode.CanHost`), und feuert, wenn dort eine
Nachricht eintrifft:

- **nicht unterbrechend** (Vorgabe): löst einen Nebenpfad mit einer Kopie des Variablen-Stands aus, der
  Hauptfluss arbeitet unverändert weiter;
- **unterbrechend**: das Haupt-Token nimmt die Kante, der Schritt gilt als abgebrochen, eine wartende
  Aufgabe verschwindet aus der Arbeitsliste.

**Ein Unterschied zum Fristen-Timer, der in der Modellierung zählt:** ein nicht unterbrechender Empfang
bleibt nach dem Feuern **scharf** und löst wieder aus. Eine Nachricht kann beliebig oft kommen („der Kunde
fragt erneut nach"), und der Schritt soll nach der ersten nicht taub werden — die Fristenliste eines Timers
läuft dagegen einmal durch. `CountVariable` zählt die Auslösungen mit, im Scope des Nebenpfads.

Wohin die **Nutzdaten** der Nachricht fliessen, hängt an derselben Unterscheidung: beim Nebenpfad in dessen
Kopie (was dort entsteht, verstellt den Hauptfluss nicht), beim Abbruch in den Hauptfluss — er läuft
weiter, und der Grund des Abbruchs wird dort gebraucht.

Kein Schema-Change: der Knoten lebt im Definitions-JSON, und die Token-Felder (`BoundaryOwnerTokenId`,
`BoundaryIteration`) gibt es seit dem Fristen-Timer. Der Empfang bekommt beim Parken ein eigenes wartendes
Token — damit findet ihn die gewöhnliche Zustellung von selbst, und die Regeln, wen eine Nachricht
erreicht (Korrelation, Nachricht gegen Rundruf), gelten unverändert auch hier.

---

## 31. Vorgänge anhalten und fortsetzen — **Pflicht-Migration (3 Spalten)**

Ein laufender Vorgang liess sich bisher nur **abbrechen**. Neu lässt er sich **anhalten**: kein Runner
treibt ihn mehr voran, er bleibt stehen, wo er steht. In der Instanz-Übersicht steht ein Knopf dafür
(`Workflow.Operate`), und beim Anhalten fragt eine kleine Maske nach dem **Grund** — ein angehaltener
Vorgang ohne Begründung ist von einem hängenden nicht zu unterscheiden.

**Was weiterläuft, ist der wichtigere Teil:** Nachrichten und Signale kommen weiterhin an und machen
Tokens aktiv, Fristen bleiben gesetzt und werden fällig. Nur *ausgeführt* wird nichts. Die umgekehrte
Auslegung — nichts mehr annehmen — klingt gründlicher, verlöre aber genau die Ereignisse, die während der
Pause eintreffen. Beim Fortsetzen läuft alles los, was sich angesammelt hat.

**Umgesetzt als eigenes Feld, nicht als weiterer `WorkflowStatus`-Wert.** Der Status trägt den
Lebenszyklus und wird im Vortrieb ständig auf „läuft" zurückgesetzt — ein Status-Wert wäre dort still
wieder aufgehoben worden. Eine angehaltene Instanz behält deshalb ihren Status und trägt das Kennzeichen
daneben; die Übersicht zeigt beides.

Eine **gescheiterte** Instanz lässt sich ausdrücklich anhalten: sie ist nicht beendet, sondern hängt — und
ist damit genau der Fall, in dem man sie aus dem Aufgriff nehmen will, bis jemand hingesehen hat. Eine
abgeschlossene oder abgebrochene dagegen nicht.

Migration: `SuspendAndFaultCode` (SQL Server und PostgreSQL, Snapshot geprüft) — legt
`WorkflowInstances.Suspended`, `SuspendedReason` und `FaultCode` (§32) an und ersetzt den Index
`IX_WorkflowInstances_Status_Priority` durch `IX_WorkflowInstances_Status_Suspended_Priority`. Die Spalte
steht mit im Index, weil **jede** Aufgriffs-Abfrage danach filtert.

---

## 32. Fehler-Code: nach der Fehlerart verzweigen statt nach dem Text

Ein gescheiterter Subworkflow gab dem Aufrufer bisher nur eine **Meldung** mit. Wer danach verzweigen
wollte, musste sie parsen — und der Ablauf hing damit an einer Formulierung, die jederzeit jemand
umschreibt oder übersetzt.

Neu gibt es neben der Meldung einen **Code**: einen kurzen, stabilen Schlüssel der Fehler*art*.

- Eine Aktivität meldet ihn mit `ctx.Fail(message, code)` — z.B. `Fail("Kreditlinie erschöpft",
  "CreditDenied")`. Die bisherige Form `Fail(message)` bleibt unverändert gültig.
- Am **Aktivitäts**- und am **Subworkflow-Aufruf**-Knoten gibt es dafür das neue Feld *Error code →
  variable* (neben *Error message → variable*).
- Ohne Fehler-Ausgang landet der Code am Fault der Instanz (`FaultCode`) — **und genau darüber kommt die
  Fehlerart aus einem Subworkflow heraus**: das Kind faultet mit Code, der Aufrufer liest ihn über sein
  *Error code → variable* und verzweigt am XOR-Gateway darauf.

Eine **abgestürzte** Aktivität lässt den Code leer — sie hat keine Fehlerart gemeldet und behauptet auch
keine. Der Code wird bei jedem Fehlerdurchlauf gesetzt, auch auf leer: sonst stünde beim zweiten Anlauf
noch der Code des ersten in der Variable, und die Verzweigung folgte einem Fehler, den es nicht mehr gibt.
`RetryFaulted` räumt ihn mit dem Fault zusammen ab.

Additiv, kein Breaking Change; Spalte `FaultCode` kommt mit der Migration aus §31.

---

## 33. Kommentare am Vorgang — **Pflicht-Migration (1 Tabelle)**

Eine Rückfrage („warum wurde das abgelehnt?"), ein Vermerk, eine Begründung: dafür gab es bisher keinen
Ort. Neu hat jeder Vorgang einen **Gesprächsfaden**, sichtbar und beschreibbar unter der Aufgaben-Maske
(aufklappbar; steht schon etwas drin, klappt er von selbst auf).

**Der Faden hängt am Vorgang, nicht an der Aufgabe** — an der Aufgabe steht nur nachrichtlich, aus welchem
Schritt heraus ein Kommentar kam. Das ist der Punkt: eine Aufgabe verschwindet mit ihrem Abschluss, und wer
später fragt, warum so entschieden wurde, sucht am Vorgang. Gezeigt wird deshalb immer der ganze Faden, auch
was an längst erledigten Schritten entstanden ist.

Es genügt `Workflow.Tasks`; wer die Aufgabe bearbeiten darf, darf kommentieren. Der Vorgang wird vor dem
Schreiben im **eigenen Mandanten** nachgewiesen — eine erratene Instanz-Id genügt nicht.

**Die Engine kennt Kommentare nicht** (wie schon die weiche Sperre): ein Kommentar ist kein Prozess-Zustand,
kein Ablauf hängt von ihm ab, keine Bedingung liest ihn. Wäre er Teil der Instanz, reiste er durch jeden
Zweig-Commit und müsste bei jedem Versionskonflikt mitgeführt werden — für etwas, das niemand auswertet.

Migration: `WorkflowComments` (beide Provider) — neue Tabelle mit Fremdschlüssel auf `WorkflowInstances`
(kaskadierend) und Index auf `(InstanceId, CreatedUtc)`.

**Anhänge sind bewusst nicht dabei.** Dateien brauchen den Datei-Handler samt Ablage, Grössengrenzen und
eigenen Berechtigungen; das gehört nicht als Anhängsel an die Kommentare, sondern ist ein eigener Schritt.

---

## 34. Nachbereitungs-Haken für eigene Aufgaben-Masken (opt-in, kein Schema-Change)

Aus dem MLM beantragt und umgesetzt. `IUserTaskView.ResolveActivityAsync` läuft **vor** dem Abschluss —
wer dort Fachdaten schreibt, schreibt sie auch dann, wenn der Abschluss anschliessend scheitert
(Versionskonflikt, „war schon erledigt") oder der Benutzer den Dialog schliesst, und muss die Doublette
danach selbst wieder einfangen.

Neu gibt es `PostResolveActivityAsync(UserTaskCompletionResult result)` — gerufen, **nachdem** feststeht,
was der Abschluss bewirkt hat.

- **Als Default-Interface-Methode**, nicht als Pflichtpunkt: der Vertrag wird beim Übersetzen erzwungen,
  ein weiterer Pflicht-Member bräche jede bestehende Maske. Wer ihn nicht braucht, merkt nichts.
- **Das Ergebnis geht mit** und ist nicht zu ignorieren: bei `AlreadyCompleted` hat jemand anderes
  abgeschlossen, und die Maske darf dann nicht auch noch schreiben.
- **Ein Scheitern hält nichts auf**, wird dem Benutzer aber angezeigt und bleibt stehen, bis er es
  wegklickt (die Aufgabe ist erledigt, der Prozess läuft weiter — das ist nicht mehr rückgängig zu
  machen). Geschluckt tauschte man eine sichtbare Doublette gegen eine unsichtbare Lücke.

Gerufen wird am **einen** Abschlussweg (`UserTaskDialog.CompleteAsync`) und **vor** dem Umhängen im
geführten Ablauf — danach zeigt der Mantel auf eine andere Maske oder auf gar keine.

**Was der Haken nicht ist:** ein Ersatz für eine eigene Aktivität im Prozess. Wenn ein späterer Schritt die
Daten liest oder eine Verzweigung davon abhängt, gehört das Schreiben in den Prozess — nur dort greifen
dessen Wiederholung und Fehlerbehandlung.

---

## 35. Muster-Designer für Zeitpläne (opt-in, kein Schema-Change)

Das Zeitplan-Muster aus §29.4 ist ein Maschinenformat. Neu steht im Start-Knoten-Editor ein Kalender-Knopf
am Feld, der es **zusammenklicken** lässt: Periode, Anker, Wochentage/Monatstage/Monate als Chips (der
**letzte Tag des Monats** ausdrücklich als eigene Auswahl — das, was Cron nicht kann), Takt, das
„sofort"-Kennzeichen, dazu eine Vorschau der nächsten Termine in Ortszeit. Das Textfeld bleibt daneben:
wer das Format kennt, tippt schneller, und ein kopiertes Muster lässt sich einfügen.

Zerlegt und zusammengesetzt wird über `ITVComponents.Scheduling.SchedulePattern` — **dieselbe Quelle wie
die Auswertung**, damit Editor und Laufzeit nicht auseinanderlaufen.

**Ein Nebeneffekt, den man kennen muss:** dabei ist aufgefallen, dass der Muster-Regex **nicht verankert**
ist. `Regex.Match` greift deshalb den kürzesten passenden Anfang und lässt den Rest liegen — ein Muster mit
vertauschten Teilen (etwa der Takt vor den Wochentagen) wurde bisher klaglos als ein anderes, kürzeres
gelesen, und der Plan lief zu einer anderen Zeit, als dastand. Die Zerlegung verankert jetzt selbst, und
`ScheduleEvaluator.TryValidate` prüft das mit. **Folge:** steht in einer bestehenden Definition ein solches
halb gelesenes Muster, meldet der Validator es künftig als Fehler. Das ist richtig — es tut heute schon
etwas anderes als beabsichtigt —, kann eine Definition aber beim nächsten Speichern als fehlerhaft
markieren.

---

## 36. Anhänge am Vorgang — **Pflicht-Migration (2 Tabellen)**

Der Gesprächsfaden aus §33 nimmt jetzt auch **Dateien** auf: hochladen, herunterladen, den eigenen wieder
entfernen. Sichtbar im selben aufklappbaren Bereich unter der Aufgaben-Maske.

**Nicht über den `IFileHandler` des Toolkits.** Dessen Vertrag ist auf den Upload-*Endpunkt*
zugeschnitten — er nimmt eine Datei entgegen und verarbeitet sie, gibt aber **keine Kennung zurück**, mit
der sie sich später wieder lesen liesse. Genau die braucht ein Anhang. Stattdessen dasselbe Muster wie im
Hilfesystem: eine schmale Ablage-Abstraktion (`IWorkflowAttachmentStore`) mit eingebauter Umsetzung in der
Datenbank. Wer seine Dateien woanders haben will (Dateisystem, Objektspeicher), registriert eine eigene
Umsetzung — Beschreibung und Oberfläche bleiben, wie sie sind.

**Beschreibung und Inhalt liegen getrennt** (`WorkflowAttachments` / `WorkflowAttachmentBlobs`): eine Liste
von Anhängen zeigt Namen und Grössen und soll dabei garantiert keine Dateien aus der Datenbank ziehen.

- **Grösse:** `WorkflowViewsOptions.MaxAttachmentBytes`, vorbelegt 10 MB. **0 oder kleiner schaltet Anhänge
  ab** — dann erscheint auch kein Knopf dafür.
- **Löschen nur den eigenen.** Fremde Belege aus einem Vorgang zu entfernen ist eine andere Handlung als
  den eigenen Fehlgriff zu korrigieren; dafür gibt es hier bewusst keinen Weg.
- Es genügt `Workflow.Tasks`; Vorgang und Mandant werden vor jedem Zugriff nachgewiesen.

Migration: `WorkflowAttachments` (beide Provider) — zwei Tabellen, Fremdschlüssel der Beschreibung auf
`WorkflowInstances` (kaskadierend), Index auf `(InstanceId, CreatedUtc)`. Die Blob-Tabelle hat bewusst
**keinen** Fremdschlüssel: die Ablage ist austauschbar, und die eingebaute soll nicht die einzig mögliche
zementieren.

---

## 37. Zentrale Abläufe, die ein Mandant für sich aktiviert — **Pflicht-Migration (1 Tabelle + Datenumzug)**

Bisher gehörte ein Auslöser dem Mandanten seiner Definition, und öffentliche Definitionen lösten
bewusst gar nicht aus. Jetzt gibt es die **Aktivierung**: ein zentral gepflegter Zeitplan (das Beispiel
ist ein Zahlungslauf zum Monatsende) wird einmal modelliert, und jeder Mandant hakt ihn für sich an.

**Der Kern ist ein Schnitt: der Lauf-Zustand gehört zur Aktivierung, nicht zum Zeitplan.** Fahren drei
Mandanten denselben zentralen Plan, hat jeder seinen eigenen letzten Lauf und seinen eigenen nächsten
Termin — eine gemeinsame Zeile kann das nicht tragen. `NextDueUtc`, `LastRunUtc`, `LastInstanceId` und
der Anspruch ziehen deshalb von `WorkflowStartTriggers` nach `WorkflowStartTriggerActivations` um.

### 37.1 Warum die Aktivierung nicht am `TriggerKey` hängt

Die Auslöser-Zeilen sind **abgeleitete** Daten: bei jedem Speichern einer Definition werden sie
weggeräumt und neu eingefügt. Der `TriggerKey` ist danach ein anderer. Eine Aktivierung, die darauf
zeigte, hinge nach der ersten Korrektur an der zentralen Definition im Leeren — und zwar **ohne
Fehlermeldung**: die Verknüpfung fände nichts, der Zeitplan liefe einfach nicht mehr. Bei einem
Monatslauf fällt das frühestens vier Wochen später auf.

Sie hängt deshalb an der fachlichen Identität: `(OwnerTenantId, DefinitionId, NodeId, Kind)` plus dem
aktivierenden `TenantId`. `OwnerTenantId` gehört zwingend dazu — eine öffentliche und eine
mandanteneigene Definition dürfen dieselbe fachliche Id tragen.

### 37.2 Was ihr am Modell einstellt

Am **Start-Knoten** (im Designer):

| Feld | Bedeutung |
|---|---|
| `AllowLocalActivation` | Ist dieser Einstieg zur Übernahme gedacht? Ohne das Kennzeichen erscheint er in keiner Auswahl. |
| `AllowReschedule` | Darf der Mandant ein eigenes Muster setzen („ich will ihn, aber am 25.")? |
| `AllowOwnVariables` | Darf er eigene Startwerte setzen? |
| `AllowTenantlessStart` | Nur Nachrichten-Start — siehe 37.4. |

An der **Definition** (nur Sysadmin):

| Feld | Bedeutung |
|---|---|
| `RequiredFeature` | Welches Feature der Mandant aktiviert haben muss, um sie zu verwenden. |
| `RequiredPermission` | Welche Berechtigung ein Benutzer zum Übernehmen und Starten von Hand braucht. |

**Die beiden werden unterschiedlich oft geprüft, und das ist der Punkt:** die Permission hängt an einem
Benutzer, und der Runner hat keinen — sie gatet also das Anhaken und den Start von Hand. Das Feature
hängt am Mandanten und wird **bei jedem Feuern** nachgeprüft. Ohne das liefe der Zahlungslauf beim
Mandanten weiter, dessen Abonnement letzten Monat ausgelaufen ist.

Fehlt das Feature beim Feuern, wird **übersprungen und protokolliert, die Fälligkeit aber trotzdem
fortgeschrieben** — die Aktivierung bleibt angehakt. Nach einem kurzen Aussetzer läuft der Plan von
selbst weiter; müsste ihn jemand von Hand wieder anhaken, merkte das niemand.

Verdrahtet wird das über `IWorkflowTenantFeatureGate` am `WorkflowEngine`-Konstruktor. **Ohne
Verdrahtung erlaubt das Gate alles** — das Verhalten ist dann exakt wie vorher.

### 37.3 Was sich am Speichern ändert

- Eine **mandanteneigene** Definition bekommt ihre eine Aktivierung automatisch. Für euch ändert sich
  dadurch nichts.
- Eine **öffentliche** Definition bekommt jetzt sehr wohl Auslöser-Zeilen (vorher gar keine). Sie feuert
  trotzdem für niemanden, solange kein Mandant angehakt hat: der Auslöser ist die Deklaration, die
  Aktivierung ist die Zusage.
- Wird ein Start-Knoten umbenannt oder entfernt, bleibt die Aktivierung als **Waise** stehen und wird nie
  aufgegriffen. Sie wird nicht gelöscht (die Zustimmung soll erhalten sein, falls der Knoten
  zurückkommt), aber das Speichern **protokolliert mit Warnung**, welche Übernahmen ab jetzt ins Leere
  laufen.
- Ändert sich das **zentrale Muster**, wird der Lauf-Zustand der betroffenen Aktivierungen zurückgesetzt
  (ein umgeschriebener Zeitplan ist ein anderer Plan, und sein erster Lauf gehört ihm). Wer ein eigenes
  Muster setzen darf und gesetzt hat, bleibt unberührt.

### 37.4 Nachrichten tragen jetzt einen Ursprungs-Mandanten — **Verhaltensänderung**

Das ist der Teil, der bestehende Installationen betreffen kann. **Bisher kannte eine Nachricht überhaupt
keinen Mandanten:** `DeliverSignal` nahm Name, Korrelationsschlüssel und Nutzdaten, und der Start lief
anschliessend im Mandanten der jeweiligen Definition. Haben hundert Mandanten je ihre eigene Definition
auf `OrderReceived`, eröffnete **eine** mandantenlose Nachricht hundert Vorgänge. Diese Flanke gab es
schon vorher — sie wird jetzt geschlossen.

Neue Regel:

- **Ursprungs-Mandant gesetzt** → es feuern nur die Auslöser bzw. Aktivierungen *dieses* Mandanten.
- **Kein Ursprungs-Mandant** → es feuert nur, was `AllowTenantlessStart` am Start-Knoten ausdrücklich
  erlaubt.

Der Ursprung wird aufgelöst: ausdrücklich übergebener Wert → Mandant der sendenden Instanz (die Outbox
führt ihn bereits) → `WorkflowExecutionScope`. **Aus dem Web setzt niemand den Ausführungs-Kontext** —
dort füllt ihn die Fassade aus dem Mandanten der Anfrage.

`AllowTenantlessStart` hat **nichts** mit `BroadcastSignal` zu tun: ein Rundruf aus der Instanz eines
Mandanten trägt sehr wohl einen Ursprung und löst nur dort aus. Es geht ausschliesslich um das *Fehlen*
des Ursprungs.

**Prüft euren Host-Code:** wer heute `DeliverSignal`/`BroadcastSignal` ohne Mandanten-Kontext aufruft und
sich darauf verlässt, dass etwas anläuft, startet danach nichts mehr. Der Fall wird ausdrücklich
protokolliert (mit den Namen der Auslöser, die nur an dieser Regel gescheitert sind) — er geht also nicht
als „auf den Namen hört halt niemand" durch. Abhilfe: den Ursprung mitgeben (`originTenantId`, neuer
optionaler Parameter) oder das Kennzeichen setzen.

Die Zustellung an **wartende** Instanzen ist unverändert. Es geht ausschliesslich um das Entstehen neuer
Vorgänge.

**Im Ein-Mandanten-Betrieb ändert sich nichts:** dort trägt alles `NULL`, Ursprung und Aktivierung fallen
zusammen, und die Regel greift von selbst nicht.

### 37.5 Migration

**Der Regelweg ist die mitgelieferte Migration** — wie in §29 und aus demselben Grund: der
`WorkflowContext` hat gepflegte Migrationsprojekte, und der Snapshot ist geprüft (die erzeugte Migration
berührt ausschliesslich `WorkflowStartTriggers` und die neue Tabelle, keine Fremd-Änderungen).

```
dotnet ef database update -p ITVComponents.Workflow.EntityFramework.SqlServer   -s <euer Host>
dotnet ef database update -p ITVComponents.Workflow.EntityFramework.PostgreSql  -s <euer Host>
```

Migration: `20260819131232_TriggerActivations` (SQL Server) bzw. `20260819131244_TriggerActivations`
(PostgreSQL).

**Diese Migration bewegt Daten, nicht nur Schema.** Sie legt die Tabelle an, zieht für jeden bestehenden
Auslöser seinen Lauf-Zustand in genau eine Aktivierung um und lässt **erst danach** die alten Spalten
fallen. Das ist keine Kosmetik: `LastRunUtc` muss mitkommen, sonst gilt hinterher jeder Plan als „noch
nie gelaufen" und jedes Muster mit `t`-Kennzeichen läuft beim ersten Poll los. Auch das `Down` holt den
Stand zurück, bevor die Tabelle fällt.

> **Nicht neu scaffolden.** Beide Migrationen sind von Hand nachbearbeitet. Das Gerüst hatte zwei Dinge
> falsch: es deutete `LeaseOwner` → `RequiredPermission` und `LastInstanceId` → `RequiredFeature` als
> **Umbenennung** (gleicher Typ, gleiche Tabelle — für den Vergleich zweier Schemata nicht
> unterscheidbar), womit Runner-Kennungen als Berechtigungsnamen und alte Instanz-Ids als Feature-Namen
> in der Tabelle stünden; und es liess den Datenumzug ganz weg, weil es die Absicht dahinter nicht kennt.
> Ein `migrations add` auf denselben Stand erzeugt wieder diese Fassung.

Auf PostgreSQL wird der eindeutige Index bewusst als rohes SQL angelegt: er braucht
`NULLS NOT DISTINCT`. PostgreSQL behandelt `NULL`s im eindeutigen Index standardmässig als
**verschieden** — ohne den Zusatz wäre der Schutz für öffentliche Auslöser (Besitzer `NULL`) und für den
mandantenfreien Betrieb stillschweigend wirkungslos. Auf SQL Server zählen `NULL`s dort als gleich, dafür
darf der Index **keinen Filter** bekommen (sonst hängt der Server von selbst ein
`WHERE ... IS NOT NULL` an und nimmt ausgerechnet die öffentlichen Zeilen von der Prüfung aus).

#### Gleichwertige Handarbeit

Für Installationen, die kein `database update` fahren. Reihenfolge einhalten — **erst umziehen, dann die
alten Spalten fallen lassen**, sonst ist der Lauf-Zustand weg.

```sql
-- 1) Neue Spalten am Auslöser (denormalisiert aus der Definition bzw. dem Start-Knoten)
ALTER TABLE WorkflowStartTriggers ADD IsPublic bit NOT NULL DEFAULT 0;
ALTER TABLE WorkflowStartTriggers ADD RequiredFeature nvarchar(max) NULL;
ALTER TABLE WorkflowStartTriggers ADD RequiredPermission nvarchar(max) NULL;
ALTER TABLE WorkflowStartTriggers ADD AllowLocalActivation bit NOT NULL DEFAULT 0;
ALTER TABLE WorkflowStartTriggers ADD AllowTenantlessStart bit NOT NULL DEFAULT 0;
ALTER TABLE WorkflowStartTriggers ADD AllowReschedule bit NOT NULL DEFAULT 0;
ALTER TABLE WorkflowStartTriggers ADD AllowOwnVariables bit NOT NULL DEFAULT 0;
GO

-- 2) Die Aktivierungstabelle
CREATE TABLE WorkflowStartTriggerActivations (
    ActivationKey         int IDENTITY(1,1) NOT NULL,
    OwnerTenantId         nvarchar(450) NULL,
    DefinitionId          nvarchar(450) NULL,
    NodeId                nvarchar(450) NULL,
    Kind                  int NOT NULL,
    TenantId              nvarchar(450) NULL,
    Enabled               bit NOT NULL,
    PatternOverride       nvarchar(max) NULL,
    VariablesJsonOverride nvarchar(max) NULL,
    NextDueUtc            datetime2 NULL,
    LastRunUtc            datetime2 NULL,
    LastInstanceId        nvarchar(max) NULL,
    LeaseOwner            nvarchar(max) NULL,
    LeaseUntilUtc         datetime2 NULL,
    ActivatedBy           nvarchar(max) NULL,
    ActivatedUtc          datetime2 NOT NULL,
    CONSTRAINT PK_WorkflowStartTriggerActivations PRIMARY KEY (ActivationKey)
);
GO

-- Der eindeutige Index über die fachliche Identität.
-- OHNE gefilterten Index: SQL Server hängt sonst von selbst ein "WHERE ... IS NOT NULL" an und nähme
-- ausgerechnet die öffentlichen Auslöser von der Prüfung aus - also genau den Fall, um den es geht.
-- Die Index-NAMEN sind exakt die der Migration. Wer sie anders wählt, bekommt eine Datenbank, die zwar
-- funktioniert, in der eine spätere Migration ihren Index aber nicht wiederfindet.
CREATE UNIQUE INDEX IX_WorkflowStartTriggerActivations_OwnerTenantId_DefinitionId_NodeId_Kind_TenantId
    ON WorkflowStartTriggerActivations (OwnerTenantId, DefinitionId, NodeId, Kind, TenantId);
GO

-- Der Aufgriff des Runners.
CREATE INDEX IX_WorkflowStartTriggerActivations_Enabled_NextDueUtc
    ON WorkflowStartTriggerActivations (Enabled, NextDueUtc);
GO

-- 3) Datenumzug: für JEDEN bestehenden Auslöser genau EINE Aktivierung, Lauf-Zustand 1:1.
--    LastRunUtc MUSS mit - sonst greift bei jedem laufenden Zeitplan mit "sofort"-Kennzeichen
--    das "noch nie gelaufen" ein zweites Mal, und alles läuft beim ersten Poll sofort los.
INSERT INTO WorkflowStartTriggerActivations
    (OwnerTenantId, DefinitionId, NodeId, Kind, TenantId, Enabled,
     NextDueUtc, LastRunUtc, LastInstanceId, LeaseOwner, LeaseUntilUtc, ActivatedBy, ActivatedUtc)
SELECT t.TenantId, t.DefinitionId, t.NodeId, t.Kind, t.TenantId, 1,
       t.NextDueUtc, t.LastRunUtc, t.LastInstanceId, t.LeaseOwner, t.LeaseUntilUtc,
       '(migriert)', SYSUTCDATETIME()
FROM WorkflowStartTriggers t;
GO

-- 4) ERST JETZT die alten Spalten und den alten Index fallen lassen.
DROP INDEX IX_WorkflowStartTriggers_Kind_NextDueUtc ON WorkflowStartTriggers;
ALTER TABLE WorkflowStartTriggers DROP COLUMN NextDueUtc, LastRunUtc, LastInstanceId,
                                              LeaseOwner, LeaseUntilUtc;
GO

-- 5) IsPublic für bestehende Zeilen aus der Definition nachziehen.
--    Bis PRE185 bekamen öffentliche Definitionen gar keine Auslöser - es sollte also nichts zu tun
--    geben. Der Abgleich steht hier, damit man sich darauf nicht verlassen muss.
UPDATE t SET t.IsPublic = 1
FROM WorkflowStartTriggers t
JOIN WorkflowDefinitions d ON d.DefinitionKey = t.DefinitionKey
WHERE d.TenantId IS NULL;
GO
```

**PostgreSQL:** identisch, aber der eindeutige Index braucht `NULLS NOT DISTINCT` — sonst gelten die
`NULL`-Werte als verschieden und der Schutz ist stillschweigend wirkungslos (dieselbe Falle wie beim
eindeutigen Index der Definitionen):

```sql
-- Der Name ist auf 63 Zeichen gekürzt, samt Tilde - genau so vergibt ihn das Migrations-Gerüst, und
-- genau so muss er bleiben, damit beide Wege dieselbe Datenbank ergeben.
CREATE UNIQUE INDEX "IX_WorkflowStartTriggerActivations_OwnerTenantId_DefinitionId_~"
    ON "WorkflowStartTriggerActivations"
    ("OwnerTenantId", "DefinitionId", "NodeId", "Kind", "TenantId") NULLS NOT DISTINCT;
```

Der Name des in Schritt 4 fallengelassenen Index kann bei euch abweichen — vorher nachsehen
(`sp_helpindex 'WorkflowStartTriggers'`).

### 37.6 Prüfen nach dem Deployment

1. `SELECT COUNT(*) FROM WorkflowStartTriggerActivations` == `SELECT COUNT(*) FROM WorkflowStartTriggers`
   (vor dem Umbau gemessen).
2. Ein bestehender Zeitplan läuft zu seinem gewohnten Termin — **nicht** sofort beim ersten Poll. Läuft
   er sofort, ist `LastRunUtc` beim Umzug nicht mitgekommen.
3. Im Log nach `laesst … Einstieg(e) NICHT anlaufen` suchen: das sind die Host-Aufrufe aus 37.4, die
   ihren Ursprungs-Mandanten noch nicht mitgeben.

---

## 38. Passkeys sind ausdrücklich einzuschalten — **Pflicht, wenn ihr Passkeys benutzt**

Betrifft euch direkt: **ohne Handeln verschwindet der Passkey-Abschnitt aus der Kontoverwaltung.**

### 38.1 Was passiert ist

.NET 10 hat `IdentityUserContext` ein `UserPasskeys`-DbSet hinzugefügt, den Entitätstyp dazu aber
**ausdrücklich ausgeschlossen** — Passkeys sind seither opt-in. Die Konvention
`TableNamesFromProperties` lief mit `FlattenHierarchy` über alle DbSet-Eigenschaften, also auch die
geerbten, und hat den Ausschluss überstimmt. Der Typ landete unkonfiguriert im Modell, mit ihm seine
`Data`-Eigenschaft als schlüssellose Entität:

```
The entity type 'IdentityPasskeyData' requires a primary key to be defined.
  at ModelValidator.ValidateNonNullPrimaryKeys
```

Das ist der **Kern**-Validator — jeder Identity-Kontext der Bibliothek scheiterte daran, auf SQL Server
genauso wie auf PostgreSQL, und zwar beim Bauen des Modells: also `dotnet ef` **und** die Laufzeit.
Dass es bei euch lief, lag allein an eurem eigenen Passkey-Block im `ApplicationDbContext`, der die
Entität nachträglich vollständig konfiguriert hat.

Die Konvention respektiert jetzt ausdrückliche Ausschlüsse. Damit ist der Fehler weg — und der
Passkey-Typ standardmässig nicht mehr im Modell, so wie .NET 10 es vorsieht.

> Nur *absichtliche* Ausschlüsse (`Explicit`, `DataAnnotation`) werden übersprungen. Was blosse
> Konvention ausgeschlossen hat, wird weiter benannt wie bisher — die Tabellennamen `Users`, `Roles`,
> `UserClaims` kommen genau aus dieser Konvention und bleiben unverändert.

### 38.2 Was ihr tun müsst

**Erstens: den Passkey-Block behalten und prüfen, dass er vollständig ist.** Er ist ab jetzt die
einzige Stelle, die den Typ ins Modell bringt. Er muss `IdentityUserPasskey<string>` registrieren,
einen Schlüssel setzen — und **etwas über `Data` sagen**. Das ist die wahrscheinlichste
Stolperstelle: ohne Aussage dazu ist `IdentityPasskeyData` wieder eine schlüssellose Entität, und die
Validierung bricht mit derselben Meldung ab. Sinnvoll ist ein komplexer Typ bzw. eine JSON-Spalte,
keine eigene Tabelle.

Prüfbar **ohne Datenbank**:

```
dotnet ef dbcontext info --context ApplicationDbContext
```

Kommt eine Ausgabe mit `Provider name`, stimmt das Modell. Kommt
`IdentityPasskeyData requires a primary key`, fehlt die Aussage über `Data`.

**Zweitens: den Schalter setzen.** Neu in `IdentityUiOptions`, **Standard `false`**:

```json
{ "UsePasskeys": true }
```

Ohne ihn registriert das WebPart den Passkey-Handler nicht, und dann gilt: kein „Mit Passkey
anmelden" auf der Anmeldeseite, kein Eintrag *Passkeys* in der Konto-Navigation, die Seite
`Account/Manage/Passkeys` antwortet „nicht verfügbar", und die zugehörigen Endpunkte lehnen ab.

### 38.3 Warum ein Schalter und keine Erkennung

Naheliegend wäre `UserManager.SupportsUserPasskey` gewesen. Der Wert trägt hier aber nicht: der
EF-Benutzer-Speicher setzt die Passkey-Methoden **unbedingt** um und meldet deshalb auch dann `true`,
wenn der DbContext die Entität gar nicht abbildet. Er beantwortet „kann der Speicher das
grundsätzlich", nicht „ist es hier eingerichtet" — und nur Letzteres entscheidet, ob das Speichern
gelingt. Verlässlich wäre allein ein Blick ins Modell des konkreten DbContext, und den kennen die
Identity-Seiten nicht; sie sehen `SignInManager` und `UserManager`. Also sagt es der Host.

Ausführlich in `Migration-Future_10-MLM-Passkeys.md`.

---

## 39. PostgreSQL für die hierarchische Mandanten-Sicherheit (optional, kein Zwang)

Bis jetzt gab es die Baum-Ausprägung (`CoreIdentityTree`) nur für SQL Server — 17 Datenbank-Objekte in
reinem T-SQL. Ein Umzug auf PostgreSQL war damit blockiert, nicht bloss aufwendig. Das ist erledigt:
die Ausprägung existiert jetzt auch für PostgreSQL, mit eigener Initialmigration.

**Wer bei SQL Server bleibt, muss nichts tun.** Dieser Abschnitt ist für den Fall, dass ihr wechselt.

### 39.1 Was zu konfigurieren ist

Im WebPart `…TenantSecurity.PostgreSql` dieselben Angaben wie bisher auf SQL Server —
`Identity = CoreIdentity`, `Strategy = Tree` —, dazu die Verbindungszeichenfolge. Der Fall
`(CoreIdentity, Tree)` ist dort neu; vorher kannte die PostgreSQL-Fassung nur die flache Variante.

### 39.2 Was beim Einspielen zu beachten ist

1. Die Initialmigration legt das Schema an (75 Tabellen).
2. Danach braucht es — wie auf SQL Server auch — eine Migration, die
   `PostgreSqlColumnsSyntaxHelper.ConfigureViews(migrationBuilder)` aufruft. Sie legt 5 Views, 10
   Funktionen und den Rekursions-Wächter an und ist **wiederholbar**: alle Objekte werden vorher
   weggeräumt.
3. **Euer eigener `ApplicationDbContext` ist damit nicht erledigt.** Die Bibliothek bringt nur ihre
   eigenen Migrationen mit; für euren Kontext braucht es einen PostgreSQL-Migrationsstand aus eurem
   Repo.

### 39.3 Zwei Verhaltensunterschiede, die ihr kennen solltet

**Der Zyklen-Abbruch meldet sich anders.** SQL Server bricht eine Rekursion nach 100 Ebenen mit
Fehler 530 ab. PostgreSQL kennt keine solche Grenze — dieselbe Abfrage liefe endlos. Nachgebaut wurde
das mit einer Wächter-Funktion, bewusst **nicht** mit der `CYCLE`-Klausel: die bricht *still* ab und
liefert ein Teilergebnis, und bei einer Rechte-Abfrage ist das die schlechtere Sorte Fehler — jemand
arbeitet mit zu wenig Rechten weiter, und niemand merkt es. Die Meldung nennt zusätzlich das Objekt:

```
ERROR: Mehr als 100 Rekursionsschritte in UpwardsTenantTree - die Hierarchie ist tiefer als
       zulaessig oder enthaelt einen Zyklus. (Ebene 102, der Anker zaehlt nicht als Schritt.)
                                                                                [SQLSTATE 54001]
```

Wer heute auf Fehler 530 prüft, muss das anpassen. Die Grenze liegt auf beiden Datenbanken an
derselben Stelle (nachgemessen: eine Kette von 101 Mandanten trägt, 102 bricht ab) — gezählt werden
Rekursions**schritte**, und der Anker liefert Ebene 1, ohne einen gebraucht zu haben.

**`GetChildTenantsWithPermsProc` ist dort eine Funktion, keine Prozedur** — PostgreSQL kennt keine
Prozedur, die eine Ergebnismenge liefert. Für den Toolkit-Code ist das unsichtbar (der Zugriff läuft
über das Methoden-Verzeichnis). Wer die Prozedur aus eigenem SQL aufruft, schreibt dort
`select * from "GetChildTenantsWithPermsProc"(…)` statt `exec`. Nebenbei kann die PostgreSQL-Fassung
dadurch mehr: sie lässt sich in eine Abfrage einbetten, was auf SQL Server am Verbot geschachtelter
`INSERT … EXEC` scheitert.

### 39.4 Was geprüft ist — und was nicht

Geprüft: dieselbe Hierarchie auf beiden Providern, dieselben Abfragen, Ergebnisse **Zeile für Zeile**
verglichen — 95 Zeilen, kein Unterschied. Darin enthalten sind die heiklen Fälle: mandanteninterne
Weitergabe von Rollen, eine Berechtigung, die nur über diese Weitergabe erreichbar ist, Gleichstände
auf derselben Ebene, ein Benutzer in der Mitte des Baums, Verzweigungen. Einzelheiten in
`Audit-PostgreSQL-Luecken.md`.

**Inzwischen auch geprüft: das Laufzeitverhalten unter Last.** 10 000 Mandanten mit einer Kette bis
Tiefe 100, beide Provider, zehn Messpunkte. Der Verdacht hat sich bestätigt — und hat einen konkreten
Grund: PostgreSQL schiebt einen Filter nicht in eine rekursive Sicht. `UpwardsTenantTree` wurde bei
jedem Zugriff komplett gebaut (509 950 Zeilen) und erst danach gefiltert; der Weg, den *jede* Anfrage
in einem Kind-Mandanten geht, kostete dadurch 270 ms statt unter 1 ms.

**Behoben.** Die Rekursion sitzt jetzt in einer Funktion mit dem Blatt als Parameter, die Sicht ist ein
flacher `LATERAL`-Aufruf darauf — damit greift der Filter im Anker. Gemessen: 270 ms → 1 ms;
Plugin-Auflösung 786 ms → 1,4 ms; der Rollen-Baum nach unten und die Kind-Mandanten-Abfrage nebenbei
um Faktor 3 schneller und damit **vor** SQL Server. Der Preis ist ein Durchlauf ohne Filter (739 statt
286 ms), den es im Toolkit-Code nicht gibt.

**Für euch heisst das:** wenn ihr eine PostgreSQL-Datenbank schon aufgebaut habt, braucht es eine
Migration, die `PostgreSqlColumnsSyntaxHelper.ConfigureViews(migrationBuilder)` erneut ausführt —
sonst bleibt die alte, langsame Sicht stehen. Schema unverändert, Ergebnisse unverändert (der
Gleichheitstest gegen SQL Server läuft nach dem Umbau unverändert durch).

---

## 40. Der Mandanten-Baum wird nicht mehr ganz gebaut — **Pflicht-Migration (`ConfigureViews`), beide Provider**

Kein Schema-Change, keine Vertragsänderung, keine neue Einstellung. Was sich ändert, sind die
Datenbank-Objekte des Mandanten-Baums — und zwar deutlich: bei 10 000 Mandanten fallen die beiden
teuersten Wege von rund **5 Sekunden auf unter 200 ms**.

### 40.1 Was zu tun ist

Eine Migration, die `ConfigureViews(migrationBuilder)` des jeweiligen Providers erneut ausführt
(`SqlColumnsSyntaxHelper` bzw. `PostgreSqlColumnsSyntaxHelper`). Sie ist wiederholbar — jedes Objekt
wird vorher weggeräumt. Ohne sie bleiben die alten Objekte stehen und es ändert sich nichts.

**Das gilt auch für SQL Server**, nicht nur für den PostgreSQL-Weg aus §39.

### 40.2 Woran es lag

Ein Filter auf ein Blatt des Baums kam bisher **nach** der Rekursion zum Zug. Bei 10 000 Mandanten
heisst das: erst 509 950 Zeilen bauen, dann 100 davon behalten.

Wo der Filter als **Konstante** dasteht, zieht SQL Server ihn von sich aus in den Anker der
Rekursion; PostgreSQL nicht — für dessen Planer ist eine rekursive CTE eine Optimierungsgrenze.
Wo er aus einem **Join** kommt — so in den beiden Rollenbaum-Prozeduren, wo der Blickpunkt aus einer
Tabellenvariablen stammt —, gelingt es **keiner** der beiden Datenbanken.

Drei Änderungen, auf beiden Providern dieselben:

1. Der Aufwärtsbaum bekommt das Blatt als Parameter (auf PostgreSQL: Rekursion in eine Funktion, Sicht
   als flacher `LATERAL`-Aufruf; auf SQL Server unnötig, der Planer kann es dort selbst).
2. Der Abwärtsbaum ebenso, und die beiden Rollenbaum-Prozeduren rufen ihn mit dem Blickpunkt als
   Parameter (`CROSS APPLY` bzw. `LATERAL`) statt die Sicht anzujoinen.
3. Die Schlussabfrage der Rollenbaum-Prozeduren **liest den Aufwärtsbaum gar nicht mehr**: die
   Rollen-Rekursion geht je Schritt genau eine Mandanten-Ebene hoch, ihr `level` *ist* der
   `ParentLevel` des Paares — der Join holte nur diesen Wert und zwei Mandantennamen und kostete dafür
   den ganzen Baum.

### 40.3 Was ihr davon merkt

| Weg | SQL Server vorher | nachher | PostgreSQL vorher | nachher |
|---|---|---|---|---|
| Mandantenfilter (läuft bei **jeder** Anfrage in einem Kind-Mandanten) | < 1 ms | < 1 ms | 280 ms | **1 ms** |
| Plugin-Auflösung | 2 ms | 1 ms | 786 ms | **1,9 ms** |
| Rollen-Baum nach unten, von der Wurzel | 5 007 ms | **166 ms** | 4 703 ms | **68 ms** |
| Kind-Mandanten mit Berechtigung | 4 839 ms | **151 ms** | 5 087 ms | **71 ms** |
| Baum vollständig durchlaufen (kommt im Code nicht vor) | 1 950 ms | 1 839 ms | 286 ms | 800 ms |

Die letzte Zeile ist der Preis auf der PostgreSQL-Seite: ein Durchlauf **ohne** Filter ruft die
Funktion je Mandant einmal. Im Toolkit-Code gibt es diesen Fall nicht — alle Lesestellen filtern auf
das Blatt.

### 40.4 Woraufhin das geprüft ist

- Gleichheitstest beider Provider unverändert grün (87 Zeilen Zeichen für Zeichen, dazu die fünf
  Kind-Mandanten-Abfragen).
- **Alt gegen neu**, weil der Vergleich der Provider hier nichts beweist (beide Seiten sind gleich
  geändert): die alte Fassung der Schlussabfrage von Hand nachgebaut und gegen die neue gestellt —
  auf beiden Datenbanken, auf der kleinen Hierarchie **und** auf 10 000 Mandanten, jeweils **null
  Unterschiede**.
- Der Zyklen-Abbruch meldet sich unverändert, auch in der neuen Abwärts-Funktion: Kette 101 trägt,
  102 bricht ab — auf beiden Datenbanken, in beiden Richtungen.

---

## 41. Token-Zeilen sind jetzt mandantengefiltert — **Pflicht-Migration (`TokenTenantBackfill`)**

Bisher hatten nur Definitionen und Instanzen einen Query-Filter. Die **Token-Zeilen** hatten keinen: wer
über `db.Tokens` einstieg — eine Aufgaben-Ansicht, eine Diagnose-Abfrage, eigener Code — sah die Zeilen
**aller** Mandanten. Geschützt war nur der Weg über den Store, weil dessen Suchläufe die gefundenen
Instanzen am Ende durch den (gefilterten) Instanz-Zugriff laden.

Das ist geschlossen: `TokenRow` hat denselben strikten Filter wie die Instanz.

### 41.1 Was zu tun ist — **vor** dem ersten Start mit der neuen Fassung

`dotnet ef database update` für den `WorkflowContext`; die Migration `TokenTenantBackfill` liegt für
**beide Provider** bei. Sie ändert **kein Schema**, sondern trägt nach:

```sql
-- SQL Server
UPDATE t SET t.TenantId = i.TenantId
FROM Tokens t INNER JOIN WorkflowInstances i ON i.Id = t.InstanceId
WHERE t.TenantId IS NULL AND i.TenantId IS NOT NULL;
```

**Warum das nicht optional ist.** Die Spalte `Tokens.TenantId` gibt es seit `UserTasks`; sie kam als
`nullable` ohne Nachtrag, weil sie damals nur die Arbeitsliste bediente — dort fällt eine leere Zelle
nicht auf. Geschrieben wird sie seither bei jedem Speichern einer Instanz, alle laufenden Vorgänge
haben sie also längst. **Ausser den parkenden:** ein Vorgang, der seit damals auf eine Aufgabe, eine
Nachricht oder eine Frist wartet, wurde in der Zwischenzeit nie gespeichert.

Und hier kippt die Fehlerart. Bei Definitionen und Instanzen heisst ein Filterfehler „sieht zu viel".
Bei Tokens heisst er **„sieht nichts"** — und ein Token, das niemand sieht, ist ein Vorgang, der stehen
bleibt, ohne dass es jemandem auffällt.

### 41.2 Was ihr sonst merkt

Nichts, wenn ihr über den Store und die mitgelieferten Ansichten arbeitet. Zwei Punkte für eigenen Code:

- **Eigene Diagnose-Abfragen oder Auswertungen über `db.Tokens`** liefern ab jetzt nur noch die Zeilen
  des aktiven Mandanten. Das ist die Absicht — wer bewusst darüber hinaus lesen will (Betriebs-Sicht,
  Support), setzt `IgnoreQueryFilters()` und trifft die Entscheidung damit sichtbar.
- **Der Runner bleibt filterfrei.** Das war schon Bedingung und ist es jetzt umso mehr: sein Suchlauf
  geht jedem `WorkflowExecutionScope` voraus. Wer einen eigenen Worker betreibt, prüft, dass dessen
  Kontext über den options-only-Weg gebaut wird.

Im Toolkit sind sechs Lesewege ausdrücklich vom Filter ausgenommen, jeder mit Begründung im Code:
das Laden einer Instanz und ihrer Tokens (die Mandanten-Grenze zieht die Instanz — was an ihr hängt,
gehört dazu), das Speichern (was es nicht findet, legt es neu an — und läuft in eine
Schlüsselverletzung), sowie die Runner-Wege `PeekNextTimerDueUtc`, das Stempeln in `ClaimDueTimers`
und `ReleaseLocksOfOwner` (ein Anspruch gehört einem Runner, nicht einem Mandanten).

---

## 42. Konsolidierungs-Runde: was davon euch betrifft

Diese Runde war überwiegend Aufräumen im Toolkit (Doppelspurigkeit zusammengeführt, Dialoge und
Raster-Werkzeugleisten auf gemeinsame Bausteine). **Für euch bleiben davon drei Punkte übrig** — der
Rest ist innen und ändert für euch nichts.

### 42.1 `HasPermission` verliert den Principal — **Pflicht, aber winzig**

```csharp
// vorher
bool HasPermission(ClaimsPrincipal user, params string[] permissions);
Handler.HasPermission(currentUser, "Tenants.Write")

// jetzt
bool HasPermission(params string[] permissions);
Handler.HasPermission("Tenants.Write")
```

Der Parameter wurde in **keiner** der 31 Implementierungen gelesen — die Prüfung lief immer gegen den
aktuellen Berechtigungs-Bereich. Er war damit kein Fehler, aber eine geladene Waffe für den ersten
Aufrufer, der bei Impersonation einen fremden Principal einsetzt und annimmt, er werde beachtet.

**Was ihr tun müsst:** die Aufrufstellen kürzen, und falls ihr einen der `I…AdminHandler`-Verträge
selbst implementiert, dort die Signatur nachziehen. Der Compiler zeigt euch jede Stelle.

```powershell
$src = Get-ChildItem -Recurse -File |
       Where-Object { $_.Extension -in '.cs','.razor' -and $_.FullName -notmatch '\\(obj|bin)\\' }
$src | Select-String -Pattern 'HasPermission\(' | Select-Object Path, LineNumber, Line
$src | Select-String -Pattern ':\s*I[A-Za-z]*AdminHandler\b' | Select-Object Path, LineNumber, Line
```

**Zwei Fallen aus unserem eigenen Durchlauf**, damit sie euch nicht auch erwischen:

- Sucht nach den **Formen**, nicht nach Bezeichnern. Wir hatten nach `user`/`currentUser` gesucht und
  `auth.User` — einen Member-Zugriff — übersehen. Drei Stellen fielen durch; gefunden hat sie der
  Compiler, nicht die Suche.
- **Baut die ganze Solution**, nicht das Projekt, an dem ihr gerade seid. Scheitert ein Projekt, wird
  alles Abhängige gar nicht erst gebaut — die erste Fehlerliste ist nie die vollständige.

### 42.2 Ein gescheiterter Vorgang läuft nicht mehr von selbst weiter — **Verhaltensänderung**

Bisher konnte ein **gefaulteter** Vorgang still weiterlaufen: `Fault()` lässt die übrigen Tokens
absichtlich stehen (der Wiederaufsatz braucht sie), ein Fristen-Timer an einem anderen Zweig blieb
scharf, wurde später fällig — und die Reaktivierung setzte den Vorgang wieder auf `Running`, **ohne den
Fault anzusehen**. Die Fehlermeldung war weg, und entschieden hatte das niemand. Dasselbe galt für ein
eintreffendes Signal, die Ziel-Übernahme und die Rückkehr eines Subworkflows.

**Ab jetzt gilt: einen gescheiterten Vorgang nimmt ausschliesslich ein ausdrücklicher Retry wieder auf**
(`RetryFaulted` / `RetryFaultedBranches`). Jede Abweisung steht im Log.

**Was ihr davon merkt:** Vorgänge, die sich bisher scheinbar „von selbst erholt" haben, bleiben jetzt
gefaultet stehen und warten auf eine Entscheidung. Das ist die Absicht — vorher habt ihr nicht gesehen,
dass überhaupt etwas gescheitert war. **Schaut nach dem Deployment einmal ins Monitoring**, ob dort
Vorgänge stehen, die vorher unbemerkt weitergelaufen sind.

Kein Schema-Change, keine Migration.

### 42.3 Ein neuer Ausgang beim Abschliessen einer Aufgabe

`UserTaskCompletionStatus` hat einen Wert dazubekommen: **`InstanceNotResumable`** — der Vorgang steht
still (gescheitert, abgebrochen oder schon beendet), die Aufgabe wurde deshalb *nicht* abgeschlossen und
die Eingaben nicht gespeichert. Der Wert hängt **hinten** an, die bestehenden behalten ihre Zahl.

Zu unterscheiden von `Faulted`: **dort** ist die Aufgabe erledigt und der Prozess erst danach
gescheitert, **hier** war er es schon vorher.

**Was ihr tun müsst:** nichts, wenn ihr den Ausgang nur über `Success` auswertet. Wertet ihr ihn in
einer **eigenen Aufgaben-Maske** aus (`IUserTaskView`), behandelt den neuen Wert — sonst landet er in
eurem `default`-Zweig und der Benutzer bekommt „gibt es nicht mehr" zu lesen, obwohl es die Aufgabe sehr
wohl noch gibt und sie nach einem Retry wieder funktioniert. Die Meldung dazu liegt als
`InstanceNotResumable` in `WorkflowTaskMessages` (en/de/fr/it).

### 42.4 Zwei Pakete gehören ab jetzt zusammen

Die neuen gemeinsamen Bausteine `EditDialogShell` und `CrudGridToolbar` liegen in
**`ITVComponents.WebCoreToolkit.Blazor.MudBlazor`**, ihre Nutzer in
**`…Blazor.MudBlazor.AdminViews`**. Zieht beide Pakete **zusammen** hoch — sonst findet AdminViews die
Komponenten nicht.

### 42.5 Sichtbar, aber ohne Aufwand

- **Der Speichern-Knopf ist jetzt überall gefüllt** (`Variant.Filled`). Vorher war es die Hälfte der
  Dialoge, die andere Hälfte nicht. 19 Masken sehen dadurch anders aus — das ist gewollt und kein
  Fehler.
- Bei ungültiger Eingabe bleibt es je Maske dabei, ob ein Hinweis erscheint.

### 42.6 Eine Warnung für eure eigenen Registrierungs-Methoden

Falls ihr dem Muster der `Add…`-Erweiterungsmethoden folgt (generische Registrierung, aufgelöst über den
DbContext): **die Bindung der Typparameter läuft über ihren NAMEN, nicht über ihre Position.** Die Namen
sind damit faktisch Vertrag. Wer einen umbenennt, bricht die Verdrahtung — und kein Compiler sagt etwas
dazu, es fällt erst beim Start auf, und zwar daran, dass eine Ansicht fehlt.

Das war bisher zusätzlich **stumm**: der Auflösungsweg lieferte kommentarlos `null`, und der Aufrufer
lief in eine `NullReferenceException`, deren Meldung nichts über die Ursache sagte. Das ist behoben —
ein Fehlschlag nennt jetzt Klasse, Methode, Kontext-Typ und Grund. Wenn bei euch nach dem Update eine
Ansicht fehlt: **ins Log schauen, dort steht es jetzt.**

---

## 43. `PagedResult` und `ListQuery` liegen jetzt an EINER Stelle — **Pflicht, wenn ihr Handler selbst implementiert**

`PagedResult<T>` lag **dreimal** buchstäblich identisch im Repo, `ListQuery` **zweimal** unter zwei Namen.
Beide sind zusammengeführt:

| vorher | jetzt |
|---|---|
| `…AdminViews.TenantSecurityViews.ViewModels.ListQuery` | `ITVComponents.WebCoreToolkit.Blazor.Paging.ListQuery` |
| `…AdminViews.AspNetCoreTenantSecurityUserView.ViewModels.UserListQuery` | dieselbe `ListQuery` |
| `…ViewModels.PagedResult<T>` (2×) und `…WorkflowViews.Common.PagedResult<T>` | `ITVComponents.WebCoreToolkit.Blazor.Paging.PagedResult<T>` |
| `AdminContext` und `UserListContext` | `ITVComponents.WebCoreToolkit.Blazor.Paging.AdminContext` |

Die Typen liegen im Paket **`ITVComponents.WebCoreToolkit.Blazor.MudBlazor`** — dem, das ihr wegen
`EditDialogShell`/`CrudGridToolbar` ohnehin zusammen mit AdminViews hochzieht (§42.4).

**Was ihr tun müsst:** in euren eigenen Handler-Implementierungen und Masken die `using`-Zeile auf
`ITVComponents.WebCoreToolkit.Blazor.Paging` umstellen und `UserListQuery` → `ListQuery`,
`UserListContext` → `AdminContext` umbenennen. Der Compiler zeigt euch jede Stelle (`CS0246`).

```powershell
$src = Get-ChildItem -Recurse -File |
       Where-Object { $_.Extension -in '.cs','.razor' -and $_.FullName -notmatch '\\(obj|bin)\\' }
$src | Select-String -Pattern '\b(PagedResult|ListQuery|UserListQuery|AdminContext|UserListContext)\b' |
  Select-Object Path, LineNumber, Line
```

### 43.1 Achtung: die Workflow-Abfrage war NICHT dasselbe

Die dritte Fassung, `…WorkflowViews.Common.ListQuery`, ist **nicht** mitgekommen und heisst jetzt
**`WorkflowListQuery`**. Sie trägt statt `TenantId` ein **`Status`**-Feld — sie sah nur gleich aus, weil
sie gleich hiess.

Das ist der Grund für die Umbenennung und nicht ein Schönheitsentscheid: hätten wir alle drei
zusammengezogen, wäre entweder der Status-Filter der Workflow-Übersicht verschwunden oder jede
Admin-Abfrage hätte ein Feld bekommen, das dort nichts bedeutet. **Wenn ihr eigene Workflow-Handler
implementiert, benennt den Typ mit um** — der Compiler meldet es.

---

## Schnellübersicht der Breaking Changes

| # | Was | Aktion |
|---|---|---|
| 42a | **`HasPermission`** | Parameter `ClaimsPrincipal user` entfällt — Aufrufstellen kürzen; eigene `I…AdminHandler`-Implementierungen nachziehen (§42.1) |
| 42b | **Workflow: Fault** | ein gescheiterter Vorgang läuft **nicht mehr** von selbst weiter — nur noch per `RetryFaulted`. Kein Schema-Change, aber Monitoring prüfen (§42.2) |
| 42c | `UserTaskCompletionStatus` | neuer Wert `InstanceNotResumable` (hinten angehängt) — nur relevant für eigene Aufgaben-Masken (§42.3) |
| 42d | **Pakete** | `…Blazor.MudBlazor` und `…Blazor.MudBlazor.AdminViews` zusammen hochziehen (§42.4) |
| 43a | **`PagedResult<T>` / `ListQuery`** | zusammengelegt nach `ITVComponents.WebCoreToolkit.Blazor.Paging` (Paket `…Blazor.MudBlazor`); `UserListQuery`→`ListQuery`, `UserListContext`→`AdminContext` (§43) |
| 43b | **Workflow-Abfrage** | `…WorkflowViews.Common.ListQuery` heisst jetzt `WorkflowListQuery` — sie trägt `Status` statt `TenantId` und war nie derselbe Typ (§43.1) |
| 1 | `IFileHandler.AddFile` | `ModelStateDictionary` raus, `FileOperationResult` zurück; Namespace → `ServiceShared.FileHandling` |
| 1b | `IFileHandler.ReadFile` (sync) | `ref`/`out byte[]` → `FileReadResult ReadFile(id, identity)` (Stream-basiert, wie async) |
| 2 | `IRespondingFileHandler.GetUploadResult` | `IResult` → `FileReadResult`; Namespace → `ServiceShared.FileHandling` |
| 3 | DiagnosticsQuery-Texte | `context.User`→`User`, `context.RequestServices`→`Services` |
| 4 | `IContextUserProvider` | `HttpContext`-Member → `IHttpContextUserProvider` |
| 5 | `CookieScopeOptions.DefaultScopeExpression` | `Func<HttpContext,…>` → `Func<IContextUserProvider,…>` |
| 6 | Blazor-Host | `AddBlazorContextUser()` + `<ContextUserInitializer/>` + `<TenantUrlGuard/>` + `AddBlazorPermissionScope(…)` + `AddWebCoreToolkitServiceShared()` |
| 7 | **Onboarding EF** (2a/2b) | **`modelBuilder.ConfigureOnboardingModel()` bedingungslos im `OnModelCreating`** (Keys/FKs, design-time-relevant) + `dotnet ef migrations add` → neue Tabellen `PendingOnboarding` + `TenantInvitation` (unique `Token`); `InvitationStatus.Expired` = kein Schema-Change |
| 8 | **Onboarding Config** (2c) | optional `TenantSetup`-GlobalSetting um `AllowRootTenantCreation` / `DefaultParentTenant` erweitern |
| 9 | **Onboarding Mail/Nav** (2b) | `IAppMailSender` via `UseDefaultMailSender` (auto) oder eigene Impl; Nav-Link auf `/Account/Onboarding/Invitations` |
| 10 | **EntityWriteTracker** (FK-Cache) | optional `ActivationSettings.UseEntityTracker = true` für sofortige FK-Label-Cache-Invalidierung; alter `IForeignKeyWriteTracker` entfallen → `IEntityWriteTracker` |
| 10a | **Permissions/Navigation sofort** | gleicher Schalter invalidiert Cookie-Permission-Cache, Navigator & `isAuthenticatedCache` automatisch; für sofortiges UI-Re-Render optional `<EntityChangeRefresher>` (Blazor) um das Menü legen |
| 11 | **Per-Op-Context** (opt-in, §8) | falls ihr den System-Context als `"sys"`-Dependency konfiguriert: `AddDependency(…, p=>p.GetService<IDbContextFactory<AppCtx>>().CreateDbContext(), disposeWithScope:true)`; Plugin-Konsumenten `using var s = pluginHelper.CreateOperationScope()`; Diag/FK-`RegisterService` ggf. `ScopedDataSource` zurückgeben. **FileHandler** sind automatisch abgedeckt (§8.4) — sobald „sys" scope-owned ist, greift der per-Op-Context im Up-/Download ohne weitere Verdrahtung. Ohne Änderung = bisheriges (geteiltes) Verhalten |
| 12 | **Auto-Permission-Registration** (opt-in, §9, neu `PRE098`) | `ActivationSettings.AutoRegisterRequestedPermissions = true` + `AutoRegisterPermissionsGrantRole = "<globale Admin-Rolle>"` → angeforderte Permissions werden on-the-fly angelegt und der Admin-Rolle gegrantet; danach `Program.cs`-„ensure permissions"-Block entfernen (Sync-Pflicht). Ohne Änderung = bisheriges Verhalten (statischer Seed nötig) |
| 13 | **Onboarding Tenant-Anlage** (§10, neu `PRE108`) | **Pflicht-Migration ab `5.0.0-PRE108`** für neue Spalte `PendingOnboarding.CreatedTenantId` (`dotnet ef migrations add PendingOnboardingCreatedTenantId` → `database update`), sonst Runtime-Crash. Behebt IDENTITY_INSERT-Crash beim Template-Apply + macht den Blazor-Abschluss atomar (keine halben/doppelten Tenants). Keine Config-/API-Änderung. MVC-Flow: nur Crash-Fix, keine Tx-Härtung |
| 14 | **Rollendefinitionen-Tab** (§10.4, `PRE108`) | Automatisch: Haupt-Grid zeigt nur bearbeitbare Zeilen (Kind-Write-gated), Anzeigename bevorzugt (Translate). Optional: `TenantSetup.ForceDedicatedRoleForMappings = true` erzwingt Neu-Rolle beim RoleMapping-Anlegen (kein Picker für bestehende Rollen). Keine Migration |
| 15 | **Delegation-Rolle** (§10.4, `PRE108`) | Dritter RoleMapping-Typ (Enum additiv, keine Migration). **Zwei neue Permissions seeden:** `Onboarding.Admin.RoleMappings.Delegation` (Voll-Edit) + `Onboarding.Admin.RoleMappings.DelegationAssign` (nur sehen + PermissionSets zuweisen). Via Auto-Permission-Registration (§9) sonst automatisch |
| 16 | **Paket-Versionen** (§12, `PRE118`) | NuGet-Refs auf `ITVComponents 5.0.0-PRE118`; Framework/3rd-party mitziehen (EFCore/AspNetCore/Extensions/System.* `10.0.9`, Npgsql `10.0.2`, MudBlazor `9.6.0`, BlazorMonaco `3.5.0`, Scriban `7.2.5`, OpenApi `3.8.0`, Saml `8.19.1`, Azure.Blobs `12.29.1`, protobuf `3.35.1`/grpc.tools `2.82.0`, PrettyPrompt `6.0.4`, PS.Automation `7.6.3`, WPF-Toolkit `5.1.2`, Stripe `52.1.0`). **NICHT** anheben: `Microsoft.CodeAnalysis.*` (bleibt `5.0.0`, sonst NU1107 mit EFCore.Design), Test-Stack (Test.Sdk 17, MSTest 3) |
| 17 | **ItvErrorBoundary** (§13, opt-in) | `<ItvErrorBoundary>@Body</ItvErrorBoundary>` im Host-Layout (`@using …Blazor.SharedComponents`) → Komponenten-Fehler reißen den SignalR-Circuit nicht mehr ab. Override-Slots `Notification`/`Actions` mit `ItvErrorContext` |
| 18 | **PermissionSet cross-tenant** (§14, `PRE118`) | **Pflicht:** neue Migration mit `SqlColumnsSyntaxHelper.ConfigureViews(migrationBuilder)` (deployt neue TVF `GetEffectiveTenantUserRoles` + regenerierte Rollen-Tree-Procs). Ohne = Downline-Propagation aktivierter PermissionSets greift nicht. Kein Schema-Change, LINQ-Zwilling ohne Migration |
| 19 | **Tenant-Template** (§15, opt-in) | optional `TenantSetup.BasicTenantType` (Template über TenantType statt Name); per-Bereich Apply-Modes `Auto`/`Additive`/`Forced` (Default = altes Forced-Verhalten); `Extensions`-Modell geändert (Payload seit §18 typisiert-polymorph, **nicht** mehr back-compat — s. Zeile 22); Re-apply via `services.ApplyTenantTypeTemplate(...)` bzw. Grid-Button. Kein Schema-Change |
| 20 | **Billing Add-ons n:m** (§16) | **Pflicht:** im App-Context DbSet `AddOnPrices` → **`PlanAddOns`** + **`PlanAddOnPrices`** (`IBillingContext` geändert, sonst Compile-Break); **neue Migration** `BillingAddOnPlanLink` (dropt `AddOnPrices` + `AddOn.BillingInterval`, legt `PlanAddOns`/`PlanAddOnPrices` an). Add-on-Preis/Buchbarkeit jetzt pro Plan, Interval vom Plan geerbt. UI/Checkout/Sync automatisch. Ersetzt das Add-on-Multi-Currency-Delta |
| 21 | **Config-Export erweiterbar + Billing-Sektion** (§17, opt-in) | Kein Schema-Change. Für Billing im Export: `services.AddBillingConfigExtension()` beim Startup + Config-Handler-Plugin bekommt `IServiceProvider` in den Ctor (optionaler 2. Param) + Context implementiert `IBillingContext`. Ohne = Billing fehlt im System-Config (kein Crash). Provider-IDs/Subscriptions bewusst ausgeschlossen. Neue Erweiterungspunkte in `EFRepo.DataSync` (`IConfigExtension`/`AddSystemConfigExtension`) |
| 22 | **TenantTemplate-Extensions typisiert** (§18) | Payload `string` → polymorpher `TemplateExtensionPayload`; `ITenantTemplatePartHandler.Extract/Apply` typisiert; Legacy-string-Converter entfällt. **Clean Cut:** in DB gespeicherte Templates **mit** Extensions (EmployeeRoleMappings) brechen beim Deserialisieren → betroffene Templates neu extrahieren. Templates ohne Extensions unberührt. Keine Migration. Library-seitig erledigt |
| 23 | **Navigations-Metadata** (§19, `PRE130`) | **Pflicht-Migration** für neue Spalte `NavigationMenu.Metadata` (`dotnet ef migrations add NavigationMenuMetadata` → `database update`), sonst schlägt jede Navigations-Query mit *„Invalid column name 'Metadata'"* fehl. Nur additive nullable Spalte, keine Datenmigration. Bestehende Einträge = NULL |
| 24 | **Help-Button + maximierbare Dialoge** (§19, `PRE130`, opt-in/automatisch) | Opt-in: `HelpSlug` als Metadata am Nav-Eintrag + `<HelpButton />` (`@using …AdminViews.HelpViews`) ins Host-Layout → Seiten-Hilfe als Popup (fail-silent). Automatisch: maximierbare Detail-/CodeEditor-Dialoge, Hilfe tenant-präfixiert + Medien-Skalierung, Config-Export-Härtung; Billing-Export-Sektion jetzt via WebPart-Flag `BillingConfigExportPartOptions.ActivateBillingConfigExport` (statt manuellem `AddBillingConfigExtension()`, §17) |
| 25 | **System-Log „Eintrag verfolgen" + Index** (§20) | Kein Breaking Change, keine Config, keine neue Permission — der Augen-Button in `/Util/SystemLog` zeigt je 20 Einträge vor/nach einer Nachricht (einstellbar). **Empfohlen:** Index `IX_SystemLogEventTime` auf `SystemLog (EventTime, SystemEventId)` **manuell** nachziehen (SQL in §20) — **nicht** via `dotnet ef migrations add`, der Snapshot driftet und würde fremde Änderungen mitschleppen. Ohne Index läuft alles, sortiert aber über die ganze Tabelle |
| 26 | **Zustimmungen im Onboarding** (§21) | **Pflicht:** DbSet `ConsentRecords` im Onboarding-Context (beide Context-Interfaces erweitern neu `IOnboardingConsentContext`, sonst Compile-Break) + Tabelle `ConsentRecord` **manuell** anlegen (SQL in §21.1) + Spalte `HelpTopic.ShowInMenu bit NOT NULL DEFAULT 1` (§21.4). Beide Onboarding-Handler haben `IConsentProvider` als neuen Ctor-Parameter (über `AddMudBlazor*OnboardingViews` automatisch). `BillingProfileViewModel.AcceptTos` hat seine Pflicht-Annotation verloren, `BillingProfileForm` braucht neu den Parameter `ConsentPoints` — wer beides ohne die mitgelieferten Seiten verwendet, muss selbst prüfen bzw. setzen. **`Scope` je Punkt (`User`/`Tenant`/`Both`, Default `User`) entscheidet, ob eine Zustimmung einmalig der Person gilt oder mit jedem Mandanten neu fällt** (§21.2.1). Ohne GlobalSetting `Consent` bleibt es beim einen eingebauten Schalter (Verhalten wie bisher, weiterhin ohne Nachweis). **Neue Permission `Onboarding.Admin.Consents.View` seeden** (§21.6) für den Reiter *Zustimmungen*; ein Schreib-Gegenstück gibt es bewusst nicht. Neue Konto-Seite `/Account/Onboarding/MyConsents` (nur Anmeldung nötig) — dort lassen sich freiwillige Zustimmungen ändern, Pflicht-Punkte nur nachlesen |
| 27 | **Selbstregistrierung + Standard-Mandant** (§22) | Kein Schema-Change. `/Account/Register` existiert neu (der Verweis auf der Anmeldeseite lief bisher ins Leere) und ist **standardmässig abgeschaltet**. Freigeben mit `TenantSetup.AllowSelfRegistration = true` **plus** `DefaultUserTenant` (+ `DefaultUserTenantRole`) — sonst landet der Registrierte in einer leeren Mandanten-Übersicht. Zuweisung nach der Mailbestätigung, nur wenn der Benutzer nirgends Mitglied ist und keine Einladung wartet. `IOnboardingHandler` hat ein neues Member (`AssignDefaultTenantAsync`) — **eigene Implementierungen des Interfaces brechen**. Der Verweis auf der Anmeldeseite hängt jetzt zusätzlich an `ISelfRegistrationPolicy` (neu in `WebCoreToolkit/Security`, optional aufgelöst — ohne Onboarding-Paket unverändert). `JoinRegister` ist unverändert erreichbar und braucht das Flag nicht |
| 28 | **Zusatzangaben-Module** (§23) | Kein Schema-Change, opt-in. Ohne GlobalSetting `CustomCompanyInfo` passiert nichts. Module sind globale Plugins nach `ICustomCompanyInformationHandler` (Blazor-frei); Reiter erscheinen im Onboarding **und** neu im Firmenprofil (Tab 1), dort gated durch `EditPermission` des Moduls — geprüft beim Anzeigen **und** beim Schreiben. Der Feld-Renderer ist nach `Blazor.MudBlazor/SharedComponents/DeclaredFieldsForm.razor` gewandert (`UserTaskFieldsForm` = Adapter, API unverändert); zwei Verhaltenskorrekturen betreffen auch die Workflow-Aufgabenmasken (Ja/Nein-Pflichtfeld startet auf `false`, Vorbelegung ohne `ResetKey`) |
| 29 | **Anonym ladbare Plugins** (§24) | **Pflicht:** Spalte ``WebPlugins.AllowAnonymous bit NOT NULL DEFAULT 0`` **manuell** anlegen (SQL in §24.1) — **nicht** via ``dotnet ef migrations add`` (Snapshot-Drift, s. §20). Sonst schlägt jede Plugin-Query mit *„Invalid column name 'AllowAnonymous'"* fehl. Danach je Plugin setzen, das **vor** der Anmeldung greifen soll — allen voran die Zusatzangaben-Module aus §23, sonst fehlt ihr Reiter im anonymen Onboarding. Nur für **globale** Plugins wirksam (Mandanten-Zeilen liefern immer ``false``). Greift nur im Anonym-Fall; für angemeldete Benutzer entscheidet weiter die Berechtigung. Neue ``VerifyUserPermissions``-Überladung mit ``out bool isUserAuthenticated`` (additiv) |
| 30 | **Blazor-Dashboard** (§25) | **Pflicht:** vier Spalten **manuell** anlegen (SQL in §25.1) — `Widgets.InitiallyActive bit NOT NULL DEFAULT 0`, `Widgets.SortOrder int NOT NULL DEFAULT 0`, `UserWidgets.ColSpan int NOT NULL DEFAULT 0`, `UserWidgets.ParamValues nvarchar(max) NULL`; **nicht** via `dotnet ef migrations add` (Snapshot-Drift, s. §20). Sonst schlägt jede Widget-Query mit *„Invalid column name 'InitiallyActive'"* fehl. Neu: Fläche `<DashboardHost>` + fertige Seite `/Dashboard`, Daten über `IDiagnosticsQueryService` **ohne** HTTP-Umweg. Standard-Sammlung über **Initially active** + **Sort order** im Widget-Editor; wer noch keine eigenen Widgets hat sieht sie ungespeichert, beim ersten Bearbeiten wird sie kopiert. **Template-Bruch:** gerendert wird mit **Scriban**, `{{ ->Key }}` und `{{ $expr }}`/`{{ !$expr }}` aus `processMessage` funktionieren **nicht** mehr (Tabelle in §25.4); Modell ist `Rows`/`Row`/`Count`/`Params`/`Title`. `InputConfig` hat eine neue, neutrale Form (§25.5) — alte Kendo-Configs werden ignoriert statt zu brechen, `MaskedText` wird Textfeld. Parameter-Eingaben werden neu mitgespeichert und sind später änderbar. `/DBW` schleift `ColSpan`/`ParamValues` mit; eigene Kopien dieser Endpunkte müssen das auch (§25.6). **Mobil** (§25.7): Spaltenzahl folgt dem Viewport (Xs = 1, Sm = Hälfte), gespeicherter `ColSpan` bleibt die Absicht des Benutzers; **Umsortieren per Ziehen geht auf Touch nicht** (HTML5-Drag-Ereignisse) → dafür „nach vorn/nach hinten" im Kachel-Menü |
| 31 | **Fortlaufender Aufgaben-Dialog** (§26) | Kein Schema-Change, opt-in. `UserTaskDialog` kann mit `Continuous=true` (und leerer `TokenId` = „nächste Aufgabe dieser Instanz") einen mehrstufigen Vorgang in **einem** Dialog durcharbeiten; wartet zwischen zwei Benutzer-Schritten auf automatische Aktivitäten (`WaitTimeout`, Standard 30 s). `IWorkflowTaskHandler` hat ein neues Member `FindNextAsync` — **eigene Implementierungen des Interfaces brechen**. Welche Schritte geführt werden, sagt das **Modell**: neue Knotenfelder `RunsInAssistant` (Schalter) und `EndsAssistant` (CScript, beim Abschluss nach dem Übernehmen der Ergebniswerte ausgewertet), beide im Editor-Reiter *General*; Ergebnisse in `UserTaskDescriptor.RunsInAssistant` bzw. `UserTaskCompletionResult.EndsAssistant` (Ctor additiv erweitert). Der Dialog-Parameter `Continuous` ist deshalb **`bool?`** (null = Modell entscheidet) — ein Assistent, den man zwischendurch schließt, kommt aus der Arbeitsliste heraus als Assistent zurück. Bestehende Definitionen unberührt. **Empfohlen:** `EntityWriteTrackerInterceptorOptionsLoader<WorkflowContext>` über den vorhandenen Options-Loader legen (§26.2), sonst wartet der Dialog im 3-s-Takt statt auf Weckruf. **Umzug:** `EntityChangeSignal<TContext>` → `ITVComponents.WebCoreToolkit.EntityFramework.Caching` (Vertrag/Registrierung/Verhalten unverändert, nur `using` anpassen, wenn die Klasse namentlich verwendet wird). **Neu, optional:** Sammelfenster je Thema für den Weckruf (`EntitySignalDebounceSettings`, §26.3) — über `ActivationSettings.EntityChangeSignal`, `services.Configure` oder den GlobalSettings-Eintrag `EntityChangeSignal` (der gewinnt). Verzögert **nur** die aktive Benachrichtigung, nie den Zeitstempel; vorbelegt ist einzig `WorkflowProgress` mit 250 ms, `Security`/`Navigation` melden wie bisher sofort |
| 32 | **Export-Profile für den System-Config** (§27) | Kein Schema-Change, opt-in. Ohne Konfiguration unverändert (eingebaut sind `Full` und `BasicOnly`). Profile über `services.Configure<ConfigExportProfileOptions>(…)` — **nicht** über GlobalSettings, die Profile werden gebraucht, bevor eine Neuinstallation Settings hat. `ActiveExtensions` benennt **Section-Keys**, nicht Handler-Typen; weggelassen = alle registrierten. Der Download trägt das Profil im Bezeichner (`sysCfg@Help`), der **Upload bleibt auf `sysCfg`** (neuer, optionaler `ConfigUploadIdentifier`; ohne ihn wird der Profil-Anteil automatisch abgeschnitten). **Wichtigster Teil ist die Absicherung:** die Basis-Sektionen des Vergleichs sind jetzt alle null-geschützt, und ein Teil-Export erklärt seine Absicht per `OmitBasicData` **in der Datei** — sonst käme eine Datei ohne Grunddaten als vorausgewählter Löschlauf über Plugins, Permissions, Rollen, Navigation und Settings aus dem Diff. Ältere Exportdateien profitieren mit |
| 33 | **Hilfesystem im Config-Export** (§28) | Kein Schema-Change, opt-in, standardmässig aus. Einschalten per WebPart-Flag `HelpConfigExportPartOptions.ActivateHelpConfigExport = true` (WebPart `…EntityFramework.HelpSystem.WebPartInit`) oder `services.AddHelpConfigExtension()`. Voraussetzung wie bei Billing: Context implementiert `IHelpSystemContext` **und** das Config-Handler-Plugin bekommt den `IServiceProvider` in den Ctor — sonst bleibt die Sektion still wirkungslos (kein Crash). Übertragen werden Themen (Schlüssel `Slug`), Inhalte je Kultur, Ordner, Ressourcen-Einträge **und die Dateien samt Inhalt** (Base64 inline, vorbelegt 2 MB je Datei / 20 MB gesamt, einstellbar über `Contents` in den WebPart-Optionen). **Nur aus dem eingebauten EF-Blob-Store** — bei eigenem `IHelpResourceStore` reisen Metadaten, und der Diff meldet gesammelt, welche Dateien deshalb fehlen. Beim Import wird der `FileIdentifier` **neu vergeben**; SHA-256 je Datei verhindert, dass unveränderte Blobs neu geschrieben werden. Ordner werden nur angelegt, nie geändert oder gelöscht (bewusst, s. §28). Neue ProjectReference `HelpSystem → EFRepo` |
| 34 | **`ChangeDetail`: Anzeige ≠ Nutzlast** (§28.2) | Kein Breaking Change, rein additiv: `DisplayValue`, `DisplayCurrentValue`, `ReadOnly`. Ist `DisplayValue` gesetzt, zeigt der Diff-Dialog diese Zusammenfassung statt des Rohwerts (und kein Eingabefeld); `NewValue` bleibt die Nutzlast, `SimpleDataApplyer` unverändert. Nötig für Base64-Inhalte — **ohne `ReadOnly` zerstört ein Tastendruck im Feld die Datei**. Bestehende Extensions und Dialoge verhalten sich unverändert (alles null/false vorbelegt); eigene `IConfigExtension`s können die Felder ohne Vertragsänderung am Ergebnis von `MakeDetail(...)` setzen |
| 35 | **Workflows laufen von selbst an** (§29) | **Pflicht-Migration:** `dotnet ef database update` für den `WorkflowContext` (Migration `WorkflowStartTriggers`, für SQL Server **und** PostgreSQL mitgeliefert; Snapshot geprüft, keine Fremd-Änderungen). Alternativ das gleichwertige SQL in §29.1. Ohne die Tabelle schlägt jedes Speichern einer Workflow-Definition mit *„Invalid object name 'WorkflowStartTriggers'"* fehl. Neu am Start-Knoten: **Nachrichten-Start** (Name + Modus `AlwaysStart`/`CorrelateOrStart`/`StartIfNoneRunning`) und **Zeitplan-Start** (Muster + feste Startwerte + „überspringen, solange der vorige Lauf läuft"). Beides gilt für den **Mandanten der Definition**; öffentliche Definitionen lösen bewusst nicht aus. Bestehende Definitionen sind unberührt und bekommen ihre Auslöser erst beim nächsten Speichern. `IWorkflowStore` hat **fünf neue Member** (`FindMessageTriggers`, `ClaimDueScheduleTriggers`, `UpdateScheduleTrigger`, `PeekNextScheduleDueUtc`, `HasRunningInstance`) — **eigene Store-Implementierungen brechen**. Betrieb: fällige Zeitpläne werden mit **Anspruch** aufgegriffen (sonst liefe derselbe Auftrag je Cluster-Knoten einmal an); verpasste Termine werden **einmal** nachgeholt, und ein gescheiterter oder übersprungener Start schiebt den Termin trotzdem weiter |
| 36 | **`TimeTable` umgezogen** (§29.4) | **Breaking:** `ITVComponents.ParallelProcessing.TaskSchedulers.TimeTable` → **`ITVComponents.Scheduling.TimeTable`** (Kernpaket `ITVComponents`, kein neues Paket). Nur `using` anpassen, Verhalten und Muster-Format unverändert. Grund: ausser dem Aufgaben-Verteiler brauchen sie inzwischen auch der Web-Hintergrunddienst und die Workflow-Auslöser — keiner davon soll deswegen auf `ParallelProcessing` verweisen müssen. Neu additiv: `TimeTable.RunsImmediately`/`Pattern` (lesend) und `ITVComponents.Scheduling.ScheduleEvaluator` — rechnet in Ortszeit, liefert **UTC**, prüft Muster mit Klartext-Meldung (auch solche, die formal stimmen und nie zutreffen) und liefert eine Termin-Vorschau |
| 37 | **Nachrichten-Empfang am Schritt** (§30) | Kein Schema-Change, opt-in, rein additiv. Neuer Knoten `BoundaryMessageNode` (Diskriminator `boundarymessage`) — hängt wie der Fristen-Timer an einem parkenden Schritt und feuert auf eine eintreffende Nachricht, unterbrechend oder als Nebenpfad. **Merke: ein nicht unterbrechender Empfang bleibt scharf und feuert wieder** (anders als der Timer, dessen Fristenliste einmal durchläuft) — `CountVariable` zählt mit. Nutzdaten gehen beim Nebenpfad in dessen Scope-Kopie, beim Abbruch in den Hauptfluss. Bestehende Definitionen unberührt; wer eigene Knoten-Editoren pflegt, bekommt den Typ erst mit der Designer-Erweiterung angeboten |
| 38 | **Vorgänge anhalten/fortsetzen** (§31) | **Pflicht-Migration:** `dotnet ef database update` für den `WorkflowContext` (Migration `SuspendAndFaultCode`, beide Provider, Snapshot geprüft) — legt `WorkflowInstances.Suspended`, `SuspendedReason`, `FaultCode` an und ersetzt `IX_WorkflowInstances_Status_Priority` durch `IX_WorkflowInstances_Status_Suspended_Priority`. Ohne die Spalten schlägt jede Instanz-Query fehl. `IWorkflowMonitorHandler` hat ein neues Member `SetSuspendedAsync` — **eigene Implementierungen des Interfaces brechen**. **Merke: angehalten ist ein eigenes Feld, KEIN `WorkflowStatus`-Wert** (der Vortrieb setzt den Status ständig auf Running zurück und hätte es still aufgehoben); die Instanz behält ihren Status und trägt das Kennzeichen daneben. Nachrichten und Fristen erreichen eine angehaltene Instanz weiterhin — sie laufen nur nicht los. `Faulted` ist ausdrücklich anhaltbar, `Completed`/`Cancelled` nicht |
| 39 | **Fehler-Code** (§32) | Rein additiv, kein Breaking Change. `ctx.Fail(message)` bekommt eine zweite Form `ctx.Fail(message, code)`; neue Knotenfelder `ErrorCodeVariable` an Aktivität und Subworkflow-Aufruf (Editor: *Error code → variable*), neues `WorkflowInstance.FaultCode` (Spalte kommt mit der Migration aus §31). **Damit kommt die Fehlerart aus einem Subworkflow heraus** — bisher erfuhr der Aufrufer nur eine Meldung und hätte sie parsen müssen. Eine abgestürzte Aktivität lässt den Code leer; er wird bei jedem Fehlerlauf gesetzt (auch auf leer), damit nicht der Code von vorhin stehen bleibt |
| 40 | **Kommentare am Vorgang** (§33) | **Pflicht-Migration:** `WorkflowComments` (beide Provider) — neue Tabelle, FK auf `WorkflowInstances` kaskadierend, Index `(InstanceId, CreatedUtc)`. `IWorkflowTaskHandler` hat zwei neue Member (`ListCommentsAsync`, `AddCommentAsync`) — **eigene Implementierungen brechen**. UI: aufklappbarer Faden unter der Aufgaben-Maske, Texte in allen vier Sprachen. Es genügt `Workflow.Tasks`. **Merke: der Faden hängt am VORGANG, nicht an der Aufgabe** (die verschwindet mit ihrem Abschluss, der Faden soll bleiben), und die Engine kennt ihn nicht — ein Kommentar ist kein Prozess-Zustand. **Anhänge sind bewusst nicht dabei** (eigener Schritt, braucht den Datei-Handler) |
| 41 | **Post-Hook für Aufgaben-Masken** (§34, MLM-Antrag) | Kein Schema-Change, opt-in, **nicht breaking**: `IUserTaskView.PostResolveActivityAsync(UserTaskCompletionResult)` ist eine **Default-Interface-Methode** — bestehende Masken merken nichts. Gerufen nach dem Abschluss am einen Abschlussweg und **vor** dem Umhängen im geführten Ablauf. **Merke: das Ergebnis auswerten** — bei `AlreadyCompleted` hat jemand anderes abgeschlossen, dann darf die Maske nicht auch noch schreiben. Ein Fehlschlag hält nichts auf, wird dem Benutzer aber angezeigt und bleibt stehen, bis er ihn wegklickt |
| 42 | **Muster-Designer + strengere Muster-Prüfung** (§35) | Kein Schema-Change. Neu `ITVComponents.Scheduling.SchedulePattern` (zerlegen/zusammensetzen) und ein Kalender-Knopf am Zeitplan-Feld mit Termin-Vorschau. **Achtung, Verhaltensänderung:** der Muster-Regex ist nicht verankert, deshalb wurde ein Muster mit vertauschten Teilen bisher still verkürzt gelesen (und der Plan lief zu einer anderen Zeit als angezeigt). Zerlegung und `ScheduleEvaluator.TryValidate` verankern jetzt selbst — ein solches Muster **meldet der Validator künftig als Fehler**, was eine bestehende Definition beim nächsten Speichern als fehlerhaft markieren kann |
| 43 | **Anhänge am Vorgang** (§36) | **Pflicht-Migration:** `WorkflowAttachments` (beide Provider) — zwei Tabellen (Beschreibung mit FK+Index, Blobs ohne FK). `IWorkflowTaskHandler` bekommt **vier** weitere Member (`ListAttachmentsAsync`, `AddAttachmentAsync`, `OpenAttachmentAsync`, `DeleteAttachmentAsync`) — **eigene Implementierungen brechen**. Grösse über `WorkflowViewsOptions.MaxAttachmentBytes` (10 MB; **0 schaltet Anhänge ab**). **Merke: NICHT über den `IFileHandler`** — dessen Vertrag gibt keine Datei-Kennung zurück, mit der sich ein Anhang später lesen liesse; stattdessen `IWorkflowAttachmentStore` mit eingebauter Datenbank-Ablage, austauschbar wie beim Hilfesystem. Löschen nur den eigenen Anhang |
| 44 | **Zentrale Abläufe per Aktivierung** (§37) | **Pflicht-Migration:** neue Tabelle `WorkflowStartTriggerActivations` + 7 Spalten an `WorkflowStartTriggers`, **Datenumzug des Lauf-Zustands** (`NextDueUtc`/`LastRunUtc`/`LastInstanceId`/Lease ziehen von der Auslöser- auf die Aktivierungs-Zeile), erst danach die alten Spalten fallen lassen. **Regelweg ist die mitgelieferte Migration `TriggerActivations` (beide Provider) — sie bewegt Daten, nicht nur Schema; Handarbeit-SQL gleichwertig in §37.5.** Nicht neu scaffolden: beide Migrationen sind nachbearbeitet (das Gerüst deutete `LeaseOwner`/`LastInstanceId` als Umbenennung nach `RequiredPermission`/`RequiredFeature` und liess den Datenumzug weg). `IWorkflowStore` bekommt 6 neue Member und ändert 2 Signaturen (`FindMessageTriggers`, `ClaimDueScheduleTriggers`) — **eigene Store-Implementierungen brechen**. **Verhaltensänderung (§37.4): Nachrichten tragen jetzt einen Ursprungs-Mandanten**, und nur der lässt etwas anlaufen; ohne Ursprung feuert nur, was `AllowTenantlessStart` erlaubt. Wer aus Host-Code ohne Mandanten-Kontext sendet, startet danach nichts mehr (wird protokolliert). Das schliesst eine Flanke, die es schon vorher gab: eine mandantenlose Nachricht eröffnete bei hundert Mandanten hundert Vorgänge. **Merke: die Aktivierung hängt an der fachlichen Identität, NICHT am `TriggerKey`** — der wird bei jedem Speichern der Definition neu vergeben. Feature/Permission an der Definition (nur Sysadmin) gaten die Verwendung; das **Feature wird bei jedem Feuern** nachgeprüft (`IWorkflowTenantFeatureGate`, ohne Verdrahtung erlaubt es alles), die Permission nur beim Anhaken und beim Start von Hand. Fehlt das Feature: überspringen + protokollieren, Fälligkeit trotzdem fortschreiben, **nicht** abhaken. Ein-Mandanten-Betrieb unberührt |
| 45 | **Passkeys sind opt-in** (§38) | **Pflicht, wenn ihr Passkeys benutzt** — sonst verschwindet der Abschnitt aus der Kontoverwaltung. Zwei Dinge müssen zusammenkommen: euer eigener Passkey-Block im `ApplicationDbContext` (der **muss auch etwas über `Data` sagen**, sonst kommt `IdentityPasskeyData requires a primary key` zurück) **und** neu `IdentityUiOptions.UsePasskeys = true` (**Standard `false`**). Hintergrund: .NET 10 schliesst den Passkey-Typ ausdrücklich aus, und `TableNamesFromProperties` hat diesen Ausschluss bisher überstimmt — dadurch scheiterte **jeder** Identity-Kontext der Bibliothek beim Bauen des Modells, auf SQL Server genauso, in `dotnet ef` **und** zur Laufzeit. Kein Schema-Change. **Merke: `UserManager.SupportsUserPasskey` taugt nicht als Schalter** — der EF-Speicher meldet immer `true`, auch ohne abgebildete Entität. Prüfen mit `dotnet ef dbcontext info --context ApplicationDbContext` |
| 46 | **PostgreSQL für den Mandanten-Baum** (§39, optional) | **Wer bei SQL Server bleibt, muss nichts tun.** Die Ausprägung `CoreIdentityTree` gibt es jetzt auch für PostgreSQL (Initialmigration + 5 Views + 10 Funktionen). Beim Wechsel: WebPart auf `Identity = CoreIdentity`, `Strategy = Tree`, dann Initialmigration und eine Migration mit `PostgreSqlColumnsSyntaxHelper.ConfigureViews(migrationBuilder)` (wiederholbar). **Euer eigener Kontext ist damit nicht erledigt** — dessen PostgreSQL-Migrationen kommen aus eurem Repo. Zwei Verhaltensunterschiede: der Zyklen-Abbruch meldet `SQLSTATE 54001` mit Klartext statt Fehler 530 (bewusst eine Ausnahme statt der still abbrechenden `CYCLE`-Klausel), und `GetChildTenantsWithPermsProc` ist dort eine **Funktion** — eigenes SQL ruft `select * from "…"(…)` statt `exec`. Geprüft: dieselbe Hierarchie beidseitig, 95 Zeilen Zeile-für-Zeile ohne Unterschied. **Inzwischen auch das Laufzeitverhalten unter Last** — siehe §40, die dort gefundene Lücke ist geschlossen |
| 47 | **Mandanten-Baum: Anker-Fix** (§40) | **Pflicht-Migration für BEIDE Provider:** eine Migration, die `ConfigureViews(migrationBuilder)` des jeweiligen Providers erneut ausführt (wiederholbar). Kein Schema-Change, keine Vertragsänderung — ohne sie bleiben schlicht die alten, langsamen Objekte stehen. Ein Filter auf ein Baum-Blatt kam bisher **nach** der Rekursion zum Zug; wo er aus einem **Join** stammt (Blickpunkt aus einer Tabellenvariablen in den beiden Rollenbaum-Prozeduren), baute **jede** der beiden Datenbanken den ganzen Baum. Bei 10 000 Mandanten: Rollen-Baum nach unten 5 007 → **166 ms** (SQL Server) bzw. 4 703 → **68 ms** (PostgreSQL), Kind-Mandanten mit Berechtigung 4 839 → **151 ms** bzw. 5 087 → **71 ms**; der Mandantenfilter auf PostgreSQL 280 → **1 ms**. Ergebnisse unverändert: alte gegen neue Fassung auf beiden Datenbanken und beiden Fixtures **null Unterschiede**, Gleichheitstest und Zyklen-Wächter unverändert. Einziger Preis: ein Durchlauf des ganzen Baums **ohne** Filter kostet auf PostgreSQL mehr (286 → 800 ms) — im Toolkit-Code kommt er nicht vor |
| 48 | **Token-Zeilen mandantengefiltert** (§41) | **Pflicht-Migration `TokenTenantBackfill`** (beide Provider), **vor** dem ersten Start mit der neuen Fassung. Kein Schema-Change — sie trägt den denormalisierten Mandanten an Token-Zeilen nach, die ihn noch nicht haben. Betroffen sind Vorgänge, die seit vor der Migration `UserTasks` **parken**: die wurden seither nie gespeichert und tragen `NULL`. Ohne den Nachtrag verschluckt der neue Filter deren Tokens — und **die Fehlerart ist hier eine andere als sonst: nicht „sieht zu viel", sondern „sieht nichts", also ein Vorgang, der ohne Meldung stehen bleibt.** Für eigenen Code: `db.Tokens` liefert ab jetzt nur die Zeilen des aktiven Mandanten (bewusst; wer darüber hinaus lesen will, setzt `IgnoreQueryFilters()`). Der Runner muss filterfrei bleiben — sein Suchlauf geht jedem `WorkflowExecutionScope` voraus |
