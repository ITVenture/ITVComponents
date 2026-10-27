# PLAN: ExternalServices — Definition und Zugangsdaten trennen

**Status:** Plan, nicht umgesetzt · **Datum:** 2026-10-27 · **Zweig:** Future_10 · **Anlass:** MiniStore E10 Teil 2
(„Wo liegen die Zugangsdaten des Anbieters?“), §87 (geheime Token-Parameter)

> Dieser Plan ist eine Hypothese über den Code. Jede Phase beginnt damit, die Befunde, auf denen sie aufbaut, am
> Code zu bestätigen.

## 1. Ziel

Die **Definition** eines Dienstes beschreibt, *was* angesprochen wird und *wie*: Endpunkte, Anmeldetyp, Scope,
Token-Parameter und die Registrierung der **App** (bei OAuth die Client-Kennung der Plattform). Die
**Zugangsdaten** beschreiben, *wer* sich anmeldet: API-Schlüssel, Benutzer und Passwort, eigener Client,
OAuth-Token. Sie gehören immer einem Mandanten (oder, ausdrücklich, der Plattform) und liegen nie an der
Definition.

## 2. Entscheidungen (User, 2026-10-27)

| Frage | Entscheid |
|---|---|
| Vererbung der Zugangsdaten im Baum | **Derselbe Mechanismus wie Settings und Plugins:** `Inheritable` an der **Zugangsdaten-Zeile**, Auflösung über `UpwardsTenantTreeView` bis zur Wurzel, die nächste passende Zeile gewinnt (siehe 4.2). Zugangsdaten beim obersten Mandanten gelten so für alle Nachkommen; darunter sind brechende oder exklusive Ausnahmen möglich. Zahlungen erben nie. *(Ersetzt den ersten Entscheid „Flag je Definition“, 2026-10-27.)* |
| Zugangsdaten auf Systemebene | **Ja, als ausdrücklicher Rückfall:** eine Zeile ohne Mandant = Plattform-Konto. Mandanten fallen nur darauf zurück, wenn die Definition es erlaubt. Zahlungen nie. |
| Bestehende Mandantendienste | **Definitionen bleiben, Geheimnisse ziehen um.** Mandanten dürfen weiter eigene Definitionen haben. Die alten Spalten bleiben eine Version lang als Lese-Rückfall. |
| Token-Endpunkt | Client-Credentials, Basic-Client-Credentials und Password-Grant holen ihr Token am **`TokenEndpoint`**, nicht mehr am `AuthorizationEndpoint` (siehe 4.5). |

## 3. Ist-Stand (Bestandsaufnahme 2026-10-27, zu verifizieren)

- **Alles an einer Zeile.** `ExternalOAuthService` trägt Endpunkte und `ClientId`/`ClientSecret`. Was die beiden
  Felder bedeuten, hängt vom Typ ab:

  | Typ | `ClientId` | `ClientSecret` | Gehört eigentlich zu |
  |---|---|---|---|
  | `OAuthAuthorizationFlow` | Client der App | Secret der App | Definition; das **Token** (TenantLogin) ist Zugangsdaten |
  | `OAuthClientCredentialsFlow`, `OAuthBasicClientCredentialsFlow` | Client des Kontos | Secret des Kontos | Zugangsdaten |
  | `Basic` | Benutzer | Passwort | Zugangsdaten |
  | `ApiKey` | **Header-Name** | Schlüssel | Header-Name → Definition, Schlüssel → Zugangsdaten |
  | `BearerToken` | — | Token | Zugangsdaten |
  | `OAuthPasswordFlow` | Benutzer | Passwort | Zugangsdaten; die App (`client_id`/`client_secret`) steht in den Token-Parametern → Definition |

- **Schlüsselwahl uneinheitlich:** Die Repositories ver- und entschlüsseln nach der Spalte `Global`, der
  Zahlungs-Speicher nach `TenantId`. Telerik-Update kann `Global`/`TenantId` umschalten, ohne neu zu verschlüsseln.
- **Vererbung heute (Baum):**
  - Dienste erben über das Flag `Inheritable`. Es lässt sich in keiner Maske pflegen, nur über Vorlagen.
  - **Für globale Dienste benutzt ein Kind ohne eigenes Token still das des nächsten Elternteils.** §78.4 sagt
    „ein Kind-Mandant erbt keinen Zugang“, das gilt aber nur im Zahlungs-Speicher.
- **Flach:** Ein eigener Dienst verdeckt den globalen gleichen Namens. *Verdacht:* Der flache Filter hat keinen
  `TenantId == null`-Zweig, globale Dienste wären zur Laufzeit dann unsichtbar.
- **Zahlungs-Speicher** (`ExternalServiceProviderCredentialStore`): legt je Mandant einen Dienst mit **leeren
  Endpunkten** als Behälter an. Er hat einen eigenen Refresh-Nachbau (ohne Scope, ohne Token-Parameter).
- **Kein eindeutiger Index** auf `ExternalOAuthServiceTenantLogin (TenantId, OAuthServiceId)`.
- **Keine Tests** für die Auflösung in beiden Repositories (Verdecken, Vererbung, Logins), für `OAuthTokenService`
  und für den Zahlungs-Speicher.

## 4. Zielmodell

### 4.1 Keine neue Entität

`ExternalOAuthServiceTenantLogin` wird zur **Zugangsdaten-Zeile**. Sie ist schon generisch, trägt `TenantId` und ein
verschlüsseltes `Token`. Eine neue generische Entität am Sicherheitskontext verschöbe die Stelligkeit aller Typen und
entwertete geseedete Vertrauens-Einträge (siehe Erinnerung „Vertrauens-Einträge Stelligkeit“). Der Name bleibt aus
Kompatibilität, die Doku erklärt die erweiterte Rolle.

| Spalte | neu/geändert | Bedeutung |
|---|---|---|
| `TenantId` | **nullable** | `null` = Plattform-Zugang |
| `Token` | unverändert | OAuth-Token (Authorization-Code, gepufferte Grants) |
| `CredentialJson` | **neu**, nullable | verschlüsselt: `{ user?, secret? }`, je Typ, siehe 4.3 |
| `Inheritable` | **neu**, Vorgabe `false` | Nachkommen ohne eigene Zeile benutzen diese Zeile (Mechanismus der Settings, 4.2). Vorgabe bewusst `false`, anders als bei Settings (`true`): Zugangsdaten werden ausdrücklich weitergegeben, nicht aus Versehen. *(Bestätigt, 2026-10-27.)* |
| Index `UQ_ExternalOAuthServiceTenantLogin` | **neu** | eindeutig auf (`OAuthServiceId`, `TenantId`); bei SQL Server gefiltert bzw. mit `NULL`-Regel beachten |

An der Definition (`ExternalOAuthService`) neu:

| Spalte | Vorgabe | Bedeutung |
|---|---|---|
| `AllowPlatformCredentials` | `false` | Mandanten ohne eigene (und ohne geerbte) Zugangsdaten benutzen die Plattform-Zeile |

Die Vererbung hängt **nicht** an der Definition, sondern an der Zeile (4.2). Das bestehende `Inheritable` der
Definition (Baum) bleibt, was es ist: ob die *Definition* in Kind-Mandanten sichtbar ist.

`ClientId`/`ClientSecret` bleiben an der Definition, aber nur noch für die **App** (Authorization-Code) und als
**Header-Name** (ApiKey). Für alle anderen Typen werden sie nach der Übergangsversion leer.

### 4.2 Auflösung — eine Stelle für alle

Neue Klasse (Arbeitstitel) `ExternalServiceCredentialResolver` im TenantSecurity-Paket. Flaches Repository, Baum-
Repository und Zahlungs-Speicher benutzen sie. Für Dienst *S* und Mandant *T*:

1. **Baum:** über `UpwardsTenantTreeView` von *T* bis zur Wurzel die **nächste** Zeile, für die gilt
   `ParentLevel == 1 || Inheritable` — dieselbe Abfrage wie `TenantSettingsProvider` und `DbPluginsSelector`.
   Daraus folgt ohne Sonderfall:
   - **Zeile beim obersten Mandanten, vererbbar:** gilt für alle Nachkommen.
   - **Eigene Zeile, vererbbar** (brechende Ausnahme): gilt für diesen Mandanten und seinen Teilbaum.
   - **Eigene Zeile, nicht vererbbar** (exklusive Ausnahme): gilt nur für diesen Mandanten; seine Kinder überspringen
     sie und erben weiter von oben.

   **Flach:** nur die Zeile mit `TenantId = T`.
2. *(entfällt — in Schritt 1 enthalten)*
3. Wenn `S.AllowPlatformCredentials`: Zeile mit `TenantId = null`.
4. Sonst: keine Zugangsdaten → bei Authorization-Code „nicht verbunden“, sonst ein Fehler mit klarem Text.
5. **Übergang (eine Version):** keine Zeile gefunden → Rückfall auf `ClientId`/`ClientSecret` der Definition, mit
   Warnung im Log.

Der Zahlungs-Speicher ruft die Auflösung mit der Regel **„nur genau dieser Mandant“** auf: in Schritt 1 nur
`ParentLevel == 1`, Schritte 3 und 5 abgeschaltet — unabhängig davon, was an einer Zeile steht. Das ist §78.4, jetzt an einer Stelle statt nachgebaut.

Das Ergebnis bleibt das bisherige DTO `ExternalServiceConnection` (mit `ClientId`/`ClientSecret` befüllt aus der
Definition **oder** den Zugangsdaten, je Typ). **`OAuthTokenService`, `OAuthHttpClientFactory`, die Handler und der
OAuth-Rückruf ändern sich dadurch nicht.** Das ist der grösste Hebel des Entwurfs.

### 4.3 Was in `CredentialJson` steht

| Typ | `user` | `secret` |
|---|---|---|
| `OAuthAuthorizationFlow` | — | — (Token in `Token`) |
| `OAuthClientCredentialsFlow` / `Basic…` | Client des Kontos | Secret des Kontos |
| `Basic` | Benutzer | Passwort |
| `ApiKey` | — | Schlüssel |
| `BearerToken` | — | Token |
| `OAuthPasswordFlow` | Benutzer | Passwort |

### 4.4 Schlüssel

**Der Besitzer der Zeile entscheidet:** `TenantId = null` → Systemschlüssel, sonst der Schlüssel dieses Mandanten.
Das gilt für `CredentialJson` und `Token`. Die Definition verschlüsselt ihr App-Secret und ihre geheimen
Token-Parameter wie heute. Dabei wird die Regel auf **`TenantId` statt `Global`** vereinheitlicht, damit
Umschalten nicht mehr den Schlüssel verfälscht.

### 4.5 Token-Endpunkt

Heute posten Client-Credentials (1), Basic-Client-Credentials (2) und Password-Grant (6) an den
`AuthorizationEndpoint`. Die Telerik-Maske zeigt für diese Typen auch nur dieses Feld (Pflicht); der
`TokenEndpoint` erscheint nur beim Authorization-Code-Flow. Bestehende Daten tragen die Token-URL dieser Typen also
im **falschen Feld**.

- **Laufzeit:** Typen 1, 2 und 6 nehmen den `TokenEndpoint`. Übergang (eine Version): ist er leer, gilt der
  `AuthorizationEndpoint`, mit Warnung im Log.
- **Daten:** Der Umzug (Phase 3) kopiert für diese Typen `AuthorizationEndpoint` → `TokenEndpoint`, wo dieser leer
  ist. Der `AuthorizationEndpoint` bleibt danach ungenutzt.
- **Masken** (Blazor und Telerik): für diese Typen nur noch „Token-Endpunkt“.
- **Zahlungs-Speicher:** benutzt schon heute den `TokenEndpoint`, keine Änderung.

## 5. Oberflächen

- **Definition** (Blazor und Telerik): ohne Zugangsdaten-Felder, ausser App-Client und Header-Name. Neuer Schalter
  `AllowPlatformCredentials`; das bestehende `Inheritable` der Definition (Baum) wird endlich pflegbar.
- **Zugangsdaten** je Dienst: Mandanten-Admin pflegt die Zeile des **eigenen** Mandanten samt Schalter
  „an Untermandanten weitergeben“ (`Inheritable`, nur Baum), Sysadmin die Plattform-Zeile. Werte nur schreibbar
  (wie `ClientSecret` heute), Anzeige „eigene / geerbt von X / Plattform / fehlt“. Die bestehende Login-Liste wird zu
  dieser Ansicht.
- **Rechte:** wie §84 (`AccessibleServices`); Zugangsdaten eines fremden Mandanten sind nie sichtbar.

## 6. Export, Vorlagen, Zahlungen

- **Konfigurations-Export:** Definitionen mit `AllowPlatformCredentials`; **Zugangsdaten nie**. Das ist die Regel des
  `ClientSecret`, jetzt strukturell statt per Ausnahme.
- **Mandanten-Vorlagen:** wie Export, keine Zugangsdaten. Ein angelegter Dienst hat keine Zeile, bis sie gesetzt
  wird.
- **Zahlungs-Speicher:** liest und schreibt Zeilen über die Auflösung mit „nur genau dieser Mandant“. *Später,
  optional:* je Anbieter **eine** globale Definition (statt Behälter-Dienst mit leeren Endpunkten je Mandant).
  Das ist nach dem Entscheid „Definitionen bleiben“ nicht Teil dieses Plans.

## 7. Phasen

| Phase | Inhalt | Schema |
|---|---|---|
| **0** | Unabhängige Fehler beheben, je mit Test: Sichtbarkeit globaler Dienste im flachen Modell (erst verifizieren); NRE im Baum-Repository bei unbekanntem Namen; Telerik-Update darf `Global`/`TenantId` nicht umschalten; Schlüsselwahl im Zahlungs-Speicher; Widerruf beim Anbieter (`RevokeAsync` hat keine Aufrufer). Dazu Tests für die **heutige** Auflösung als Sicherheitsnetz. | nein |
| **1** | Spalten (Login: `TenantId` nullable, `CredentialJson`, `Inheritable`, eindeutiger Index; Dienst: `AllowPlatformCredentials`), Modell, manuelles SQL (SQL Server + PostgreSQL) | ja |
| **2** | `ExternalServiceCredentialResolver`, beide Repositories darauf, Übergangs-Rückfall; Token-Endpunkt (4.5) mit Rückfall; Test-Matrix Verdecken × Vererbung (Wurzel / brechend / exklusiv) × Plattform × Zahlung | nein |
| **3** | **Daten-Umzug im Code** (nicht SQL: neu verschlüsseln mit dem Besitzer-Schlüssel). Je Dienst die Zugangsdaten in die Zeile des Besitzers; bestehende globale Dienste mit Geheimnis → Plattform-Zeile + `AllowPlatformCredentials = 1`; im **Baum** bestehende Logins globaler und vererbbarer Dienste → `Inheritable = 1` (heute benutzen Kinder sie still mit; das Verhalten bleibt, Admins verschärfen). Token-Endpunkt der Typen 1, 2, 6 übernehmen (4.5). Idempotent, als Admin-Aktion oder Start-Schritt, mit Bericht. | nein |
| **4** | Oberflächen (5), Export und Vorlagen (6) | nein |
| **5** | Zahlungs-Speicher auf die Auflösung | nein |
| **6** | Nächste Version: Rückfall und Zugangsdaten-Inhalt der alten Spalten entfernen | ggf. |

Jede Phase ist für sich auslieferbar. Ab Phase 2 verhält sich ein unveränderter Bestand gleich wie vorher (der
Rückfall), ab Phase 3 liegen die Daten neu.

## 8. Breaking / Leitfaden

- Schema (Phase 1): `ExternalOAuthServiceTenantLogins.TenantId` nullable, zwei Spalten (`CredentialJson`,
  `Inheritable`), ein Index; eine Spalte an `ExternalOAuthServices` (`AllowPlatformCredentials`).
- **Token-Endpunkt:** Typen 1, 2, 6 rufen den `TokenEndpoint`. Ohne Umzug greift eine Version lang der Rückfall.
- **Verhalten Baum:** Ein Kind erbt Zugangsdaten nur noch von einer Zeile mit `Inheritable` (Vorgabe aus). Der
  Umzug setzt das Flag für den Bestand, damit sich nichts unbemerkt ändert.
- Eigene `ISecurityRepository`-Implementierungen: keine Signaturänderung geplant (das DTO bleibt).

## 9. Offene Punkte

- SQL-Server: eindeutiger Index mit `NULL` in `TenantId` (nur eine Plattform-Zeile je Dienst) — gefilterter Index
  oder berechnete Spalte?
- `ExternalOAuthServiceState.TenantId` (OAuth-Zustand) bleibt nicht nullable; die Plattform verbindet sich nicht per
  Browser. Bestätigen.
- Payrexx legt heute den Webhook-Schlüssel zweckfremd als `BearerToken`-Dienst ab (`-webhook`). Mit eigenen
  Zugangsdaten-Zeilen wäre ein zweites Geheimnis an derselben Zeile denkbar (`CredentialJson` als Map). Entscheiden
  in Phase 5.
