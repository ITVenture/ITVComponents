# Issue: Ein per `X-Api-Key` angemeldetes Gerät bekommt seinen Mandanten — aber **keine Rechte**

**Status:** OFFEN — Fehlerbericht ans Toolkit
**Datum:** 2026-09-16
**Quelle:** MiniStore-Session (Konsument). Beim Durchstich von Leitfaden §65.8 Schritt 8 aufgetreten:
der POS-Agent meldet seinen Gerätedienst am ServiceHub an.
**Toolkit-Stand:** `5.0.0-PRE243`. Alle Aussagen unten sind am Quelltext und am laufenden System
verifiziert.
**Hängt zusammen mit:** `ISSUE-MiniStore-ApiKey-HashedResolver.md`, `ISSUE-MiniStore-DevicePairing.md`
(beide geliefert), `ISSUE-MiniStore-JwtAuthInit-Unimplemented.md` (der Weg, der *funktioniert*).

## Der Fall

Der API-Schlüssel-Weg ist der, den §65.8 als den normalen beschreibt:

> 8. **Anmelden**: der Agent schickt ihn fortan als `X-Api-Key`. Er ist damit im Mandanten der
>    Anwendung angemeldet und hat die Rechte ihrer Bündel.

Die erste Hälfte stimmt, die zweite nicht. Am laufenden System:

```
dbug: …ClientAppApiKeyResolver[0]
      Client-app access Kasse1-dd0b0427139c4f208f5c3e06cfdb1db3
      authenticated for tenant 45e6968d5a7044c38ee17787efe0ee66 (machine: True).
dbug: Microsoft.AspNetCore.Authorization.DefaultAuthorizationService[1]
      Authorization was successful.
info: Microsoft.AspNetCore.Routing.EndpointMiddleware[0]
      Executing endpoint 'gRPC - /ITVRpcComm.ServiceHub/RegisterService'
…
ERROR - Type: System.Security.SecurityException;
        Message: Access denied for the following permissions: ActAsService
```

Die Datenkette ist vollständig — nachgeprüft in der Datenbank:

| | |
|---|---|
| `ClientApps` | `POS-Standard`, `Enabled = true`, Mandant des Ladens |
| `ClientAppAccesses` | `Kasse1-dd0b0427…`, `RevokedUtc IS NULL`, `TenantUserId IS NULL` (Maschine) |
| `ClientAppPermissions` | Anwendung → Bündel *Gerätedienst* |
| `AppPermissions` | Bündel → `ActAsService` |
| `Permissions` | `ActAsService`, `TenantId IS NULL` (global) |

## Die Ursache

Die Rechte einer **Maschine** hängen an genau einem Claim, und der wird auf diesem Weg nie gesetzt.

**1. Der Leseweg** — `DbSecurityRepository` erkennt einen Anwendungs-Zugang ausschliesslich am
gewickelten Label:

```csharp
// …/TreeShared/Security/DbSecurityRepository.cs (~618)
var filteredLabels = (from ul in userLabels
    where Regex.IsMatch(ul, Global.AppUserKeyPattern)
    select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();

var appUsers = securityContext.ClientAppAccesses
    .Where(n => n.ClientApp.TenantId == securityContext.CurrentTenantId.Value && …)
    .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase));

var machinePerms = AppSetPermissions(appUsers.Where(n => n.TenantUserId == null));
```

Ist `filteredLabels` leer, ist `appUsers` leer, ist `machinePerms` leer — und es gibt kein einziges
Recht. **Ohne Fehler und ohne Hinweis:** die Abfrage findet schlicht nichts.

**2. Wer die Wicklung erzeugt** — die Benutzer-Mapper, aus einem Claim vom Typ
`ClaimTypes.ClientAppAccess` (`urn:ITV:IWCT:App:UserId`):

```csharp
// ITVComponents.WebCoreToolkit/Security/UserMappers/SimpleUserNameMapper.cs:49
// (gleichlautend in User2GroupsMapper.cs:66)
retVal.AddRange(from t in identity.Claims.Where(n => n.Type == ClaimTypes.ClientAppAccess)
                select string.Format(Global.AppUserKeyIndicatorFormat, t.Value));
```

**3. Wer diesen Claim setzt** — im ganzen Repositorium **eine** Stelle:

```csharp
// ITVComponents.WebCoreToolkit.Authentication/OpenId/JWT/Impl/JwtTokenService.cs:112
claims.Add(new Claim(ClaimTypes.ClientAppAccess, applicationUserLabel));
```

**4. Was der API-Schlüssel-Weg setzt** — ihn nicht:

```csharp
// ITVComponents.WebCoreToolkit.Authentication/ApiKey/ClientAppApiKeyResolver.cs (~92)
var claims = new List<Claim>
{
    new Claim(WebCoreToolkit.ClaimTypes.FixedUserScope, access.TenantName)
};
…
return new ApiKeyInfo(access.Label, DateTime.UtcNow, claims);
```

Der Handler reicht die Zusatz-Ansprüche korrekt weiter (`claims.AddRange(existingApiKey.AdditionalClaims)`,
`ApiKeyAuthenticationHandler.cs` ~63) — es kommt nur nichts an, was ankommen könnte. Der `Label`
landet als `ClaimTypes.Name`, und der Leseweg schaut dort nicht hin.

**Ergebnis:** über `X-Api-Key` authentifiziert sich das Gerät, bekommt seinen Mandanten und hat
**null Rechte**. Über JWT funktioniert derselbe Zugang, weil `JwtTokenService` den Claim ergänzt.
Zwei Wege zur selben Identität, von denen nur einer die Rechte mitbringt.

## Warum das teuer ist

Das Fehlerbild zeigt in die falsche Richtung, und zwar dreifach:

- **Die Anmeldung gelingt** und protokolliert sich sogar erfolgreich, mit korrektem Mandanten und
  `machine: True`. Wer das liest, hakt die Authentifizierung ab.
- **`Authorization was successful`** steht *vor* dem Fehler — das ist die ASP.NET-Prüfung am
  Endpunkt, nicht die Rechteprüfung des Toolkits. Die beiden sehen im Protokoll gleich aus.
- Die Meldung lautet `Access denied for the following permissions: ActAsService`. Man sucht dann
  beim Berechtigungs-Set, bei der Vorlage, beim Widerruf — also überall dort, wo die Daten stehen,
  die alle stimmen.

Wir haben die ganze Kette in der Datenbank nachgeprüft, bevor der Verdacht überhaupt auf die Claims
fiel. Ein Satz im Protokoll — *„no client-app access claim present; machine permissions cannot be
resolved"* — hätte das abgekürzt.

## Vorschlag

**1. Der eigentliche Fix ist eine Zeile.** `ClientAppApiKeyResolver` ergänzt den Claim, den der
Leseweg braucht:

```csharp
var claims = new List<Claim>
{
    new Claim(WebCoreToolkit.ClaimTypes.FixedUserScope, access.TenantName),
    new Claim(WebCoreToolkit.ClaimTypes.ClientAppAccess, access.Label)   // NEU
};
```

Der Label ist an dieser Stelle bereits geprüft (Geheimnis verglichen, nicht widerrufen, nicht
abgelaufen, Anwendung eingeschaltet) — es wird also nichts Ungeprüftes zum Claim.

**2. Eine gemeinsame Stelle wäre uns lieber als zwei.** Beide Wege bauen dieselbe Identität aus
demselben `ClientAppAccess`; dass der eine drei Ansprüche setzt und der andere zwei, ist der Fehler
selbst — nicht seine Ursache. Ein Helfer (`ClientAppIdentity.BuildClaims(access)`), den `JwtTokenService`
und `ClientAppApiKeyResolver` gemeinsam rufen, macht die nächste Abweichung unmöglich.

**3. Der leere Leseweg sollte etwas sagen.** `DbSecurityRepository` weiss im Moment des
Nichttreffers, dass gar kein Anwendungs-Label dabei war — das ist ein anderer Zustand als „Label da,
aber keine Bündel". Eine Debug-Zeile, die beide Fälle trennt, spart die Suche oben.

## Was wir bis dahin tun

Eine `IClaimsTransformation` im Host, die den fehlenden Claim für Identitäten des ApiKey-Schemas aus
dem `Name` nachträgt. Das ist eng und sicher — der Name ist an dieser Stelle der bereits geprüfte
Label —, aber es ist eine Umgehung an einer Stelle, an der ein Konsument nichts zu suchen hat: sie
fasst die Rechteauflösung an. Sie verschwindet, sobald der Resolver den Claim selbst setzt.

**Priorität von unserer Seite: hoch.** Nicht wegen der Umgehung, die hält — sondern weil der
dokumentierte Normalweg (§65.8 Schritt 8, `X-Api-Key`) für jede Maschine ohne Rechte endet, und der
Befund sich als Rechteproblem tarnt. Wer ihn nicht kennt, sucht zuerst überall sonst.
