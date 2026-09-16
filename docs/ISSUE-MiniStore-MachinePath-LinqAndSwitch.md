# Issue: Der Maschinen-Weg ist nach PRE244 noch zweimal blockiert — ein Schalter und eine untranslatierbare Abfrage

**Status:** OFFEN — Fehlerbericht ans Toolkit
**Datum:** 2026-09-16
**Quelle:** MiniStore-Session (Konsument). Durchstich von Leitfaden §65.8 Schritt 8, POS-Agent am
ServiceHub.
**Toolkit-Stand:** `5.0.0-PRE244`, .NET 10, EF Core 10, PostgreSQL/Npgsql.
**Folgt auf:** `ISSUE-MiniStore-ApiKey-NoMachinePermissions.md` — der dortige Fix (`ClientAppIdentity.BuildClaims`)
ist in PRE244 drin und **wirkt**: der Anspruch `ClaimTypes.ClientAppAccess` wird jetzt gesetzt. Genau
dadurch werden die beiden folgenden Stellen überhaupt erst erreicht.

## Befund 1 — `MapApplicationId` ist nur aus dem Bearer-Zweig erreichbar

Der Anspruch allein genügt nicht. Die Wicklung zu `##APPUSER##<Label>#` — das Einzige, was
`DbSecurityRepository` als Maschinen-Zugang erkennt — entsteht nur hinter einem Schalter:

```csharp
// ITVComponents.WebCoreToolkit/Security/UserMappers/SimpleUserNameMapper.cs:47
if (user is ClaimsIdentity identity && userMappingOptions.MapApplicationId)
{
    retVal.AddRange(from t in identity.Claims.Where(n => n.Type == ClaimTypes.ClientAppAccess)
                    select string.Format(Global.AppUserKeyIndicatorFormat, t.Value));
}
```

(gleichlautend in `User2GroupsMapper.cs:62`.)

Gesetzt wird `UserMappingOptions.MapApplicationId` im ganzen Repositorium an **einer** Stelle — und
zwar ausschliesslich innerhalb des Bearer-Zweigs:

```csharp
// ITVComponents.WebCoreToolkit.Authentication/WebPartInit.cs:113
if (configureBearer)
{
    …
    services.Configure<UserMappingOptions>(o => { o.MapApplicationId = bearerConfig.MapApplicationId; });
}
```

**Wer sich per `X-Api-Key` anmeldet und kein Bearer konfiguriert, hat den Schalter auf `false`** — und
damit wieder keine Maschinen-Rechte, trotz gesetztem Anspruch. Der Schalter steuert einen
Mechanismus, den *beide* Anmeldewege brauchen, hängt aber an der Konfiguration nur eines davon.

**Vorschlag:** den Schalter aus dem ApiKey-Abschnitt ebenfalls setzbar machen — oder, weil er
ohnehin nur eine Wicklung erlaubt, die ohne den Anspruch gar nicht entsteht: ihn für den
ClientApp-Anspruch **entfallen** lassen. Ein Konsument, der `UseClientAppResolver` einschaltet, hat
sich für Anwendungs-Zugänge entschieden; dass deren Rechte dann an einem zweiten, anderswo
angesiedelten Schalter hängen, ist nicht erwartbar.

*(Wir setzen ihn bis dahin selbst — `builder.Services.Configure<UserMappingOptions>(o => o.MapApplicationId = true);`
ist eine gewöhnliche Options-Konfiguration und damit unbedenklich.)*

## Befund 2 — die Abfrage des Maschinen-Zugangs ist nicht übersetzbar und wirft auf .NET 10

Mit gesetztem Schalter läuft der Weg weiter — und stirbt in der Claims-Transformation, **bevor** der
Endpunkt überhaupt erreicht wird:

```
fail: Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware[1]
      System.InvalidOperationException: An exception was thrown while attempting to evaluate a LINQ
      query parameter expression.
       ---> System.ArgumentException: GenericArguments[1], 'System.ReadOnlySpan`1[System.String]',
            on 'System.Linq.Expressions.Interpreter.FuncCallInstruction`2[T0,TRet]' violates the
            constraint of type 'TRet'.
       ---> System.TypeLoadException: …
         at System.Linq.Expressions.Interpreter.CallInstruction.Create(…)
         …
         at ITVComponents…TreeShared.Security.DbSecurityRepository`45.GetCustomProperties(
                String[] userLabels, String userAuthenticationType, CustomUserPropertyType propertyType)
         at ITVComponents.WebCoreToolkit.Security.SecurityRepository.GetCustomProperties(…)
         at ITVComponents.WebCoreToolkit.Security.ClaimsTransformation.RepositoryClaimsTransformation.TransformAsync(…)
         at ITVComponents.WebCoreToolkit.Security.ClaimsTransformation.CollectedClaimsTransform.TransformAsync(…)
         at ITVComponents.WebCoreToolkit.Blazor.Security.TenantPathPrefixMiddleware.InvokeAsync(HttpContext context)
```

Die Stelle:

```csharp
// …/TreeShared/Security/DbSecurityRepository.cs:477 (GetCustomProperties)
tenantUsers = securityContext.ClientAppAccesses
    .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
    .Where(n => n.TenantUserId != null)
    .Select(n => n.TenantUser.User);
```

`Enumerable.Contains(source, value, IEqualityComparer<T>)` — die Überladung **mit Vergleicher** —
kann EF Core nicht übersetzen. Es versucht daraufhin, den Ausdruck als Parameter auszuwerten, und
der Ausdrucks-Interpreter scheitert auf .NET 10 an der `params ReadOnlySpan<string>`-Gestalt der
beteiligten Methode. Die Meldung nennt weder `Contains` noch die Tabelle.

**Das Muster steht nicht nur einmal da:** je **8** Vorkommen in
`Shared/Security/DbSecurityRepository.cs` und in `TreeShared/Security/DbSecurityRepository.cs` —
unter anderem in `GetPermissions`, `GetCustomProperties` und den Navigations-Abfragen. Betroffen ist
also der ganze Maschinen-Leseweg, nicht eine einzelne Methode.

**Vorschlag:** die Vergleicher-Überladung durch etwas Übersetzbares ersetzen. Da `filteredLabels`
ohnehin client-seitig entsteht, genügt das Normalisieren vor der Abfrage:

```csharp
var filteredLabels = (… select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value.ToLowerInvariant())
    .ToArray();
…
.Where(au => filteredLabels.Contains(au.Label.ToLower()))
```

Das entspricht dem, was der Benutzer-Filter an anderer Stelle bereits tut
(`lower(u."UserName") = ANY (@lbl)` in den erzeugten Abfragen) — und es läuft in der Datenbank statt
im Speicher.

**Zur Gegenprobe wäre uns eine Aussage wichtig:** ist dieser Weg schon einmal gegen eine echte
Datenbank gelaufen? Leitfaden §65.9 sagt „Am Host nicht geprüft" für die Masken; die Fundstellen hier
legen nahe, dass auch der Maschinen-Leseweg noch nie ausgeführt wurde. Das ist keine Kritik, sondern
eine Frage der Erwartung: wir richten unsere Prüftiefe danach.

## Was das Fehlerbild so teuer macht

Beide Befunde tarnen sich als etwas anderes, und der zweite besonders:

- Die Anmeldung protokolliert sich **erfolgreich** samt Mandant und `machine: True`.
- Die Anfrage stirbt in der **Middleware**, nicht im Endpunkt. In unserem Protokoll standen deshalb
  12 × `Request matched endpoint 'gRPC - …/RegisterService'` und **0 ×** `Executing endpoint`. Wer
  nur nach Ablehnungen sucht, findet keine — und hält das für Erfolg. (Wir sind genau darauf
  hereingefallen: „null Ablehnungen" war kein Erfolg, sondern die Folge davon, dass die
  Rechteprüfung nie erreicht wurde.)
- Die LINQ-Meldung spricht von `ReadOnlySpan<String>` und `FuncCallInstruction` — sie zeigt auf die
  Laufzeit, nicht auf die Abfrage, und nennt die betroffene Tabelle nicht.

## Priorität

**Hoch.** Befund 1 umgehen wir; Befund 2 nicht — er sitzt in der Rechteauflösung selbst, und dort
hat ein Konsument nichts verloren. Der dokumentierte Normalweg für Maschinen (§65.8 Schritt 8) ist
damit auf .NET 10 nicht benutzbar.
