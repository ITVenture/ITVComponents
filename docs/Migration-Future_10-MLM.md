# Migrationsleitfaden — Branch `Future_10` (Phasen 2–5)

Dieser Leitfaden beschreibt, was im MLM-Projekt anzupassen ist, um auf den `Future_10`-Stand der
ITVComponents-Toolkit zu wechseln. Es ist ein **Major-Release (5.0-PRExx)** mit bewussten Breaking
Changes (keine Shims). Die Umbauten ziehen MVC-/HTTP-Kopplung aus der Kernlogik heraus, damit MVC **und**
Blazor dieselben Services teilen. Die neue, framework-neutrale Assembly heißt
`ITVComponents.WebCoreToolkit.ServiceShared`.

Reihenfolge der Abschnitte = empfohlene Reihenfolge der Migration. Pro Abschnitt: **was bricht** →
**wie anpassen**.

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

Der **Read-Pfad** (`ReadFile` → `AsyncReadFileResult`) ist unverändert, nur der Namespace wandert nach
`…ServiceShared.FileHandling`.

---

## 2. Responding-FileHandler / Config-Exchange (Phase 4) — **bricht eigene Responding-Handler**

`IRespondingFileHandler` / `IAsyncRespondingFileHandler` liegen jetzt ebenfalls in ServiceShared, und
`GetUploadResult()` gibt statt eines MVC-`IResult` ein neutrales **`FileUploadResponse`** zurück.

| vorher | nachher |
|---|---|
| `using ITVComponents.WebCoreToolkit.Net.FileHandling;` | `using ITVComponents.WebCoreToolkit.ServiceShared.FileHandling;` |
| `IResult GetUploadResult()` | `FileUploadResponse GetUploadResult()` |
| `Results.Text(json)` / `Results.Bytes(…)` | `FileUploadResponse.Text(json)` / `FileUploadResponse.Bytes(bytes, contentType, downloadName)` |

```csharp
// vorher
public IResult GetUploadResult() => Results.Text(diffJson, "application/json");

// nachher
public FileUploadResponse GetUploadResult() => FileUploadResponse.Text(diffJson);
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

**Eine `<ContextUserInitializer />`-Komponente nahe der App-Wurzel platzieren** (einmal) — sie seedet den
synchronen `User`-Getter nach dem ersten interaktiven Render und hält ihn über
`AuthenticationStateChanged` aktuell.

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

---

## 6. Verifikation auf eurer Seite

- Build der gesamten Solution grün (alle eigenen FileHandler + Cookie-Scope-Config angepasst).
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
| 2 | `IRespondingFileHandler.GetUploadResult` | `IResult` → `FileUploadResponse`; Namespace → `ServiceShared.FileHandling` |
| 3 | DiagnosticsQuery-Texte | `context.User`→`User`, `context.RequestServices`→`Services` |
| 4 | `IContextUserProvider` | `HttpContext`-Member → `IHttpContextUserProvider` |
| 5 | `CookieScopeOptions.DefaultScopeExpression` | `Func<HttpContext,…>` → `Func<IContextUserProvider,…>` |
| 6 | Blazor-Host | `AddBlazorContextUser()` + `<ContextUserInitializer/>` + `AddBlazorPermissionScope(…)` + `AddWebCoreToolkitServiceShared()` |
