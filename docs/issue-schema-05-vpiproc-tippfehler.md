# Schema-Support 05: Aufruf von `GetChildTenantsWithPermsByVpIProc` – Prozedur heisst `…ByVpIdProc`

**Typ:** Bug (unabhängig vom Schema, schlägt in jeder DB fehl)
**Betrifft:** `ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.SqlServer\SyntaxHelper\SqlColumnsSyntaxHelper.cs:175, 185`

## Problem

```csharp
$"exec GetChildTenantsWithPermsByVpIProc @UserId=…,@ViewPointTenantId=…"
```

`ConfigureViews` legt die Prozedur aber als `GetChildTenantsWithPermsByVpIdProc` an (Zeile 594, Drop in
Zeile 209). Die beiden `ConfigureMethod`-Überladungen mit `int? currentTenant` werfen daher zur Laufzeit
`Could not find stored procedure 'GetChildTenantsWithPermsByVpIProc'`.

## Vorschlag
Aufruf auf `GetChildTenantsWithPermsByVpIdProc` korrigieren (zusammen mit der Schema-Qualifizierung aus
Issue 04). Idealerweise den Namen als Konstante in `GlobalDbObjectNaming` ablegen und an beiden Stellen
(Create/Drop und Aufruf) verwenden.

## Akzeptanzkriterien
- Aufruf der Überladungen mit Viewpoint-Tenant-ID funktioniert.
