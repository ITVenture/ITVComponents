# Schema-Support 01: Ziel-Schema zentral bereitstellen (Laufzeit + Migrations-Platzhalter)

**Typ:** Enhancement (Grundlage für die Issues 02–04, 06, 10)
**Betrifft:** `ITVComponents.EFRepo`, `ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.SqlServer`

## Ausgangslage / Konzept

Das Schema wird **pro Installation konfiguriert** und nicht in Code oder Migrationen fest verdrahtet:

- **Laufzeit:** Das Default-Schema des Modells wird über `SchemaRedirect` gesetzt – für Security-Kontexte via
  `ActivationOptions.CustomSchema` (WebPart), für Plugin-Kontexte via `CustomSchemaOptionsLoader` (Plugin-Kette).
  Beide unterstützen Verweise (`->Pfad`), damit der Wert nur an einer Stelle steht.
- **Migrationen werden immer ohne Schema generiert** (kein `schema:`-Literal, kein `HasDefaultSchema` im
  Snapshot/Designer). Dieselbe Migration gilt so für `dbo` und für jedes andere Schema.
- **Beim Anwenden** setzt der `SchemaAwareMigrationsSqlGenerator` (Issue 10) das konfigurierte Schema ein.

Daraus folgt: `Migration.TargetModel` enthält **kein** Schema und kann **nicht** als Quelle dienen.

## Vorschlag

### Laufzeit: Schema aus dem Kontext-Modell

```csharp
public static string GetDbSchema(this DbContext context)
    => context.Model.GetDefaultSchema() ?? "dbo";

public static string QualifiedObjectName(this DbContext context, string objectName)
    => $"[{context.GetDbSchema()}].[{objectName}]";
```

Verwendung in allen Raw-SQL-Statements zur Laufzeit (Issues 02, 04). Bezeichner sind in
`FromSqlInterpolated`/`ExecuteSqlInterpolated` nicht parametrisierbar – Objektnamen daher als Literal einsetzen,
Werte weiterhin als Parameter übergeben.

### Migrationen: Schema-Platzhalter

Zentrale Konstante, die der `SchemaAwareMigrationsSqlGenerator` (Issue 10) in `SqlOperation`s ersetzt:

```csharp
public static class MigrationSchema
{
    public const string Placeholder = "__SCHEMA__";
}
```

`SqlColumnsSyntaxHelper.ConfigureViews`:
- Der stille Default `schema = "dbo"` entfällt.
- Neue Überladung ohne Schema-Parameter, die mit `MigrationSchema.Placeholder` arbeitet:
  `ConfigureViews(MigrationBuilder mb) => ConfigureViews(mb, MigrationSchema.Placeholder);`
- Die Überladung mit explizitem Schema bleibt für Sonderfälle bestehen.

Ohne Generator bzw. ohne konfiguriertes Schema ersetzt der Generator den Platzhalter durch `dbo` –
das Verhalten entspricht dann dem bisherigen.

Nicht geeignet als Schema-Quelle in Migrationen:
- `Migration.TargetModel` – enthält bewusst kein Schema (s.o.).
- Abfrage über die Verbindung (`SELECT SCHEMA_NAME()`) – hängt vom Login ab (sysadmin → immer `dbo`) und
  funktioniert bei Skript-Generierung nicht.

## Akzeptanzkriterien
- `GetDbSchema`/`QualifiedObjectName` vorhanden und in Issues 02/04 verwendet.
- `MigrationSchema.Placeholder` vorhanden; `ConfigureViews(mb)` erzeugt ausschliesslich Platzhalter-Referenzen.
- Ohne konfiguriertes Schema ist das Verhalten unverändert (`dbo`).
