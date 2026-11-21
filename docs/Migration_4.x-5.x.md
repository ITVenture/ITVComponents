# Umstieg ITVComponents 4.x → 5.x

Sammelt, was ein Konsument beim Umstieg von 4.x auf 5.x (`Future_10`, .NET 10) **ausserhalb** des eigenen Codes
anpassen muss: Konfiguration, Datenbank-Inhalte, Migrationen, Skripte in der Datenbank. Die Punkte stammen
überwiegend aus dem Port des ESAG-PartnerPortals (4.0.0-PRE314 → 5.0.0-PRE278).

Was im Code eines Konsumenten wegen umbenannter Namespaces/Pakete angepasst werden muss, steht im
Packaging-Leitfaden. Dieses Dokument ergänzt ihn um das, was in der Datenbank und in der Konfiguration steckt
und deshalb kein Compiler findet.

---

## 1. Gespeicherte Typnamen in der Datenbank

Mit der Paket-Konsolidierung haben sich Assemblies, Namespaces und teilweise die Generic-Stelligkeit
geändert (z.B. `DbSecurityRepository`43/46` → `` `45 ``, `DbPluginsSelector`8` → `` `11 ``). Assembly-qualifizierte
Typnamen aus 4.x in der Datenbank lassen sich danach nicht mehr auflösen.

Betroffen sind mindestens:

| Tabelle | Spalte(n) |
|---|---|
| `TrustedFullAccessComponents` | `FullQualifiedTypeName`, `TargetQualifiedTypeName` |
| `WebPlugins` | `Constructor`, `StartupRegistrationConstructor` |
| `GenericPluginParams` | `TypeExpression` |
| `WebPluginConstants` | `Value` |
| `Localizations` | `Identifier` |

**Typisches Symptom:** Mit falschen `TrustedFullAccessComponents` wird ein Baustein nicht mehr als vertrauenswürdig
erkannt, und der Mandant bleibt unaufgelöst. Seit 5.0 meldet das eine Exception mit „requires a current tenant,
but …“ statt nur „Nullable object must have a value“.

**Prüf-Skript (SQL Server)**: listet jede Zeile, die einen 4.x-Versionsstempel oder überhaupt einen
assembly-qualifizierten Namen trägt. Die zweite Gruppe ist nicht automatisch falsch, muss aber gegen die
neuen Namen geprüft werden. Das Schema bei Bedarf anpassen.

```sql
DECLARE @schema sysname = N'dbo';
DECLARE @sql nvarchar(max) = N'';
;WITH targets(tbl, col) AS (
    SELECT N'TrustedFullAccessComponents', N'FullQualifiedTypeName' UNION ALL
    SELECT N'TrustedFullAccessComponents', N'TargetQualifiedTypeName' UNION ALL
    SELECT N'WebPlugins', N'Constructor' UNION ALL
    SELECT N'WebPlugins', N'StartupRegistrationConstructor' UNION ALL
    SELECT N'GenericPluginParams', N'TypeExpression' UNION ALL
    SELECT N'WebPluginConstants', N'Value' UNION ALL
    SELECT N'Localizations', N'Identifier')
SELECT @sql = @sql + CASE WHEN @sql = N'' THEN N'' ELSE N' UNION ALL ' END
    + N'SELECT N''' + tbl + N''' AS TableName, N''' + col + N''' AS ColumnName, '
    + N'CASE WHEN ' + QUOTENAME(col) + N' LIKE N''%Version=4.%'' THEN N''4.x version stamp'' ELSE N''assembly-qualified, check'' END AS Finding, '
    + QUOTENAME(col) + N' AS Content FROM ' + QUOTENAME(@schema) + N'.' + QUOTENAME(tbl)
    + N' WHERE ' + QUOTENAME(col) + N' LIKE N''%Version=4.%'' OR ' + QUOTENAME(col) + N' LIKE N''%, Version=%'''
FROM targets
WHERE COL_LENGTH(QUOTENAME(@schema) + N'.' + QUOTENAME(tbl), col) IS NOT NULL;
EXEC sp_executesql @sql;
```

**PostgreSQL**: dieselbe Abfrage je Tabelle, z.B.

```sql
SELECT 'WebPlugins' AS "TableName", "Constructor" AS "Content" FROM "WebPlugins"
WHERE "Constructor" LIKE '%Version=4.%' OR "Constructor" LIKE '%, Version=%';
```

Der Packaging-Leitfaden nennt nur Code und WebPart-Konfiguration. Die Datenbank-Inhalte gehören zur
Umstellung dazu.

## 2. `DbPluginFormatter` nimmt eine `IToolkitContextFactory`

Der Formatter bekommt statt des Security-Kontexts eine `IToolkitContextFactory`. Ein Plugin-Konstruktor, der ihn
von Hand aufbaut, braucht dafür eine eigene Abhängigkeit, z.B. `$sysFactory`.

## 3. `CallingPlugin` in Pre-/PostInit-Sequenzen (seit `8baa9b00d`)

Mit „CallingPlugin: volle Aufrufkette“ ist für Einträge einer Pre-/PostInit-Sequenz der **Sequenz-Besitzer** das
`CallingPlugin`, nicht mehr der Anforderer. Konfigurationen, die in solchen Ketten `CallingPlugin.PluginType` oder
`[CallingPlugin.UniqueName]` verwenden, bekommen ein falsches Typ-Argument bzw. eine `~>`-Referenz „not found“.

Umstellung (ESAG):
- Typ-Argumente: `CallingPlugin["TContext"]` statt `CallingPlugin.PluginType`.
- `~>`-Referenzen in Konstruktoren: `[CallingPlugin.PrevPlugin(1).UniqueName]` statt `[CallingPlugin.UniqueName]`.

## 4. Migrationen: `ConfigureViews`

**Nur die jeweils neueste Migration ruft `ConfigureViews` auf.** Die Methode erzeugt immer das SQL der
**referenzierten** Library-Version, nicht das zum Zeitpunkt der Migration. Ruft eine ältere Migration sie auf
(z.B. eine Baseline), scheitert ein frischer Aufbau, sobald die Objekte Spalten verwenden, die erst spätere
Migrationen anlegen (beim ESAG-Port: `Tenants.Culture`).

Beim Update auf eine Version mit geänderten Views/Prozeduren:
1. In der neuen Migration `ConfigureViews(migrationBuilder)` aufrufen.
2. Den Aufruf aus der vorherigen Migration entfernen. Auf bestehenden Datenbanken ändert das nichts, die
   Migration ist dort schon gelaufen.

**Ausrollen per Skript (`dotnet ef migrations script`).** Ein normales Skript ist in Ordnung: Jede SQL-Anweisung
aus `ConfigureViews` wird ein eigener, mit `GO` abgetrennter Batch. Im **idempotenten** Skript (`--idempotent`)
steht jeder Befehl in `IF NOT EXISTS (...) BEGIN ... END`, und dort sind `CREATE VIEW/FUNCTION/PROCEDURE` ungültig.
Seit 5.x verpackt der `SchemaAwareMigrationsSqlGenerator` (SQL Server) solche Definitionen im idempotenten Skript
in `EXEC(N'...')`. Voraussetzung ist `UseSchemaAwareMigrations(...)` in der Design-Time-Factory, die
`ConfigureViews` ohnehin braucht.

## 5. Diagnostics-Queries

Die Skripte der Diagnostics-Queries laufen in 5.x ohne `HttpContext`. Im Skript stehen nur:

| 4.x | 5.x |
|---|---|
| `context.RequestServices` | `Services` (der Scope des Aufrufers), oder `Inject<T>()` |
| `context.User` | `User` |
| `context.Features.Get<IRequestCultureFeature>()` | `System.Globalization.CultureInfo.CurrentCulture` / `CurrentUICulture` (von der `ThreadCultureMiddleware` gesetzt) |
| der Kontext | `db` (vom Typ des Kontexts, den die Datenquelle liefert) |

Ein Verweis auf `context` endet in `CS0103: Der Name "context" ist im aktuellen Kontext nicht vorhanden`.

**Referenzen und Usings.** Das Skript kennt nur ein paar Toolkit-Assemblies und die Assembly bzw. den Namespace des
Kontexts selbst. Alles andere bindet der Query-Text am Anfang ein:

```
#@#{R:Egro360.FileHandlers;R:ITVComponents.WebCoreToolkit;U:Egro360.FileHandlers.Helpers;U:ITVComponents.WebCoreToolkit.Routing}#@#
```

Liegen die Sets eines Kontexts in einer Basisklasse einer anderen Assembly (z.B. der Security-Kontext auf
`AspNetTreeSecurityContext<>`), muss auch diese Assembly referenziert werden.

**Datenquellen.** Eine in `ConfigureDiagnosticsQueries` registrierte Factory muss einen `DbContext` oder einen
`DynamicDataAdapter` liefern. Eine `IToolkitContextFactory` direkt zurückzugeben endet in „Unexpected returned
value“. Der Schlüssel (das Feld `DbContext` der Abfrage) unterscheidet Gross-/Kleinschreibung. Ein Tippfehler
fällt auf `UsePlugins()` zurück und endet in derselben Meldung.

**Dictionary-Ergebnisse** liefert `/Diagnostics/{query}` seit dem Fix zu Issue Diagnostics 01 wieder als
JSON-Objekt wie in 4.x. Die Werte müssen materialisiert sein, die Datenquelle ist beim Zurückgeben schon
freigegeben.

## 6. Plugin-Definitionen werden gepuffert

Die Plugin-Fabrik puffert Plugin-Zeilen, Parameter, Konstanten und Settings je Mandant. Ohne Change-Signal gilt
nur die Ablaufzeit von **30 Minuten**: Eine geänderte Plugin-Zeile greift dann erst so spät.

Abhilfe: in den `ActivationSettings` des Security-WebParts

```json
"UseEntityTracker": true
```

Danach macht jede Änderung über EF an `WebPlugin`, `WebPluginGenericParam`, `WebPluginConstant`, `GlobalSetting`,
`TenantSetting` oder `Tenant` den Puffer sofort ungültig (bei mehreren Instanzen im Takt von `RefreshSeconds`).
Direkte SQL-Updates bemerkt der Tracker nicht.

## 7. Behoben, ohne Aufwand beim Konsumenten

- **`ChildTenantsWith` und `TenantNameLower`** (Issue ESAG ChildTenantsWith): Die Prozeduren bestimmen nur noch,
  welche Mandanten gelten. Die Zeilen kommen aus dem Modell. Es braucht **keine** Migration, und eine
  Umgehung per `ALTER PROCEDURE` ist nicht mehr nötig.
- **Externe Dienste: Token-Zeilen aus 4.x** (Issue External Services 01): In 4.x waren die Zeilen in
  `ExternalOAuthServiceTenantLogins` reine Token-Puffer. Nach dem Umzug der Zugangsdaten in die Plattform- bzw.
  Besitzer-Zeile verdeckten sie diese, und der Token-Abruf lief mit leerem Benutzer und Passwort („invalid_grant“).
  Der Start-Umzug entfernt solche Zeilen jetzt (ohne Zugangsdaten, nicht Authorization-Code, von einer Plattform- oder
  vererbbaren Besitzer-Zeile mit Zugangsdaten abgedeckt), auch auf Datenbanken, die schon mit einer früheren 5.x-Fassung
  umgezogen sind. Zusätzlich überspringt die Auflösung eine Zeile ohne Zugangsdaten und protokolliert das als Warnung.
  Das manuelle Löschen der Zeile ist nicht mehr nötig.
