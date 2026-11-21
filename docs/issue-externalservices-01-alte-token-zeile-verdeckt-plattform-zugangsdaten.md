# External Services 01: Token-Zeile aus 4.x verdeckt nach der Credential-Migration die Plattform-Zugangsdaten

**Typ:** Bug (Regression 4.x → 5.x, Migrationslücke)
**Betrifft:**
- `ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity\Shared\ExternalOAuthServices\ExternalServiceCredentialMigration.cs` (`ExternalServiceCredentialMigrator`)
- `ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity\TreeShared\Security\DbSecurityRepository.cs` (`GetExternalOAuthServiceLogin`, `GetBufferedToken`, `ApplyCredentialRow`)
- analog zu prüfen: `Shared\Security\DbSecurityRepository.cs` (flach)

## Problem

In 4.x standen die festen Zugangsdaten an der Dienst-Definition (`ExternalOAuthServices.ClientId/ClientSecret`). Die
Zeilen in `ExternalOAuthServiceTenantLogins` waren reine **Token-Caches** je Mandant. Bei Client-Credentials- und
Password-Flow wurde die Zeile beim ersten Abruf für den anfragenden Mandanten angelegt.

Seit §93 gilt: *„Token und Zugangsdaten kommen aus derselben Zeile“*. `ExternalServiceCredentialMigrator` zieht die
Zugangsdaten eines globalen Dienstes in die **Plattform-Zeile** (`TenantId = NULL`) und setzt
`AllowPlatformCredentials = true`. Die bestehenden Token-Zeilen der Mandanten fasst er nicht an. Diese Zeilen haben
kein `CredentialJson`, und durch die neue Spalte stehen sie nach der Migration auf `Inheritable = 1`.

`GetExternalOAuthServiceLogin` sucht zuerst im Mandantenbaum und greift erst danach auf die Plattform-Zeile zurück.
Die alte Token-Zeile des Mandanten gewinnt deshalb (`ParentLevel 1`). `ApplyCredentialRow` legt ihre leeren
Zugangsdaten auf die Verbindung, und ist das Token abgelaufen, schickt `RefreshAsync` den Grant mit leerem Benutzer
und Passwort.

**Beobachtet** in ESAG-PartnerPortal (Port 4.0.0-PRE314 → 5.0.0-PRE280, Baum-Strategie), Dienst `Docuware`
(`OAuthPasswordFlow`, global):

| Tabelle | Zeile | Zustand nach Migration |
|---|---|---|
| `ExternalOAuthServices` | 5 `Docuware` | `ClientId`/`ClientSecret` leer, `AllowPlatformCredentials = 1` |
| `ExternalOAuthServiceTenantLogins` | 8, `TenantId NULL` | `CredentialJson` gesetzt (die umgezogenen Daten), `Token = "{}"` |
| `ExternalOAuthServiceTenantLogins` | 4, `TenantId 12478` | altes 4.x-Token, **`CredentialJson NULL`**, `Inheritable = 1` |

Log:

```
Token request for external service 'Docuware' (OAuthPasswordFlow) to https://…/DocuWare/Identity/connect/token was
rejected with 400 (Bad Request). Sent fields: grant_type, username (empty), password (empty), scope, client_id,
organization. Response: {"error":"invalid_grant"}
   at …OAuthTokenService.RefreshAsync(…)
   at …OAuthTokenService.GetValidAccessTokenAsync(String connectionName)
   at …OAuthBearerHandler.SendAsync(…)
```

Betroffen sind alle Flows mit festen Zugangsdaten (Client-Credentials, Basic-Client-Credentials, Password, und sinngemäss
ApiKey/Basic/Bearer, sofern diese Token-Zeilen hatten), sobald vor dem Update ein Mandant schon einmal ein Token
gezogen hat. Bei Authorization-Code ist die Mandanten-Zeile das Login selbst, dort ist alles korrekt.

## Vorschlag

Bevorzugt im **Migrator** beheben, weil die Lücke dort entsteht. Für jeden umgezogenen Dienst, der kein
Authorization-Code-Dienst ist, werden die übrigen Zeilen dieses Dienstes ohne `CredentialJson` bereinigt. Für die
Zeile gibt es zwei Varianten:

- **(a) Zeile entfernen:** Danach greift die Plattform-Zeile (bzw. bei mandanteneigenen Diensten die Besitzer-Zeile),
  und das nächste Token landet über `PersistToken` dort. Der Cache ging verloren, aber das Token wird ohnehin neu
  gezogen. Das ist die einfachste und konsistenteste Variante.
- **(b) Zugangsdaten hineinkopieren:** Die Zugangsdaten werden mit dem Schlüssel des Zeilenbesitzers in die
  Mandanten-Zeile kopiert. Das erhält das Verhalten 1:1, vervielfacht aber das Geheimnis, und eine spätere Änderung an
  der Plattform-Zeile wirkt dort nicht mehr. Davon rate ich ab.

Zusätzlich defensiv in `GetExternalOAuthServiceLogin` bzw. `GetBufferedToken`: Hat die gefundene Zeile **kein**
`CredentialJson` und ist der Dienst kein Authorization-Code-Dienst, gilt sie für die Auflösung der Zugangsdaten nicht
als Treffer. Dann wird weitergesucht (Baum aufwärts, dann Plattform, wenn `AllowPlatformCredentials`). Dabei muss
gelten:
- Token und Zugangsdaten bleiben dann **aus derselben Zeile**, also der gefundenen mit Zugangsdaten. Die leere Zeile
  wird ignoriert und nicht gemischt.
- Mindestens ein Warning-Log, wenn eine Zeile ohne Zugangsdaten übersprungen wird oder wenn am Ende gar keine
  Zugangsdaten gefunden werden. Die jetzige Meldung „username (empty)“ ist gut, kommt aber erst nach dem Roundtrip
  zum Provider.

## Akzeptanzkriterien

- Szenario: Dienst mit Password- oder Client-Credentials-Flow, global, Zugangsdaten an der Definition, plus eine 4.x-
  Token-Zeile eines Mandanten ohne `CredentialJson`. Nach dem Start mit der neuen Version holt der Mandant ein Token mit
  den umgezogenen Plattform-Zugangsdaten.
- Authorization-Code-Dienste und ihre Mandanten-Logins bleiben unverändert.
- Der Migrator bleibt idempotent, ein zweiter Start ändert nichts.
- Test für flache und Baum-Ausprägung.

## Workaround (bis zum Fix)

Die alte Token-Zeile des Mandanten für den Dienst löschen (im Beispiel `ExternalOAuthServiceTenantLogins` Id 4). Danach
greift die Plattform-Zeile.
