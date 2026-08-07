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

### 21.6 Wo welche Zustimmung erscheint

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

## Schnellübersicht der Breaking Changes

| # | Was | Aktion |
|---|---|---|
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
| 26 | **Zustimmungen im Onboarding** (§21) | **Pflicht:** DbSet `ConsentRecords` im Onboarding-Context (beide Context-Interfaces erweitern neu `IOnboardingConsentContext`, sonst Compile-Break) + Tabelle `ConsentRecord` **manuell** anlegen (SQL in §21.1) + Spalte `HelpTopic.ShowInMenu bit NOT NULL DEFAULT 1` (§21.4). Beide Onboarding-Handler haben `IConsentProvider` als neuen Ctor-Parameter (über `AddMudBlazor*OnboardingViews` automatisch). `BillingProfileViewModel.AcceptTos` hat seine Pflicht-Annotation verloren, `BillingProfileForm` braucht neu den Parameter `ConsentPoints` — wer beides ohne die mitgelieferten Seiten verwendet, muss selbst prüfen bzw. setzen. **`Scope` je Punkt (`User`/`Tenant`/`Both`, Default `User`) entscheidet, ob eine Zustimmung einmalig der Person gilt oder mit jedem Mandanten neu fällt** (§21.2.1). Ohne GlobalSetting `Consent` bleibt es beim einen eingebauten Schalter (Verhalten wie bisher, weiterhin ohne Nachweis) |
| 27 | **Selbstregistrierung + Standard-Mandant** (§22) | Kein Schema-Change. `/Account/Register` existiert neu (der Verweis auf der Anmeldeseite lief bisher ins Leere) und ist **standardmässig abgeschaltet**. Freigeben mit `TenantSetup.AllowSelfRegistration = true` **plus** `DefaultUserTenant` (+ `DefaultUserTenantRole`) — sonst landet der Registrierte in einer leeren Mandanten-Übersicht. Zuweisung nach der Mailbestätigung, nur wenn der Benutzer nirgends Mitglied ist und keine Einladung wartet. `IOnboardingHandler` hat ein neues Member (`AssignDefaultTenantAsync`) — **eigene Implementierungen des Interfaces brechen**. Der Verweis auf der Anmeldeseite hängt jetzt zusätzlich an `ISelfRegistrationPolicy` (neu in `WebCoreToolkit/Security`, optional aufgelöst — ohne Onboarding-Paket unverändert). `JoinRegister` ist unverändert erreichbar und braucht das Flag nicht |
