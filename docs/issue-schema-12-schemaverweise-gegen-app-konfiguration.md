# Schema-Support 12: Schema-Verweise im Plugin-Teil werden gegen `NativeSettings` statt gegen die App-Konfiguration aufgelöst

**Typ:** Bug
**Betrifft:**
- `ITVComponents.EFRepo\DIIntegration\Impl\CustomSchemaOptionsLoader.cs`
- `ITVComponents.EFRepo\Helpers\SchemaConfiguration.cs` (`Resolve(value, configuration = null)`)
- `ITVComponents.EFRepo.SqlServer\Migrations\SchemaAwareMigrationsExtensions.cs` (`UseSchemaAwareMigrations`)

## Problem

Ohne übergebene `IConfiguration` löst `SchemaConfiguration.Resolve` Verweise gegen
`NativeSettings.Configuration` auf. Diese liest ausschliesslich `<EntryAssembly>.appsettings.json`
(z.B. `ESAG_PartnerPortal.appsettings.json`) – **nicht** die ASP.NET-Konfiguration
(`appsettings.json`, `appsettings.{Env}.json`, Umgebungsvariablen, Custom-Config-Files).

- **WebPart-Seite** (`TenantSecurityShared.WebPartInit`) übergibt die App-Konfiguration → korrekt.
- **Plugin-Seite** (`CustomSchemaOptionsLoader`) übergibt nichts → `->Database:Schema` wird in einer Web-App
  typischerweise **nicht gefunden** → Warnung + Default-Schema. Security- und Plugin-Kontext laufen dann in
  verschiedenen Schemas.
- **Design-Time** (`UseSchemaAwareMigrations` in einer `IDesignTimeDbContextFactory`): Entry-Assembly ist
  `ef.dll`/der Host – auch hier wird ein Verweis in der Regel nicht aufgelöst. Konsumenten sollten dort den
  aufgelösten Wert übergeben; die Doku sollte das sagen.

## Vorschlag
- `CustomSchemaOptionsLoader`: Verweise gegen die App-Konfiguration auflösen – z.B. `IConfiguration` per DI
  beziehen (falls die Plugin-Factory DI-Parameter unterstützt) oder lazy in `Configure` über einen
  `IServiceProvider` auflösen.
- Alternativ/zusätzlich: WebCoreToolkit hängt beim Start die App-Konfiguration an `NativeSettings.Builder`
  (`AddConfiguration(builder.Configuration)`), bevor `NativeSettings.Configuration` zum ersten Mal gelesen wird –
  dann lösen alle `NativeSettings`-basierten Verweise einheitlich auf.
- XML-Doku von `UseSchemaAwareMigrations` / `SchemaConfiguration.Resolve`: Hinweis, gegen welche Konfiguration
  aufgelöst wird.

## Akzeptanzkriterien
- In einer ASP.NET-App mit `Database:Schema` in `appsettings.json` bzw. per Umgebungsvariable liefert
  `CustomSchemaOptionsLoader("->Database:Schema")` denselben Wert wie die ActivationSettings.
