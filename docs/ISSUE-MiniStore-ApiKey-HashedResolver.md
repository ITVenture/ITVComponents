# Issue: Der mitgelieferte API-Schlüssel-Resolver vergleicht Klartext — ein gehashter fehlt

**Status:** OFFEN — Anfrage an das Toolkit
**Datum:** 2026-09-15
**Quelle:** MiniStore-Session (Konsument). MiniStore koppelt Kassenterminals: eine Anwendung auf dem
Laden-PC meldet ihren Gerätedienst am ServiceHub an und weist sich dabei mit einem API-Schlüssel aus
(`ApiKeyAuthInit` → `X-Api-Key` → `ApiKeyAuthenticationHandler`).
**Toolkit-Stand:** `5.0.0-PRE239`. Alle Aussagen unten sind am Quelltext verifiziert.

## Der Fall

Der Weg funktioniert und ist an beiden Enden fertig — Client (`ApiKeyAuthInit` in
`…InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Client`) und Server
(`…WebCoreToolkit.Authentication.ApiKey.ApiKeyAuthenticationHandler`). **Das hier ist kein Defekt,
sondern eine fehlende Ausbaustufe.**

Der einzige mitgelieferte `IGetApiKeyQuery` vergleicht den Schlüssel **im Klartext** gegen einen
Benutzernamen:

```csharp
// …Authentication/ApiKey/DefaultApiKeyUserResolver.cs
public Task<ApiKeyInfo> Execute(string providedApiKey, string authenticationScheme)
{
    var apiKeyUser = securityRepository.Users.FirstOrDefault(
        n => n.UserName.Equals(providedApiKey, StringComparison.OrdinalIgnoreCase)
          && n.AuthenticationType == authenticationScheme);
    …
}
```

Der Schlüssel steht damit als `UserName` in der Datenbank. Für einen API-Schlüssel ist das die
gleiche Klasse von Angriffsfläche wie ein Klartextpasswort: Wer die Benutzertabelle lesen kann —
Datenbanksicherung, Auskunftsabfrage, ein zu weit gefasster Administrationszugang — kann sich als
dieses Gerät ausgeben. Anders als bei einem Passwort fällt es zudem nicht auf, weil niemand ihn je
tippt.

## Dass es überhaupt anschliessbar ist, steht nirgends

`IGetApiKeyQuery` ist der richtige Erweiterungspunkt, aber der entscheidende Zusammenhang ist
undokumentiert: **Der Handler nimmt `ApiKeyInfo.Key` als Benutzernamen** —

```csharp
// …Authentication/ApiKey/ApiKeyAuthenticationHandler.cs
var existingApiKey = await getApiKeyQuery.Execute(providedApiKey, Options.Scheme);
if (existingApiKey != null)
{
    var claims = new List<Claim>
    {
        new Claim(System.Security.Claims.ClaimTypes.Name, existingApiKey.Key)
    };
    …
}
```

— und nicht etwa den *gelieferten* Schlüssel. Genau darauf beruht die Lösung: Ein eigener Resolver
hasht den gelieferten Schlüssel, schlägt ihn nach und gibt den **gefundenen Benutzernamen** in
`ApiKeyInfo.Key` zurück. Der Klartext steht dann nirgends.

Dass `ApiKeyInfo.Key` diese Doppelrolle trägt (Eingabe beim Default-Resolver, Benutzername beim
Handler), ist dem Namen nicht anzusehen. Wir haben es nur gefunden, weil wir den Handler gelesen
haben; wer der Signatur vertraut, baut hier einen Resolver, der den Klartext durchreicht.

## Vorschlag

1. **Einen gehashten Resolver mitliefern**, etwa `HashedApiKeyUserResolver`, zusammen mit einer
   Registrierung `UseHashedApiKeyResolver()` neben dem bestehenden `UseDefaultApiKeyResolver()`.
   Welches Verfahren (PBKDF2, das ohnehin im Umfeld benutzte `PasswordSecurity`) entscheidet das
   Toolkit — von aussen zählt, dass der Klartext nicht gespeichert wird.
2. **Die Doppelrolle von `ApiKeyInfo.Key` dokumentieren** — ein Satz am XML-Kommentar des Feldes und
   an `IGetApiKeyQuery.Execute` genügt: *„der zurückgegebene Key wird als `ClaimTypes.Name` gesetzt;
   ein Resolver darf hier einen anderen Wert liefern als den übergebenen"*. Das ist der Hinweis, der
   uns eine Stunde gespart hätte.
3. Optional, aber naheliegend: `ApiKeyInfo` hat heute nur `Key` und `Created`. Ein **Ablaufdatum**
   und ein **Widerrufsvermerk** wären an dieser Stelle natürlicher als in jedem Konsumenten
   nachgebaut — `Created` allein lässt beides offen.

## Was wir bis dahin tun

MiniStore implementiert `IGetApiKeyQuery` selbst: eine eigene Tabelle je Kassenterminal mit dem
**Hash** des Schlüssels, dem zugehörigen technischen Benutzer und einem Widerrufsdatum. Der Resolver
hasht den gelieferten Schlüssel, schlägt ihn dort nach und gibt den Benutzernamen zurück.

Das ist wenig Code, und der Erweiterungspunkt trägt ihn sauber — insofern **keine Dringlichkeit**.
Die Anfrage stellen wir, weil der Klartextvergleich als *Vorgabe* des Toolkits vermutlich nicht
gewollt ist: Ein Konsument, der `UseDefaultApiKeyResolver()` aufruft, trifft damit eine
Sicherheitsentscheidung, ohne es zu merken.
