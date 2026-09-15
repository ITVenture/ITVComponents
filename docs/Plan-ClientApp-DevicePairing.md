# Plan: ClientApps tragfaehig machen + Geraete-Kopplung

**Stand:** ENTWURF, 2026-09-15
**Zweig:** Future_10, Toolkit-Stand `5.0.0-PRE239`
**Anstoss:** `docs/ISSUE-MiniStore-DevicePairing.md`, `docs/ISSUE-MiniStore-ApiKey-HashedResolver.md`
**Nicht in diesem Plan:** die JWT-Achse (`JwtAuthInit`, `ApplicationTokenService`,
`docs/ISSUE-MiniStore-JwtAuthInit-Unimplemented.md`) - wird danach separat angeschaut.

---

## 1. Ausgangslage

Das ClientApp-Konzept ist **hinten fertig und vorne gar nicht gebaut**.

**Fertig:** `DbSecurityRepository` (flach und Tree) loest vollstaendig auf -
`Claim ClientAppUser` -> UserMapper -> `##APPUSER##<Label>#` -> `ClientAppUsers.Label` ->
`ClientApp.AppPermissions` -> `AppPermissionSet` -> `Permission`. Neun Stellen je Fassung.

**Fehlt:**

- **Kein Schreibweg.** Keine Zeile im Repo legt eine `ClientApp`, einen `ClientAppUser` oder eine
  `ClientAppPermission` an. Fuenf Tabellen, sechs Migrationen, seit 2022 unveraendert, nur per Hand-SQL
  befuellbar.
- **`ClientKey`/`ClientSecret` sind tote Spalten.** Repo-weit kein Lese- oder Schreibzugriff. Beide
  `nvarchar(max)` ohne Index - in SQL Server nicht einmal indizierbar.
- **Der Template-Zweig ist invers zum Nutzen gebaut.** `ClientAppTemplate` hat als Einziges eine
  vollstaendige Maske (Blazor *und* Telerik) - und es gibt keinen Code, der je ein Template anwendet.
- **API-Key-Pfad und ClientApp-Pfad kennen einander nicht.** `DefaultApiKeyUserResolver` vergleicht den
  Schluessel im Klartext gegen `Users.UserName`.

### Zwei Fehler im Bestand, die mitrepariert werden

**(a) Der Deckel ignoriert den Label-Filter.** `DbSecurityRepository.GetPermissions`:

```csharp
var appUsers     = securityContext.ClientAppUsers.Where(n => n.TenantUser.TenantId == ...);
preFilteredPerms = appUsers.SelectMany(n => n.ClientApp.AppPermissions)...;   // <- ohne Label-Filter!
tenantUsers      = appUsers.Where(au => filteredLabels.Contains(au.Label))...;
```

Der Deckel ist damit die Vereinigung der PermissionSets **aller** Apps mit einem Zugang in diesem
Mandanten. App A hebt den Deckel fuer App B. Steht in beiden `GetPermissions`-Fassungen und im
Tree-Pendant.

**(b) `AppPermissionSet.Name` ist systemweit eindeutig** (`UQ_AppPermissionSetName`). Ein Bundel
"Vollzugriff" kann es genau einmal geben - obwohl Rechtebuendel ihrem Wesen nach anwendungsspezifisch
sind.

---

## 2. Das Zielmodell

```
ClientAppTemplate        global          "POS-Agent"
  └─< AppPermissionSet   je Template     "Vollzugriff", "Nur Druck"
        └─< AppPermission → Permission   (nur GLOBALE Rechte, TenantId = null)

ClientApp                je Mandant      "Filiale Mueller / POS-Agent"
  │   TenantId, ClientAppTemplateId, ClientKey (systemweit eindeutig)
  └─< ClientAppPermission → AppPermissionSet
  │       Invariante: das Set gehoert zum Template DIESER App
  └─< ClientAppAccess      der eigentliche Zugang (heute ClientAppUser)
          Label, SecretHash, ExpiresUtc, RevokedUtc, LastUsedUtc
          TenantUserId = null  → Maschine    (Rechte DIREKT aus den Sets)
          TenantUserId gesetzt → Delegation  (Schnitt mit den Rechten des Benutzers)

DevicePairing            je Mandant      die Zwischenzustaende der Kopplung
```

### Die tragenden Entscheidungen

1. **Die App gehoert einem Mandanten, das Template ist global.** Der Mandanten-Administrator bestimmt,
   welchen Freiheitsgrad er einer Anwendung zugesteht; die Plattform bestimmt, was ueberhaupt zur
   Auswahl steht. Damit ist die Zustimmung explizit und nicht bloss "jemand hat einen Knopf gedrueckt".
2. **Die Rechtebuendel gehoeren zum Template.** Damit ist die Obergrenze eine
   **Fremdschluessel-Invariante** statt einer Rechenregel: eine App kann nur Sets ihres eigenen
   Templates fuehren. Der Administrator kann nichts erteilen, was fuer diese Anwendung keinen Sinn
   ergibt.
3. **Der Administrator waehlt ganze Buendel, nicht einzelne Rechte.** Einzelne Rechte wegzunehmen hiesse,
   ein global geteiltes Set zu aendern - das traefe jeden Mandanten.
4. **Das Geheimnis haengt am ZUGANG, nicht an der App.** Zwanzig Terminals teilen sonst ein Geheimnis
   und ein Widerruf traefe alle.
5. **`TenantUserId` wird optional.** Ohne Benutzer = Maschine, mit Benutzer = Delegation. Ein
   Kassenterminal ist keine Person und braucht keinen Schattenbenutzer in Benutzerlisten, im Onboarding
   und in Mandanten-Benutzerzahlen.
6. **Nur globale Rechte in den Sets.** Das Template ist global; ein mandantengebundenes Recht liesse
   sich nicht in jeden Mandanten anwenden. Prueffung beim Speichern, nicht als Konvention im Kopf.

---

## 3. Schema-Aenderungen

Alle drei Varianten (`Basic`, `CoreIdentity`, `CoreIdentityTree`), beide Datenbanken
(SQL Server, PostgreSQL).

> **KORREKTUR gegenueber dem ersten Entwurf: KEINE generierten Migrationen.**
> Der erste Entwurf sagte "6 Migrationen". Das war falsch. Fuer den **Security-Kontext** gilt in diesem
> Repo eine andere Festlegung: die Migrations-Snapshots unter `…TenantSecurity.SqlServer/*/Migrations`
> (und `.PostgreSql`) hinken seit 2024 hinterher - fuenf von sechs stehen auf EF `ProductVersion 8.0.11`,
> waehrend die Projekte auf `net10.0`/EF `10.0.11` gehoben sind. Ein `dotnet ef migrations add` erzeugt
> dort **den ganzen aufgelaufenen Drift** mit (`ExternalOAuthServices`, `GlobalRoles`, `ServerCookies`,
> `Navigation.Metadata`/`IsPublic`, `WebPlugins.Transient` …) und wuerde beim Host fremde Schema-Objekte
> anlegen.
>
> **Der Regelweg hier:** die Aenderung als Attribut am Modell deklarieren (design-time) und das
> Schema-Delta als **handgeschriebenes SQL** in `docs/Migration-Future_10-MLM.md` dokumentieren, je
> Datenbank. Der Konsument zieht es dort nach; er macht seine Migrationen ohnehin selbst.
>
> Die **Probe-Migration** bleibt als Werkzeug erlaubt und sinnvoll - um zu sehen, welches DDL EF aus den
> Deklarationen macht: `migrations add` → Datei lesen → `migrations remove --force` → Snapshot mit
> `git checkout --` zuruecksetzen. Nur committet wird sie nie.
>
> **Gilt NICHT fuer den WorkflowContext** - der hat gepflegte Migrationsprojekte.

| # | Entitaet | Aenderung |
|---|---|---|
| 1 | `AppPermissionSet` | **+ `ClientAppTemplateId`** (Pflicht, FK). `UQ_AppPermissionSetName` von systemweit auf **je Template** (`ClientAppTemplateId`, `Name`). |
| 2 | `ClientAppTemplatePermission` | **entfaellt** - aus der Verknuepfungstabelle wird der FK aus (1). Wurde nie beschrieben. |
| 3 | `ClientApp` | **+ `TenantId`** (Pflicht, FK, Mandantenfilter), **+ `ClientAppTemplateId`** (Pflicht, FK), **+ `Enabled`**, **+ `CreatedUtc`**. `ClientKey`: `MaxLength(128)` + **systemweit** eindeutig. `ClientSecret`: bleibt unveraendert stehen (JWT-Achse). |
| 4 | `ClientAppPermission` | **+ Unique-Index** `(ClientAppId, AppPermissionSetId)` - fehlt heute. |
| 5 | `ClientAppUser` → **`ClientAppAccess`** | Umbenennung (Entitaet + Tabelle + DbSet). `TenantUserId` wird **nullable**. **+ `SecretHash`**, **+ `ExpiresUtc`**, **+ `RevokedUtc`**, **+ `LastUsedUtc`**, **+ `DeviceLabel`**. `Label`: 50 → 128. `UQ_TUserPerApp` bekommt **Filter** `TenantUserId IS NOT NULL`. |
| 6 | `DevicePairing` | **neu**: `DevicePairingId`, `TenantId`, `ClientAppId`, `DeviceCodeHash`, `UserCode`, `DeviceLabel`, `CreatedUtc`, `ExpiresUtc`, `State`, `ConfirmedByUserId`, `ClientAppAccessId`, `SecretDeliveredUtc`, `PollCount`, `LastPollUtc`. |

### Der gefilterte Unique-Index - KORREKTUR

Der erste Entwurf sagte, `UQ_TUserPerApp` brauche eine Fluent-Konfiguration mit `HasFilter`, weil SQL
Server NULLs im Unique-Index als gleich behandelt und sonst genau **einen** Maschinenzugang pro Datenbank
zuliesse. **Das gilt nur fuer handgeschriebenes SQL.**

Die Probe-Migration hat es widerlegt: der SQL-Server-Provider haengt an einen Unique-Index ueber nullable
Spalten von selbst einen Filter an -

```csharp
name: "UQ_TUserPerApp", columns: new[] { "TenantUserId", "ClientAppId" },
unique: true, filter: "[TenantUserId] IS NOT NULL"
```

Im Modell bleibt der Index deshalb ein schlichtes `[Index]`-Attribut **ohne** Filter; PostgreSQL zaehlt
NULLs ohnehin als verschieden. Genau dieser Automatismus musste bei den `WorkflowDefinitions` mit
`HasFilter(null)` *abgeschaltet* werden (siehe `workflow_definition_key`) - dort war er unerwuenscht, hier
ist er genau richtig.

**Im handgeschriebenen SQL von Leitfaden-§65 steht der Filter dagegen ausdruecklich** - dort gibt es
keinen Provider, der ihn ergaenzt.

### Warum `ClientKey` systemweit eindeutig bleibt

Beim Anmelden gibt es **noch keinen Mandantenkontext**: das Geraet legt seinen Schluessel vor, und
daraus muss der Mandant erst gefunden werden. Ein pro-Mandant eindeutiger Schluessel wuerde genau dort
nicht funktionieren.

---

## 4. Der Leseweg

`DbSecurityRepository` (flach + Tree), je ~8 Stellen:

- **Maschinenfall** (`TenantUserId IS NULL`): Rechte **direkt** aus den Sets der App. Kein
  Benutzer-Join, kein Schnitt. Der Mandant kommt aus `ClientApp.TenantId`.
- **Delegationsfall** (`TenantUserId` gesetzt): wie heute - Rollen des Benutzers, gedeckelt durch die
  Sets der App.
- **Fehler (a) reparieren**: der Deckel wird ueber die **gefilterten** Zugaenge gebildet, nicht ueber
  alle des Mandanten.

Das Label-Format (`##APPUSER##<Label>#`, `Global.AppUserKeyPattern`) bleibt unveraendert - die
UserMapper muessen nicht angefasst werden.

---

## 5. Die Anmeldung

**Schluessel am Geraet:** `<ClientKey>.<Label>.<Geheimnis>`

- `ClientKey` - findet die App (indiziert, systemweit eindeutig) und damit den Mandanten
- `Label` - findet den Zugang (indiziert, systemweit eindeutig)
- Geheimnis - gegen `SecretHash` geprueft

**Neu: eine Hash-Konvention.** Das Toolkit hat heute keine. `PasswordSecurity` ist **reversible
Verschluesselung**, der einzige echte Hash-Helfer (`HashHelper`) faellt auf SHA1 zurueck und ist fuer
Geheimnisse untauglich. Also: PBKDF2-SHA256 im Hausformat von `AesEncryptor` (Salt im Wert
mitgefuehrt, Base64), mit **Versionspraefix**, damit das Verfahren spaeter wechselbar bleibt.

**`ClientAppApiKeyResolver : IGetApiKeyQuery`** - loest den Schluessel auf, prueft Hash, `Enabled`,
`ExpiresUtc`, `RevokedUtc`, setzt `LastUsedUtc`, und gibt das **`Label`** als `ApiKeyInfo.Key` zurueck.
Damit greift der bereits fertige Leseweg - ohne eine einzige neue Tabelle fuer den Schluessel selbst.

**Mandantenkontext.** `ApiKeyAuthenticationHandler` setzt heute **nur** `ClaimTypes.Name` - ein per
API-Schluessel angemeldetes Geraet hat keinen Mandanten. Das muss dazu (`FixedUserScope` aus
`ClientApp.TenantId`), sonst laeuft das Geraet mandantenlos.

**Die Registrierungsfalle:** `UseDefaultApiKeyResolver()` wird **unbedingt** aus `WebPartInit.cs:190`
gerufen, sobald ein Host API-Key-Auth konfiguriert - per `AddTransient`, nicht `TryAdd`. Wer einen
eigenen Resolver registriert, gewinnt nur, wenn er **nach** der WebPart-Konfiguration registriert. Das
gehoert dokumentiert und der neue Resolver bekommt eine eigene `UseClientAppApiKeyResolver()`.

---

## 6. Die Kopplung

```
Agent                        Anwendung                    Benutzer (angemeldet)
  │ 1. StartAsync(clientKey, label)  │                              │
  │─────────────────────────────────>│                              │
  │   deviceCode (geheim), userCode  │                              │
  │   "K7M4-9QPZ", Ablauf, Intervall │                              │
  │<─────────────────────────────────│                              │
  │                                  │ 2. Code eingeben, Sets waehlen│
  │                                  │<─────────────────────────────│
  │                                  │ → ClientAppAccess + Geheimnis │
  │ 3. PollAsync(deviceCode)         │                              │
  │─────────────────────────────────>│                              │
  │   Geheimnis - EINMALIG           │                              │
  │<─────────────────────────────────│                              │
```

`IDevicePairingService` mit `StartAsync` / `ConfirmAsync` / `PollAsync` / `RevokeAsync`,
`MapDevicePairingEndpoints()` im Stil der uebrigen Endpunkt-Erweiterungen: `AllowAnonymous` auf
`start`/`poll` (das Geraet hat noch keine Identitaet), Rechtepruefung auf `confirm`.

**Festlegungen:**

1. **`userCode` meidet verwechselbare Zeichen** - kein `0`/`O`, kein `1`/`I`/`l`. Er wird vorgelesen
   und abgetippt, oft nebenbei.
2. **`deviceCode` ist das Geheimnis, `userCode` nicht.** In der Tabelle steht nur der **Hash** des
   `deviceCode`; der `userCode` darf auf einem Bildschirm im Laden stehen.
3. **Ablauf ist Pflicht**, Vorgabe 10 Minuten, konfigurierbar. Ein ewig offener Kopplungsvorgang ist
   ein dauerhaft gueltiger Einstieg.
4. **Das Geheimnis wird genau einmal ausgeliefert** (`SecretDeliveredUtc`), danach nur noch der
   Zustand.
5. **`PollAsync` braucht eine Bremse** - Mindestintervall in der Antwort plus serverseitige Begrenzung
   je `deviceCode` (`PollCount`, `LastPollUtc`).
6. **Bestaetigen verlangt ein Recht**, und die Maske **zeigt die Rechtebuendel**, die sie gerade
   erteilt. Ein Knopf, hinter dem niemand weiss, was er freigibt, ist keine Zustimmung.

---

## 7. Verwaltung

| Maske | Zustand |
|---|---|
| `ClientAppTemplate` + seine `AppPermissionSet`s | **umbauen** - die Sets werden kuenftig INNERHALB des Templates gepflegt (heute freischwebend, `PermissionSetAdminHandler` / Telerik `PermissionSetController`) |
| `ClientApp` je Mandant | **neu** - ohne sie kann niemand eine App anlegen |
| `ClientAppAccess` (Zugaenge/Geraete) | **neu** - Liste, Widerruf, Ablauf, `LastUsedUtc` |
| Kopplungs-Bestaetigung | **neu** - Code-Eingabe + Buendel-Auswahl + Rechteanzeige |

---

## 8. Settings-Exchange (Config-Export/Import)

Heute **komplett abwesend**: `SysConfigurationHandler` (3111 Zeilen) fuehrt die ClientApp-Typen
ausschliesslich als Generic-Parameter, es gibt keinen einzigen DbSet-Zugriff.

Aufzunehmen:

- **Global:** `ClientAppTemplate` samt seinen `AppPermissionSet`s und `AppPermission`s.
- **Je Mandant:** `ClientApp` samt `ClientAppPermission` (die gewaehlten Buendel).

**Nicht exportieren:** `ClientAppAccess`. Ein Zugang ist an ein konkretes Geraet gebunden, und sein
`SecretHash` hat in einer anderen Umgebung nichts zu suchen. Ebensowenig `DevicePairing` - das sind
fluechtige Zwischenzustaende. Ein Geraet wird in der Zielumgebung neu gekoppelt; das ist der Punkt des
Verfahrens.

Zu beachten: `config_export_no_streaming_reads` (`ToList()` statt `AsEnumerable()`) und
`config_apply_value_conversion` (invariant + UTC).

---

## 9. Mandanten-Vorlage (Tenant-Template)

Heute ebenfalls abwesend: `TenantTemplateHelperBase` nennt `ClientAppTemplate` nur als
Generic-Parameter.

Aufzunehmen: **`ClientApp` je Mandant** - eine neue Mandanten-Instanz bekommt die Apps, die die Vorlage
vorsieht, samt ihrer Buendel-Auswahl. Mit den bestehenden **Apply-Modes je Art** (Auto / Additiv /
Erzwungen, siehe `tenant_template_apply_modes`).

`ClientKey` muss dabei **je Mandant neu erzeugt** werden - er ist systemweit eindeutig, eine Kopie
waere ein Konflikt. Das ist der Punkt, an dem eine naive Kopie scheitert.

`ClientAppAccess` gehoert auch hier **nicht** hinein.

---

## 10. Phasen

| # | Inhalt | Abhaengt von |
|---|---|---|
| 1 | **ERLEDIGT** - Modell + Schema + SQL-Abschnitt im Leitfaden (§65) | - |
| 2 | **ERLEDIGT** - Leseweg `DbSecurityRepository` (Maschinenzweig + drei Bestandsfehler) | 1 |
| 3 | Hash-Konvention + `ClientAppApiKeyResolver` + Mandantenkontext | 1, 2 |
| 4 | `IDevicePairingService` + Endpunkte | 1, 3 |
| 5 | Verwaltungsmasken | 1, 4 |
| 6 | Settings-Exchange | 1 |
| 7 | Mandanten-Vorlage | 1, 6 |
| 8 | Abschnitt in `docs/Migration-Future_10-MLM.md` | alle |

**Breaking:** Umbenennung `ClientAppUser` → `ClientAppAccess`, Wegfall von
`ClientAppTemplatePermission`, `AppPermissionSet` jetzt pflichtig an einem Template, `ClientApp` jetzt
pflichtig an Mandant und Template. Da bisher **niemand** in diese Tabellen schreibt, gibt es keinen
Altbestand ausser von Hand angelegtem - das ist der guenstigste Moment fuer diesen Schnitt.
