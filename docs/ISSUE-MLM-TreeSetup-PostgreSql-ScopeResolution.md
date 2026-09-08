# Bug: PostgreSQL-Baumfunktionen vergleichen den Benutzernamen gross-/kleinschreibungsgenau — Mandanten-Auflösung schlägt fehl (Anstoß aus MLM)

**Status:** UMGESETZT (2026-09-08) — Ursache behoben, die drei Folge-Punkte a/b/c mit, dazu die
fehlende Deployment-Lücke (§1). Bewusst NICHT umgesetzt: das Permission-Seeding (§2) — siehe
„Umsetzung“ am Ende. Enthalten ab der auf `5.0.0-PRE215` folgenden Version.
**Datum:** 2026-09-08
**Quelle:** MLMManager-Session (Konsument). MLM fährt **CoreIdentity + Tree** und ist seit 2026-09-08
zwischen SQL Server und PostgreSQL umschaltbar. Der Fehler tritt **ausschliesslich auf PostgreSQL** auf.
**Toolkit-Stand:** `5.0.0-PRE215` (alle Fundstellen daran verifiziert).

## Kurzfassung

`GetUpwardsRoleTreeForLabels` vergleicht das übergebene Benutzer-Kennzeichen mit
`Users."NormalizedUserName"` — also mit der **grossgeschriebenen** Fassung. Übergeben wird aber
`IIdentity.Name`, und das ist der Benutzername in seiner **gespeicherten** Schreibweise. Unter SQL
Server geht das dank case-insensitiver Sortierfolge trotzdem auf; PostgreSQL vergleicht exakt, die
Funktion liefert **0 Zeilen**, und in der Folge ist die Liste der zulässigen Mandanten leer. Der
Benutzer kann in **keinen** Mandanten wechseln.

## Der Beleg

Dieselbe Funktion, dieselbe Datenbank, nur die Schreibweise des Kennzeichens unterscheidet sich:

```sql
SELECT 'klein (Identity.Name)'      AS variante, count(*)
FROM   "GetUpwardsRoleTreeForLabels"('["mw@it-venture.ch"]', null)
UNION ALL
SELECT 'gross (NormalizedUserName)', count(*)
FROM   "GetUpwardsRoleTreeForLabels"('["MW@IT-VENTURE.CH"]', null);

--          variante          | count
-- ---------------------------+-------
--  klein (Identity.Name)     |     0     ← das übergibt die Anwendung
--  gross (NormalizedUserName)|     1
```

Und derselbe Unterschied im Nachbau von `GetEligibleScopes` (Users ⋈ TenantUsers ⋈ Funktion ⋈ Tenants):

| übergebenes Kennzeichen | Ergebnis |
|---|---|
| `mw@it-venture.ch` (das echte) | **0 Zeilen** |
| `MW@IT-VENTURE.CH` | 1 Zeile: `ADM` / `Admin` / DirectlyAssigned |

Die Datenlage ist dabei in Ordnung: `GetEffectiveTenantUserRoles(1)` liefert `RoleId 1`,
`UpwardsTenantTree` liefert `ADM → ADM`, Mandant, TenantUser und Rolle sind vorhanden.

## Die Kette

1. `SimpleUserNameMapper.GetUserLabels(IIdentity user)` (`UserMappers/SimpleUserNameMapper.cs:39-53`)
   liefert `user.Name`. Bei ASP.NET-Core-Identity ist das der `UserName` **wie gespeichert** —
   in aller Regel klein.
2. `ResolvingPermissionScope.GetUserLabels()` (`:381-389`) reicht das unverändert weiter.
3. `DbSecurityRepository.GetEligibleScopes` (`TreeShared/Security/DbSecurityRepository.cs:907`)
   verbindet `Users` (über `UserFilter`) mit `TenantUsers` und mit
   `GetUpwardsTenantUserRoles(userLabels, null)`.
4. **Der C#-Teil normalisiert, der SQL-Teil nicht.**
   `AspNetDbTreeSecurityRepository.UserFilter` (`CoreIdentityTree/Security/…:45-50`) macht es richtig:
   ```csharp
   var lbl = (from t in userLabels select t.ToLower()).ToArray();
   return n => lbl.Contains(n.UserName.ToLower()) && …;
   ```
   Die Datenbankfunktion dagegen vergleicht roh
   (`PostgreSql/CoreIdentityTree/SyntaxHelper/PostgreSqlColumnsSyntaxHelper.cs:658`):
   ```sql
   INNER JOIN json_array_elements_text(p_user_id::json) uta ON u."NormalizedUserName" = uta.value
   ```
5. Ergebnis: 0 Zeilen → der Join in Schritt 3 ist leer → `EligibleScopes` ist ein **leeres Array**.

Das SQL-Server-Gegenstück ist wörtlich dieselbe Logik
(`SqlServer/…/SqlColumnsSyntaxHelper.cs:563` und `:722`):

```sql
inner join openjson(@UserId) with ([value] nvarchar(150) '$') uta on u.NormalizedUserName = uta.value
```

Dort trägt die case-insensitive Standard-Sortierfolge den Vergleich. Bei der Übersetzung nach
PostgreSQL ist diese stillschweigende Voraussetzung nicht mitgewandert — PostgreSQL vergleicht immer
exakt.

**Vorschlag:** in der Funktion normalisieren statt sich auf die Sortierfolge zu verlassen, z.B.
`ON u."NormalizedUserName" = upper(uta.value)`. Betroffen ist der Generator an `:658`, also **beide**
Kennzeichen-Varianten (`GetUpwardsRoleTreeForLabels` und `GetUpwardsRoleTreeForLabelsByLeafId`). Die
Id-Varianten sind nicht betroffen. Lohnend wäre ausserdem ein Durchgang durch den PostgreSQL-Helfer
nach weiteren Wert-Vergleichen, die auf SQL-Server-Sortierfolge bauen — beim Mandantennamen ist es
schon richtig gelöst (`lower(t0."TenantName") = …`), hier eben nicht.

---

## Was der Fehler daraus macht: eine unbrauchbare Meldung

Aus dem leeren `EligibleScopes` wird keine saubere Absage, sondern eine nichtssagende Ausnahme. 48-mal
im `SystemLog`, immer derselbe Pfad:

```
System.InvalidOperationException: Sequence contains no matching element
   at UserScope.UpdateScopePermissions(String currentScope, Permission[] knownPermissions, String[] permissions)
   at ResolvingPermissionScope.UpdateToken(…) → GetCurrentScope() → GetPermissionScopePrefix()
   at PermissionScopeBase.get_IsScopeExplicit() → HttpAppLink.get_Tenant()
   at CircuitAppLink.get_CurrentModuleUrl() → Navigator.EnsureBuilt()
   at Blazor.MudBlazor.AdminViews.HelpViews.Components.Viewer.HelpButton.OnInitializedAsync()
```

Für den Konsumenten sieht das nach einem Fehler im `HelpButton` aus. Drei Stellen tragen dazu bei —
jede davon wäre auch für sich eine Verbesserung wert:

### a) `First` ohne Absicherung

`UserScope.UpdateScopePermissions` (`CookieModels/UserScope.cs:65`) und `UpdateScopeFeatures` (`:81`)
beginnen mit `EligibleScopes.First(n => n.ScopeName.Equals(currentScope, OrdinalIgnoreCase))`. Passt
nichts, fliegt *„Sequence contains no matching element"* — ohne den Scope, ohne die Liste.

`ResolvingPermissionScope` kennt die Gefahr und kommentiert sie sogar (`:226-233`), sichert aber nur
den Fall **leerer** Rückgabewert ab. Der Fall **nicht-leer, aber nicht zulässig** bleibt offen — und
den erzeugt `DefaultScopeExpression` (`:222`), ein **Host-Delegat**, dessen Ergebnis niemand gegen
`EligibleScopes` prüft. MLMs Ausprägung ist ein naheliegendes Muster und läuft genau hinein: der
Anspruch kommt aus einem Claim (`DefaultTenant` = „ADM"), die Zulässigkeit aus der Datenbank (leer).

Was `:208` für den Routen-Override bereits tut, fehlt an `:222`: den Wert gegen `EligibleScopes`
prüfen und andernfalls entweder auf `EligibleScopes.FirstOrDefault()` klemmen oder mit einer Meldung
werfen, die den angeforderten Scope **und** die zulässigen nennt.

### b) Leer ist nicht null — ein einmal leerer Satz bleibt stehen

`ReadScopeToken` (`ResolvingPermissionScope.cs:316-331`):

```csharp
if (!createdNew && scopeToken.UserLabels != null && UserValidateHelper.IsUserOk(…))
{
    eligibles = scopeToken.EligibleScopes;   // kann ein LEERES Array sein
}
if (eligibles == null)                       // greift bei leer NICHT
{
    eligibles = GetEligibleScopes(out secc, lbl);
}
```

Ein leeres, aber nicht-null `EligibleScopes` wird nicht neu ermittelt und bleibt bis zum Ablauf des
Tokens stehen (in MLM `RenewalMinutes = 60`). Das erklärt die 48 Wiederholungen und warum sich der
Zustand nicht von selbst erholt. `eligibles is not { Length: > 0 }` statt `== null` wäre der Einzeiler.

### c) Gross-/Kleinschreibung auch im C# uneinheitlich

| Stelle | Vergleich |
|---|---|
| `ResolvingPermissionScope.cs:208` (Routen-Override) | `OrdinalIgnoreCase` |
| `ResolvingPermissionScope.cs:218` (`invalidTenant`) | `n.ScopeName != retVal` — **case-sensitive** |
| `UserScope.cs:42/65/81/90/98` | `OrdinalIgnoreCase` |

Dieselbe Frage im selben Ablauf dreimal unterschiedlich beantwortet. Auf PostgreSQL, wo Namen ihre
Schreibweise zuverlässig behalten, wiegt das schwerer als auf SQL Server.

---

## Zwei Lücken im Tree/PostgreSQL-Setup, die beim Suchen aufgefallen sind

Beide sind nicht die Ursache des obigen Fehlers, aber sie stehen im selben Weg.

### 1. Die PostgreSQL-`CoreIdentityTree`-Migrationen legen die Datenbank-Objekte nicht an

| Ausprägung | SQL Server | PostgreSQL |
|---|---|---|
| `Basic` | `IncludeToolkitPermissions()` in `…_ExtendedMultiTenantSupportFeatures` | `IncludeToolkitPermissions()` in `…_InitialTenantSecurity` |
| `CoreIdentity` | `IncludeToolkitPermissions()` in `…_InitialContext` | `IncludeToolkitPermissions()` in `…_InitialTenantSecurity` |
| `CoreIdentityTree` | `ConfigureViews` in `20241125163308_SetViewCode`, dazu `20260706120000_RedeployAnchoredUpwardsRoleTree` | **nur `20260819222431_InitialTreeTenantBuild` — ruft weder `ConfigureViews` noch sonst etwas** |

Im PostgreSQL-Tree-Paket gibt es also keine Migration, die die 17 Baum-Objekte deployt, obwohl
`PostgreSqlColumnsSyntaxHelper.ConfigureViews(migrationBuilder, schema = "public")` vollständig
vorhanden ist. Wer die mitgelieferten Tree-Migrationen anwendet, bekommt eine Datenbank ohne Sichten
und Funktionen.

### 2. `IncludeToolkitPermissions()` fehlt in **beiden** Tree-Ausprägungen

`Basic` und `CoreIdentity` säen den Berechtigungs-Katalog auf beiden Anbietern, `CoreIdentityTree` auf
keinem. Eine frische Tree-Datenbank hat damit einen leeren `Permissions`-Katalog. Entweder mitsäen wie
flach, oder im Migrations-Leitfaden als Konsumentenpflicht benennen — zusammen mit `ConfigureViews`,
denn beides gehört zum selben Schritt „Tree-Datenbank aufsetzen".

## Was ausdrücklich NICHT das Toolkit ist

MLMs eigene PostgreSQL-Initialmigration ruft `ConfigureViews()`, aber nicht
`IncludeToolkitPermissions()` — im SQL-Server-Gegenstück stehen beide nebeneinander. Das ist ein
Fehler auf Konsumentenseite und wird dort behoben. Er steht hier nur, weil er zeigt, wie leicht
Lücke 2 zuschlägt: zwei Aufrufe, die zusammengehören, ohne dass irgendwo steht, dass sie
zusammengehören.

## Umgehung auf Konsumentenseite (nur benannt, nicht umgesetzt)

Ein eigener `IUserNameMapper`, der statt `user.Name` dessen grossgeschriebene Fassung liefert, würde
die Funktion treffen — und `UserFilter` bliebe unbeeindruckt, weil es ohnehin beide Seiten
kleinschreibt. Das ist aber eine Umgehung im Konsumenten für einen Fehler im Toolkit und wird erst
gebaut, wenn die Toolkit-Lösung sich verzögert.

## Abnahme

1. `GetUpwardsRoleTreeForLabels('["mw@it-venture.ch"]', null)` liefert auf PostgreSQL dieselbe Zeile
   wie mit `'["MW@IT-VENTURE.CH"]'` — und der angemeldete Benutzer kann in seinen Mandanten wechseln.
2. Ein `DefaultScopeExpression`, das einen nicht zulässigen Scope liefert, führt zu einer Meldung, die
   Scope und zulässige Scopes nennt — nicht zu *„Sequence contains no matching element"*.
3. Ein leerer (nicht-null) `EligibleScopes`-Satz wird bei der nächsten Auflösung neu ermittelt.
4. Die drei Scope-Namen-Vergleiche im selben Ablauf verhalten sich gleich.
5. `database update` gegen eine frische PostgreSQL-`CoreIdentityTree`-Datenbank liefert die 7 Sichten
   und 12 Funktionen, ohne dass der Konsument `ConfigureViews` selbst aufruft.

---

## Umsetzung (2026-09-08, Zweig `Future_10`)

### Die Ursache

`PostgreSqlColumnsSyntaxHelper` — der Kennzeichen-Join vergleicht jetzt normalisiert:

```sql
INNER JOIN json_array_elements_text(p_user_id::json) uta ON u."NormalizedUserName" = upper(uta.value)
```

`upper()` und nicht beidseitiges `lower()`, damit ein Index auf `NormalizedUserName` benutzbar bleibt.
Das setzt voraus, dass der `ILookupNormalizer` grossschreibt — der `UpperInvariantLookupNormalizer`
von ASP.NET Core Identity, also der Standard, tut das. Wer einen eigenen Normalizer einhängt, muss die
Zeile mitziehen; das steht als Vorbehalt im Code. Die Stelle ist der Generator mit `byLabels`-Schalter,
deckt also beide Kennzeichen-Varianten ab.

### Die Folge-Punkte

**a) Kein ungesichertes `First` mehr** — und es waren fünf, nicht zwei: `GetPermissionsOf`,
`UpdateScopePermissions`, `UpdateScopeFeatures`, `SetScopeRefreshed` und `GetFeaturesOf`. Die
schreibenden Pfade werfen jetzt mit dem angeforderten Scope **und** den zulässigen im Text, die
lesenden liefern `null` und schreiben den Grund ins Log. Zusätzlich prüft
`ResolvingPermissionScope` das Ergebnis von `DefaultScopeExpression` gegen `EligibleScopes` und
klemmt mit einer Warnung auf den ersten zulässigen Scope, statt den Wert ungeprüft weiterzureichen.

**b) Leer wird neu ermittelt** — `eligibles is not { Length: > 0 }` statt `== null`. Kommt die
Auflösung erneut leer zurück, steht das jetzt als Warnung im Log, mit den Kennzeichen. Ein Benutzer,
der wirklich in keinem Mandanten ist, zahlt dafür mit einer Abfrage je Auflösung — der seltene Fall,
und der ehrliche.

**c) Gleiche Frage, gleiche Antwort** — der case-sensitive Vergleich auf `:218` ist weg; alle drei
Stellen vergleichen `OrdinalIgnoreCase`.

**Und einen Schritt weiter:** was aus der Auflösung herauskommt, ist ab jetzt die **gespeicherte**
Schreibweise des Mandanten, nicht die aus der Route oder aus dem Claim. Das ist der eigentliche Grund,
warum die übrigen Wert-Vergleiche im PG-Helper (`st."TenantName" = p_from_leaf`,
`t."OutermostLeafTenantName" = p_from_leaf`) so bleiben durften, wie sie sind: der einzige Weg, auf
dem dort eine abweichende Schreibweise ankam, war eine Route wie `/t001/` bei einem Mandanten `T001`
— die kam durch das case-insensitive Tor und lief danach ins Leere. Kanonisiert man einmal in C#,
trifft jeder nachgelagerte Vergleich die gespeicherte Schreibweise. Ein `lower()` im Anker wäre die
schlechtere Wahl gewesen: genau dort hängt die gemessene Anker-Optimierung (0,2–1,3 ms statt 270 ms).

### Die Deployment-Lücke (§1)

Keine neue Migration — `20260819222431_InitialTreeTenantBuild` trägt es jetzt selbst:
`ConfigureViews` am Ende von `Up`, und am Anfang von `Down` das neue öffentliche `DropViews`, weil
PostgreSQL keine Tabelle löscht, solange eine Sicht darauf steht. `ConfigureViews` räumt zuerst weg
und legt dann neu an, ist also wiederholbar: **künftige Änderungen an den Baum-Objekten gehören in den
Syntax-Helper und brauchen keine eigene Migration** — ein erneuter Lauf trägt sie mit.

### Nicht umgesetzt: das Permission-Seeding (§2)

`IncludeToolkitPermissions()` fehlt in den Tree-Migrationen beider Anbieter — das ist so geblieben.
Im Testprojekt ist der Permission-Interceptor eingeschaltet, der System-Admin bekommt seine
Berechtigungen also ohnehin. Der Befund bleibt oben stehen, falls er später doch entschieden werden
soll.

### Tests

Neun neue, alle im WebCoreToolkit-Testprojekt; Lauf 178/178 grün.

- `ResolvingPermissionScopeTests`: Route-Override kanonisiert die Schreibweise; ein gespeicherter
  Scope, der sich nur in der Gross-/Kleinschreibung unterscheidet, gilt als derselbe; ein
  `DefaultScopeExpression` ausserhalb der zulässigen Menge klemmt statt zu werfen; eine leere Menge
  wird erneut abgefragt.
- `UserScopeLookupTests`: die Ausnahme nennt Scope und Alternativen, die leere Menge wird
  ausgeschrieben, die Lesepfade liefern `null`, und Schreibweise spielt keine Rolle.
- Die Attrappe `FakeSecurityRepository` kann jetzt eine veränderbare Scope-Menge und zählt die
  Abfragen — ohne das lässt sich „wird erneut gefragt“ nicht prüfen.

**Was ungeprüft bleibt:** Abnahmepunkt 1 und 5 laufen gegen eine echte PostgreSQL-Datenbank und sind
hier nicht nachgestellt — der SQL-Text ist geprüft, das Verhalten der Funktion nicht.
