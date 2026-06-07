# Migrationsleitfaden — Paket-Konsolidierung (Branch `Future_10`, PRE040→PRE041)

Companion zu [`Migration-Future_10-MLM.md`](Migration-Future_10-MLM.md). Während jenes Dokument die
**architektonischen** Breaking Changes beschreibt (ServiceShared, FileHandler, Diagnostics,
ContextUserProvider, PermissionScope), behandelt **dieses** Dokument die **Paket-Konsolidierung**: das
ITVComponents-Toolkit wurde von **103 auf 71 NuGet-Pakete** zusammengelegt. Reine PRE-Phase, daher
bewusste Breaking Changes ohne Shims.

Für den Konsumenten gibt es **drei** Arten von Anpassung:

1. **NuGet-Referenz ersetzen** — eine alte Paket-ID ist weg, der Inhalt steckt in einem neuen Paket. (Betrifft alle Abschnitte.)
2. **`using`-Direktiven anpassen** — nur dort, wo der **Namespace** mit umbenannt wurde (Abschnitt B). Wo nicht erwähnt: Namespace **unverändert**, nur die NuGet-ID ändert sich → kein C#-Diff.
3. **Konfiguration anpassen** — WebPart-Config-Keys (`appsettings-parts.json` o.ä.) + ein paar DI-Setup-Calls (Abschnitt C).

> **Reihenfolge der Migration:** zuerst alle NuGet-Referenzen umstellen (A), dann die `using`-Sweeps (B), dann die Config (C). Nach jedem Build die Fehlerliste abarbeiten — die meisten Treffer sind mechanische `using`/Ref-Fehler.

---

## A. NuGet-ID-Mapping (alt → neu)

Nur Pakete, deren ID sich geändert hat oder die entfernt wurden. Alle übrigen Paket-IDs sind unverändert.
Spalte **NS** = hat sich der C#-Namespace mit geändert? (Details in Abschnitt B.)

### Core-Layer (`ITVComponents.*`)

| Alt (entfernt) | Neu (Inhalt jetzt hier) | NS |
|---|---|---|
| `ITVComponents.StateMachine.PluginDriven` | `ITVComponents.StateMachine` | nein |
| `ITVComponents.ParallelProcessing.TaskSchedulers` | `ITVComponents.ParallelProcessing` | nein |
| `ITVComponents.GenericService.ServiceSecurity` | `ITVComponents.GenericService` | nein |
| `ITVComponents.GenericService.WebService` | `ITVComponents.GenericService` | nein |
| `ITVComponents.UserInterface.DefaultLayouts` | `ITVComponents.UserInterface` | nein |
| `ITVComponents.TypeConversion.DefaultConverters` | `ITVComponents.Plugins` | **ja** |
| `ITVComponents.DataExchange.KeyValueImport` | `ITVComponents.DataExchange` | nein |
| `ITVComponents.DataExchange.Linq` | `ITVComponents.DataExchange` | nein |
| `ITVComponents.DataExchange.TextImport` | `ITVComponents.DataExchange` | nein |
| `ITVComponents.DataAccess.Linq` | `ITVComponents.DataAccess` | nein |
| `ITVComponents.Plugins.ApplicationManagementServices` | `ITVComponents.Plugins` | nein |
| `ITVComponents.Plugins.RuntimeSerialization` | **ersatzlos entfernt** (BinaryFormatter, auf .NET 10 runtime-werfend) | — |

### WebCoreToolkit-Layer

| Alt (entfernt) | Neu | NS |
|---|---|---|
| `…WebCoreToolkit.OnboardingShared` | `…WebCoreToolkit.EntityFramework.Onboarding` | **ja** |
| `…WebCoreToolkit.CustomerOnboarding` | `…WebCoreToolkit.EntityFramework.Onboarding` | **ja** |
| `…WebCoreToolkit.TreeCustomerOnboarding` | `…WebCoreToolkit.EntityFramework.Onboarding` | **ja** |
| `…WebCoreToolkit.OpenIdAuthentication` | `…WebCoreToolkit.Authentication` | **ja** |
| `…WebCoreToolkit.ApiKeyAuthentication` | `…WebCoreToolkit.Authentication` | **ja** |
| `…WebCoreToolkit.WindowsAuthentication` | `…WebCoreToolkit.Authentication` | **ja** |
| `…WebCoreToolkit.AnonymousAssetAccess` | `…WebCoreToolkit.Extras` | **ja** |
| `…WebCoreToolkit.EmailDnsValidation` | `…WebCoreToolkit.Extras` | **ja** |
| `…WebCoreToolkit.DbLessConfig` | `…WebCoreToolkit.Extras` | **ja** |
| `…WebCoreToolkit.ScheduledBackgroundProcessing` | `…WebCoreToolkit.Extras` | **ja** |
| `…WebCoreToolkit.Net.OpenShiftHealth` | `…WebCoreToolkit.Net` | nein |

### Tenant-Security EF (#18, 10 → 3)

| Alt (entfernt) | Neu | NS |
|---|---|---|
| `…EntityFramework.TenantSecurityShared` | `…EntityFramework.TenantSecurity` | **ja** |
| `…EntityFramework.TenantTreeShared` | `…EntityFramework.TenantSecurity` | **ja** |
| `…EntityFramework.AspNetCoreTenants` | `…EntityFramework.TenantSecurity` | **ja** |
| `…EntityFramework.AspNetCoreTreeTenants` | `…EntityFramework.TenantSecurity` | **ja** |
| `…EntityFramework.TenantSecurityContext` | `…EntityFramework.TenantSecurity` | **ja** |
| `…AspNetCoreTenants.SqlServer` / `…AspNetCoreTreeTenants.SqlServer` / `…TenantSecurityContext.SqlServer` | `…EntityFramework.TenantSecurity.SqlServer` | **ja** |
| `…AspNetCoreTenants.PostgreSql` / `…TenantSecurityContext.PostgreSql` | `…EntityFramework.TenantSecurity.PostgreSql` | **ja** |

> Pro Provider **ein** Sub-Paket für **alle** Identity-Strategien. Konsument zieht `*.SqlServer` **oder** `*.PostgreSql`.

### Telerik-MVC AdminViews (#19, 7 → 6)

| Alt (entfernt) | Neu | NS |
|---|---|---|
| `…Net.TelerikUi` (Core) | `…Net.TelerikUi.AdminViews` | **ja** |
| `…Net.TelerikUi.TenantSecurityViews` | `…Net.TelerikUi.AdminViews` | **ja** |
| `…Net.TelerikUi.AspNetCoreIdentityPages` | `…Net.TelerikUi.IdentityPages` | **ja** |
| `…Net.TelerikUi.COB` | `…Net.TelerikUi.Onboarding` | **ja** |

> **Bleiben separat** (nur ggf. NuGet-ID-Stabilität prüfen): die 3 UserView-Pakete
> `…Net.TelerikUi.AspNetCoreTenantSecurityUserView`, `…AspNetCoreTreeTenantSecurityUserView`,
> `…TenantSecurityContextUserView` (Namespaces unverändert; ihre Refs auf Core/TSV wurden intern auf AdminViews umgebogen).

### Blazor-MudBlazor AdminViews (#20, 7 → 3)

| Alt (entfernt) | Neu | NS |
|---|---|---|
| `…Blazor.MudBlazor.TenantSecurityViews` | `…Blazor.MudBlazor.AdminViews` | **ja** |
| `…Blazor.MudBlazor.AspNetCoreTenantSecurityUserView` | `…Blazor.MudBlazor.AdminViews` | **ja** |
| `…Blazor.MudBlazor.AspNetCoreTreeTenantSecurityUserView` | `…Blazor.MudBlazor.AdminViews` | **ja** |
| `…Blazor.MudBlazor.TenantSecurityContextUserView` | `…Blazor.MudBlazor.AdminViews` | **ja** |
| `…Blazor.MudBlazor.OnboardingViews` | `…Blazor.MudBlazor.AdminViews` | **ja** |

> **Bleiben separat:** `…Blazor.MudBlazor` (Basis) und `…Blazor.MudBlazor.IdentityPages`.
> **Namespaces angeglichen** (nachgezogen in Future_10): Die 5 Bereiche sind jetzt auf das Schema
> `ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.<Bereich>.*` (RootNamespace + Ordnerpfad)
> vereinheitlicht — früher trug jeder Bereich noch seinen Pre-Konsolidierungs-Root
> `ITVComponents.WebCoreToolkit.<Bereich>.Blazor.*`. **`using`-Sweep nötig** → Abschnitt B.

---

## B. `using`-Sweeps (nur Pakete mit „NS = ja")

Reines Präfix-Ersetzen in `.cs`/`.cshtml`/`.razor`. Empfehlung: per Such-/Ersetzen über die MLM-Solution.

### TypeConversion → Plugins
```
ITVComponents.TypeConversion.DefaultConverters   →   ITVComponents.Plugins.TypeConversion.DefaultConverters
```

### Onboarding (EF)
```
ITVComponents.WebCoreToolkit.OnboardingShared.*          →  …EntityFramework.Onboarding.Shared.*
ITVComponents.WebCoreToolkit.CustomerOnboarding.*        →  …EntityFramework.Onboarding.Flat.*
ITVComponents.WebCoreToolkit.TreeCustomerOnboarding.*    →  …EntityFramework.Onboarding.Tree.*
```

### Authentication (provider-prefixed)
```
…WebCoreToolkit.OpenIdAuthentication.*   →  …WebCoreToolkit.Authentication.OpenId.*
…WebCoreToolkit.ApiKeyAuthentication.*   →  …WebCoreToolkit.Authentication.ApiKey.*
…WebCoreToolkit.WindowsAuthentication.*  →  …WebCoreToolkit.Authentication.Windows.*
```
Zusätzlich: das Modell `ApiKey` heißt jetzt **`ApiKeyInfo`** (Namens-Kollision mit dem NS-Segment `ApiKey`).

### Extras (provider-prefixed)
```
…WebCoreToolkit.AnonymousAssetAccess.*          →  …WebCoreToolkit.Extras.AnonymousAssetAccess.*
…WebCoreToolkit.EmailDnsValidation.*            →  …WebCoreToolkit.Extras.EmailDnsValidation.*
…WebCoreToolkit.DbLessConfig.*                  →  …WebCoreToolkit.Extras.DbLessConfig.*
…WebCoreToolkit.ScheduledBackgroundProcessing.* →  …WebCoreToolkit.Extras.ScheduledBackgroundProcessing.*
```

### Tenant-Security EF (#18) — 1:1-Präfix-Tausch, Sub-Namespaces identisch
```
…EntityFramework.TenantSecurityShared    →  …EntityFramework.TenantSecurity.Shared
…EntityFramework.TenantTreeShared        →  …EntityFramework.TenantSecurity.TreeShared
…EntityFramework.AspNetCoreTenants       →  …EntityFramework.TenantSecurity.CoreIdentity
…EntityFramework.AspNetCoreTreeTenants   →  …EntityFramework.TenantSecurity.CoreIdentityTree
…EntityFramework.TenantSecurityContext   →  …EntityFramework.TenantSecurity.Basic
```
> Achtung: auch **bloße** Usings ohne Sub-Segment treffen (`using …TenantSecurityShared;`) — auf Wortgrenze ersetzen, nicht nur `.`-Suffix.

### Telerik-MVC (#19)
```
…Net.TelerikUi                       →  …Net.TelerikUi.AdminViews          (Core: bare + alle Sub-NS .Extensions/.Handlers/.DynamicData/…)
…Net.TelerikUi.TenantSecurityViews   →  …Net.TelerikUi.AdminViews.TenantSecurityViews
…Net.TelerikUi.AspNetCoreIdentityPages →  …Net.TelerikUi.IdentityPages
…Net.TelerikUi.COB                   →  …Net.TelerikUi.Onboarding
```
> **NICHT** ersetzen: die 3 UserView-Namespaces (`…TelerikUi.AspNetCoreTenantSecurityUserView`,
> `…AspNetCoreTreeTenantSecurityUserView`, `…TenantSecurityContextUserView`) — die bleiben.

Razor-/Asset-Spezifika (#19), in den **eigenen** cshtml/Layouts des Hosts:
- `@addTagHelper *, ITVComponents.WebCoreToolkit.Net.TelerikUi` → `…Net.TelerikUi.AdminViews` (Assembly-Name!).
  Ebenso ein etwaiges `@addTagHelper *, …TenantSecurityViews` → `…AdminViews`.
- `_content/ITVComponents.WebCoreToolkit.Net.TelerikUi[.TenantSecurityViews]/…` → `_content/…Net.TelerikUi.AdminViews/…` (Asset-Pfade kollabieren auf die neue Paket-ID, **ohne** Sub-Segment).

### Blazor-MudBlazor (#20) — RootNamespace + Ordnerpfad
```
…WebCoreToolkit.TenantSecurityViews.Blazor                  →  …WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews
…WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor     →  …WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTenantSecurityUserView
…WebCoreToolkit.AspNetCoreTreeTenantSecurityUserView.Blazor →  …WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTreeTenantSecurityUserView
…WebCoreToolkit.TenantSecurityContextUserView.Blazor        →  …WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityContextUserView
…WebCoreToolkit.OnboardingViews.Blazor                      →  …WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews
```
> 1:1-Präfix-Tausch; alle Sub-Segmente (`.Extensions`/`.Handlers[.Impl]`/`.ViewModels`/`.Options`/`.Components.*`) bleiben erhalten. Gilt für `.cs` **und** Razor (`@using`/`@namespace` in `_Imports.razor`). Die 5 alten Roots überlappen nicht, Reihenfolge egal.
> **`global::MudBlazor`-Falle:** Der neue Root enthält das Segment `Blazor.MudBlazor` und beschattet damit den Library-Namespace `MudBlazor`. In jedem bereichseigenen `_Imports.razor` steht deshalb neben `@using global::MudBlazor` (Member-Import für unqualifizierte Nutzung) zusätzlich der Alias `@using MudBlazor = global::MudBlazor` — sonst scheitern qualifizierte (auch vom Razor-Generator emittierte) Referenzen wie `MudBlazor.CellContext<>` / `MudBlazor.InputType`. Eigene Host-Razor-Komponenten, die unter `…Blazor.MudBlazor.*` liegen, brauchen denselben Alias.

---

## C. Konfiguration & DI-Setup

### C1. Authentication (#14) — ApiKey-Config wird benannt
Die ApiKey-WebPart-Config wechselt von einem einzelnen `DetailConfigPath` auf eine benannte Map:
```jsonc
// vorher
"DetailConfigPath": "…ApiKeyConfig…"
// nachher
"DetailConfigPaths": { "ApiKey": "…ApiKeyConfig…" }
```

### C2. Tenant-Security EF (#18) — `ActivationOptions` braucht Strategie-Achsen
Das eine konsolidierte TenantSecurity-Paket bedient alle Strategien; die Auswahl erfolgt jetzt über die
Config statt über die Paket-Wahl. In den `ActivationOptions` (Tenant-Security-Aktivierung) müssen gesetzt
sein:
```jsonc
{
  "Identity": "CoreIdentity",   // oder "BasicTenantSecurity"  (ASP.NET-Identity vs. int-keyed Basic)
  "Strategy": "Flat"            // oder "Tree"                  (flache vs. hierarchische Tenants)
  // … übrige ActivationOptions-Felder unverändert
}
```
Provider: statt der alten strategie-spezifischen Provider-Pakete genügt **ein** `…TenantSecurity.SqlServer`
**bzw.** `…TenantSecurity.PostgreSql`; deren WebPartInit wählt `UseSqlServer`/`UseNpgsql` + den richtigen
SyntaxHelper anhand `(Identity, Strategy)`.

> **EF-Migrationen vor Produktiv-Einsatz im Test-DB validieren** — der Strategie-Dispatch ist Laufzeitlogik;
> der Build verifiziert ihn nicht.

### C3. Telerik-MVC AdminViews (#19) — Core-Config wird benannt (`NetUi`)
Die WebPart-Config des früheren `…Net.TelerikUi`-Core lag im **Default-Slot**; im konsolidierten
`…Net.TelerikUi.AdminViews`-WebPart ist sie auf den benannten Key **`NetUi`** umgezogen (eine WebPartInit
bedient jetzt Core + TenantSecurityViews + die UserView-Strategie). Die TenantSecurityViews-Configs
(`ContextSettings`/`ContextActivationSettings`/`ViewConfig`) bleiben.

### C4. Blazor-MudBlazor AdminViews (#20) — benannte Config-Keys + Activation
Der eine `…Blazor.MudBlazor.AdminViews`-WebPart ersetzt die früheren 5 Blazor-WebParts. Config-Keys:

| Key | Typ | wofür |
|---|---|---|
| `SecurityContext` | `SecurityContextOptions` | ContextType der Admin-/UserViews |
| `Activation` | `ActivationOptions` | `(Identity, Strategy)` → wählt die UserView-Strategie (Flat/Tree/Basic) |
| `Onboarding` | `OnboardingViewOptions` | optional, nur wenn Onboarding-Views gehostet werden |

> **Neu für den Blazor-Part:** Er braucht jetzt eine **`Activation`**-Config (dieselben `Identity`/`Strategy`
> wie C2), um die richtige UserView-Strategie zu registrieren. Ohne sie greift der Default
> (`CoreIdentity`/`Flat`).

### C5. Entferntes Paket
`ITVComponents.Plugins.RuntimeSerialization` ist weg (BinaryFormatter). Falls MLM Plugin-Dateien in diesem
Format hat: auf `System.Text.Json` o.ä. umstellen. (Nach aktuellem Kenntnisstand nutzt MLM es nicht.)

---

## D. Verifikation auf MLM-Seite

1. **Build grün** nach NuGet-Swap (A) + `using`-Sweeps (B).
2. **Tenant-Security (#18):** App startet, Login + Tenant-Admin-Views laden; EF-Migrationen im Test-DB
   geprüft. `(Identity, Strategy)` in `ActivationOptions` korrekt für den verwendeten Context.
3. **Telerik-MVC (#19):** Admin-Views (Security/Util/Connectivity/Help-Areas) rendern; TagHelper/Assets
   laden (TagHelper-Assembly + `_content`-Pfade auf `AdminViews` umgestellt). `NetUi`-Config greift.
4. **Blazor (#20):** `/Security/Users` lädt; je nach Strategie korrekte Spalten/Tabs (Capability-gesteuert:
   Basic ohne Email/Logins/Tokens/Claims, dafür Auth-Type). Admin-Views + Onboarding-Pages rendern.
   Doppelregistrierungs-/Routing-Sanity (eine `Users.razor`-Route, kein `AmbiguousMatch`).

> Laufzeit-Risiko liegt bei #18–#20 in den WebPartInit-Dispatches (build-grün, aber nur per App-Lauf
> verifizierbar). Bei Auffälligkeiten zuerst die Config-Keys (C2–C4) und die `Activation`-Achsen prüfen.
