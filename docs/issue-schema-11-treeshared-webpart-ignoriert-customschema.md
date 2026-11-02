# Schema-Support 11: `TenantTreeShared`-WebPart wertet `ActivationOptions.CustomSchema` nicht aus

**Typ:** Bug (Schema-Konfiguration wirkt bei Tree-Tenant-Anwendungen nicht)
**Betrifft:** `ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared\WebPartInit.cs`

## Problem

Die Auswertung von `CustomSchema` (Verweis-Auflösung in `LoadOptions`, `ConfigureCustomTenantSecuritySchema`
in `RegisterServices`) wurde nur in `...TenantSecurityShared\WebPartInit.cs` eingebaut.

Anwendungen mit Tree-Tenants (z.B. ESAG-PartnerPortal) laden aber das WebPart aus
`ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.dll` – `TenantSecurityShared.dll` ist dort gar
nicht als WebPart registriert. `TenantTreeShared.WebPartInit` verwendet zwar dieselbe `ActivationOptions`-Klasse
(Property `CustomSchema` existiert), aber:
- `LoadOptions` ruft weder `RefResolve` noch `SchemaConfiguration.Resolve` auf,
- `RegisterServices` ruft `ConfigureCustomTenantSecuritySchema` nicht auf.

Folge: `"CustomSchema": "->Database:Schema"` in den ActivationSettings wird stillschweigend ignoriert; der
Security-Kontext läuft in `dbo`, während ein Plugin-Kontext mit `CustomSchemaOptionsLoader` im konfigurierten
Schema läuft.

Das zusätzliche Laden von `TenantSecurityShared.dll` als WebPart ist **kein** Workaround – damit würden
globale Filter, TemplateFactory, Localizer usw. doppelt registriert.

## Vorschlag
- Die Schema-Logik aus `TenantSecurityShared.WebPartInit` in eine gemeinsame Hilfsmethode auslagern
  (z.B. `ActivationOptions ResolveActivationOptions(IConfiguration, string path)` und
  `services.ApplyCustomSchema(t, partOptions)`), und in beiden WebPartInits verwenden.
- Prüfen, ob weitere WebParts mit eigenem `LoadOptions` für `ActivationOptions` existieren
  (z.B. `CustomerOnboarding`) – dort mindestens die Verweis-Auflösung angleichen.

## Akzeptanzkriterien
- Mit `"CustomSchema": "->Database:Schema"` in den ActivationSettings des `TenantTreeShared`-WebParts hat
  `Egro360SecurityDbContext.Model.GetDefaultSchema()` den konfigurierten Wert.
