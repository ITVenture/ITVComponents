# Migrationsleitfaden — Branch `Future_10` (Phasen 2–5 + Onboarding-Flows)

> **Stand: `5.0.0-PRE068`** (Branch `Future_10`). Dieses Dokument deckt die Cross-cutting-Refactors
> (Phasen 2–5), die danach gebauten Onboarding-Flows (2a/2b/2c, Abschnitt 6) **und** die
> EntityWriteTracker-/EntityChangeSignal-Invalidierung (Abschnitt 7/7a) ab.

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
> aktuellen Stand (`5.0.0-PRE068`) **beide** Dokumente durcharbeiten.

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
| `/Account/Onboarding/Invitations` | `[Authorize]` | Admin: Einladungen erstellen/verwalten (2b) | **ja** (Admin-Menü) |
| `/Account/Onboarding/CreateTenant` | `[Authorize]` | Tenant anlegen (bestehend; akzeptiert jetzt `?invitation=`) | bestehend |
| `/Account/Onboarding/MyTenants` | `[Authorize]` | eigene Tenants + Einladungen annehmen (bestehend) | bestehend |

Der deferred 2a-Abschluss passiert idempotent beim ersten Login-Landing (`MyTenants`) bzw.
E-Mail-Confirm — Voraussetzung ist nur, dass eure `/Account/ConfirmEmail`-Page erreichbar ist (Standard).

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

## 9. Verifikation auf eurer Seite

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
| 7 | **Onboarding EF** (2a/2b) | `dotnet ef migrations add` → neue Tabellen `PendingOnboarding` + `TenantInvitation` (unique `Token`); `InvitationStatus.Expired` = kein Schema-Change |
| 8 | **Onboarding Config** (2c) | optional `TenantSetup`-GlobalSetting um `AllowRootTenantCreation` / `DefaultParentTenant` erweitern |
| 9 | **Onboarding Mail/Nav** (2b) | `IAppMailSender` via `UseDefaultMailSender` (auto) oder eigene Impl; Nav-Link auf `/Account/Onboarding/Invitations` |
| 10 | **EntityWriteTracker** (FK-Cache) | optional `ActivationSettings.UseEntityTracker = true` für sofortige FK-Label-Cache-Invalidierung; alter `IForeignKeyWriteTracker` entfallen → `IEntityWriteTracker` |
| 10a | **Permissions/Navigation sofort** | gleicher Schalter invalidiert Cookie-Permission-Cache, Navigator & `isAuthenticatedCache` automatisch; für sofortiges UI-Re-Render optional `<EntityChangeRefresher>` (Blazor) um das Menü legen |
| 11 | **Per-Op-Context** (opt-in, §8) | falls ihr den System-Context als `"sys"`-Dependency konfiguriert: `AddDependency(…, p=>p.GetService<IDbContextFactory<AppCtx>>().CreateDbContext(), disposeWithScope:true)`; Plugin-Konsumenten `using var s = pluginHelper.CreateOperationScope()`; Diag/FK-`RegisterService` ggf. `ScopedDataSource` zurückgeben. **FileHandler** sind automatisch abgedeckt (§8.4) — sobald „sys" scope-owned ist, greift der per-Op-Context im Up-/Download ohne weitere Verdrahtung. Ohne Änderung = bisheriges (geteiltes) Verhalten |
