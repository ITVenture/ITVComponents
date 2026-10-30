# Schema-Support 09: Schema-Konfiguration (Laufzeit)

**Typ:** Enhancement – **grösstenteils umgesetzt**, Restpunkte unten
**Betrifft:**
- `ITVComponents.EFRepo\DbContextConfig\Impl\SchemaRedirect.cs` (neu)
- `ITVComponents.EFRepo\DIIntegration\Impl\CustomSchemaOptionsLoader.cs` (neu)
- `...TenantSecurityShared\Options\ActivationOptions.cs` (`CustomSchema`)
- `...TenantSecurityShared\WebPartInit.cs`, `...TenantSecurityShared\Extensions\DependencyExtensions.cs`
  (`ConfigureCustomTenantSecuritySchema`)

## Umgesetzt
- `SchemaRedirect` (`IEntityConfigurator`) setzt `HasDefaultSchema`.
- Security-Kontexte: `ActivationOptions.CustomSchema` → `ConfigureCustomTenantSecuritySchema` hängt den
  `SchemaRedirect` an `DbContextModelBuilderOptions<TContext>`. `ActivationSettings` werden per `RefResolve`
  aufgelöst, d.h. `"CustomSchema": "->Database:Schema"` ist möglich.
- Plugin-Kontexte: `CustomSchemaOptionsLoader<TContext>` (mit Parent-Konstruktor, also in die bestehende
  `DbModelBuilderOptionsProvider`-Kette einhängbar); Wert direkt oder als Verweis `->Pfad`.
- Design-Time-Factories hängen keinen `SchemaRedirect` an → Migrationen werden ohne Schema generiert
  (gewollt, siehe Issue 01).

## Offen
- **Logging in `CustomSchemaOptionsLoader.ApplySchema`:** Liefert ein Verweis `->Pfad` keinen Wert, bleibt
  `targetSchema` still `null` und der Kontext läuft unbemerkt im Default-Schema (`dbo`). Hier muss eine
  Warnung geloggt werden (Pfad nennen). Gilt sinngemäss für ein per `RefResolve` leer aufgelöstes
  `ActivationOptions.CustomSchema`.
- **Migrations-History und Anwenden der Migrationen** → Issue 10 (`SchemaAwareMigrationsSqlGenerator`).
- **Mitgelieferte Library-Migrationen** (`...AspNetCoreTreeTenants.SqlServer\Migrations`,
  `...AspNetCoreTenants.SqlServer\Migrations`, `...TenantSecurityContext.SqlServer\Migrations`) sind schemalos
  und damit kompatibel; `SetViewCode` muss aber auf `ConfigureViews(mb)` mit Platzhalter umgestellt werden
  (Issue 03).
- **Doku:** Ein Konsument setzt das Schema ausschliesslich über Konfiguration; `migrations add` muss nicht
  besonders behandelt werden, weil die Design-Time-Factory kein `SchemaRedirect` verwendet.

## Akzeptanzkriterien
- Fehlender Verweiswert erzeugt eine Log-Warnung.
- Security- und Plugin-Kontext laufen mit demselben konfigurierten Schema (ein Config-Key, zwei Verweise).
