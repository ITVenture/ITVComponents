# Issue: `JwtAuthInit` ist eine Attrappe — der Bearer-Anmeldefluss des ServiceHub fehlt

**Status:** ERLEDIGT — umgesetzt im Toolkit (siehe „Auflösung" am Ende)
**Datum:** 2026-09-15
**Quelle:** MiniStore-Session (Konsument). MiniStore baut den POS-Agenten: eine Anwendung auf dem Laden-PC,
die ihren Gerätedienst am ServiceHub anmeldet und dafür einen Bearer braucht.
**Toolkit-Stand:** `5.0.0-PRE239`. Alle Aussagen unten sind am Quelltext verifiziert.
**Vom Betreuer bestätigt:** liegengeblieben, nicht absichtlich leer.

## Der Fall

`JwtAuthInit` sieht nach dem fertigen Anmeldeweg für einen Hub-Client aus — die eine Hälfte ist es auch.
`ConfigureCallOptions` setzt den Header korrekt und an der richtigen Stelle:

```csharp
// …InterProcessExtensions.JwtAuth/JwtAuthInit.cs
public override CallOptions ConfigureCallOptions(CallOptions optionsRaw)
{
    var retVal = base.ConfigureCallOptions(optionsRaw);
    var ent = new Metadata.Entry("Authorization", $"Bearer {GetCurrentBearer()}");
    …
}
```

**Die andere Hälfte gibt es nicht:**

```csharp
private string GetCurrentBearer()
{
    return "";
}

public void Initialize()
{
    Initialized = true;
}
```

Jeder Aufruf geht also mit `Authorization: Bearer ` hinaus — mit leerem Token. Am `AuthServiceHubRpc`
(das ein `[Authorize]` trägt) endet das als Zurückweisung, und die Meldung spricht von fehlenden
Rechten, nicht von einem fehlenden Token.

**Die Konfiguration ist ebenso leer.** `JwtAuthConfig` trägt genau ein Feld:

```csharp
public class JwtAuthConfig
{
    public string Name { get; set; }
}
```

Es gibt also keine Stelle, an der ein Konsument hinterlegen könnte, *wo* ein Token zu holen ist und
*womit*. Der Name allein genügt, um eine Konfiguration zu finden — nicht, um sie zu benutzen.

## Betroffene Stellen

| Datei | Was dort steht |
|---|---|
| `…InterProcessExtensions.JwtAuth/JwtAuthInit.cs` | `GetCurrentBearer()` → `return "";`, `Initialize()` setzt nur ein Flag |
| `…InterProcessExtensions.JwtAuth/Config/JwtAuthConfig.cs` | nur `Name` |
| `…GRpc/Hub/DefaultConfigurators/Client/CollectableClientInit.cs` | die Basis ist vollständig — an ihr liegt es nicht |

Der Plan von MiniStore hatte diesen Baustein als tragend eingeplant („der Bearer-Anmeldefluss, den das
Ausgangsdokument für die lokale Anwendung verlangt, ist der Anmeldefluss dieses Kanals — beides fällt
zusammen"). Beim Bauen zeigt sich: die Zusammenlegung stimmt als Entwurf, nur ist sie nicht umgesetzt.

## Vorschlag

**`JwtAuthConfig` um das erweitern, was ein Tokenbezug braucht** — die Felder sind die übliche Menge:

```csharp
public class JwtAuthConfig
{
    public string Name { get; set; }

    /// <summary>Absolute URL of the token endpoint.</summary>
    public string TokenEndpoint { get; set; }

    /// <summary>Client credentials used to obtain the token.</summary>
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }

    /// <summary>Optional scope / audience passed to the endpoint.</summary>
    public string Scope { get; set; }

    /// <summary>How long before expiry the token is renewed. Defaults to a sane value (e.g. 60 s).</summary>
    public TimeSpan RenewBefore { get; set; }
}
```

**`JwtAuthInit` holt und hält das Token:**

- `Initialize()` besorgt das erste Token (die Klasse meldet bereits `ForceImmediateInitialization = true`
  — offensichtlich war genau das vorgesehen).
- `GetCurrentBearer()` gibt das zwischengespeicherte Token zurück und erneuert es, wenn es innerhalb von
  `RenewBefore` abläuft. **Threadsicher**: an einem Hub-Client hängen mehrere gleichzeitige Aufrufe, und
  ein Erneuerungssturm ist genau der Fehler, den man dann sucht.
- Schlägt der Bezug fehl, gehört das **protokolliert** und nicht in ein leeres Token verwandelt — heute
  ist ein fehlendes Token von einem abgelaufenen nicht zu unterscheiden.

**Ein Erweiterungspunkt wäre uns mehr wert als ein fester Fluss.** Nicht jeder Konsument holt sein Token
per `client_credentials` von einem OAuth-Endpunkt; MiniStore etwa koppelt seine Kassen über einen
Device-Code-artigen Ablauf und bekommt das Token aus einem eigenen Endpunkt. Wenn `JwtAuthInit` eine
überschreibbare Methode oder eine einsetzbare Abstraktion (`ITokenSource` o. ä.) böte, liesse sich das
ohne Kopie der ganzen Klasse anschliessen — und der mitgelieferte `client_credentials`-Weg bliebe der
Normalfall.

## Was wir bis dahin tun

MiniStore implementiert `IHubClientConfigurator` selbst (die Basis `CollectableClientInit` ist
vollständig, der Aufwand hält sich also in Grenzen) und hängt dort sein eigenes Token an. Die Kopie
verschwindet, sobald `JwtAuthInit` ein Token besorgt — dann bleibt bei uns nur die Token-Quelle.

**Keine Dringlichkeit von unserer Seite**, aber ein Hinweis: In diesem Zustand ist die Klasse eine Falle.
Sie ist vollständig genug, dass ein Konsument sie für fertig hält — der Header wird ja gesetzt —, und der
Fehler zeigt sich erst am Hub als Rechteproblem. Ein `NotImplementedException` in `GetCurrentBearer` oder
ein Vermerk im XML-Kommentar würde das abfangen, auch wenn die Umsetzung noch wartet.

---

## Auflösung (Toolkit)

Umgesetzt, mit **einer Erweiterung gegenüber der Anfrage** — und der Nebenvorschlag ist zum Hauptweg
geworden.

### Der Befund war noch schlechter als beschrieben

Das Issue behandelt den Token-Endpunkt als Sache des Konsumenten. Tatsächlich **lieferte das Toolkit
auch keinen**: die einzige Token-Route war `/UserToken/Refresh`, also „ein Benutzer erneuert sein
Token". Eine Maschine hatte keinen Weg, überhaupt an ein erstes zu kommen.

Neu ist deshalb `POST /ClientAppToken` — es nimmt denselben Schlüssel entgegen, den die Gerätekopplung
ausgibt (`<ClientKey>.<Label>.<Geheimnis>`, siehe Leitfaden §65), und gibt ein JWT samt Ablauf zurück.
Das Token trägt Bezeichner und Mandant, damit dieselbe Rechteauflösung greift wie beim API-Schlüssel.

### Der Erweiterungspunkt statt eines festen Flusses

Euer Nebenvorschlag war der richtige Hauptvorschlag: `ITokenSource` mit einer Methode, dem `JwtAuthInit`
als Plugin in den Konstruktor gereicht. Der mitgelieferte `ApiKeyTokenSource` bleibt der Normalfall; euer
kopplungsbasierter Ablauf hängt sich als eigene Quelle an, ohne die Klasse zu kopieren.

`JwtAuthConfig` trägt jetzt `TokenEndpoint`, `ApiKey`, `RenewBeforeSeconds` und `TimeoutSeconds` — nur
für den mitgelieferten Weg; wer eine eigene Quelle mitgibt, lässt sie leer.

### Was `JwtAuthInit` jetzt tut

Token halten, vor dem Ablauf erneuern, nebenläufigkeitssicher (doppelte Prüfung innerhalb der Sperre —
sonst erneuern beim Ablauf alle gleichzeitig). **Und es wirft, statt einen leeren Bearer zu schicken:**
beim Hochfahren, wenn kein erstes Token zu bekommen ist, und beim Aufruf, wenn keines mehr gilt.

Schlägt die Erneuerung fehl, behält es das alte Token, solange es noch gilt — sonst würde aus einer
vorübergehenden Störung sofort ein Ausfall.

### Drei stille Fehler, die dabei auffielen

1. **`JwtTokenService` warf.** Es setzte `new Claim(type, null)`, weil `GetApplicationUserLabel` immer
   `null` liefert. Der Weg war nicht bloss funktionslos, sondern kaputt, sobald ihn jemand benutzte.
2. **`VerifyRefreshToken` warf ebenfalls** — `First` statt `FirstOrDefault` — und verglich gegen `Label`,
   wo begrifflich der `ClientKey` gemeint war.
3. **`JwtAuthInit.Dispose()` verdeckte** `CollectableClientInit.Dispose()`; deren Aufräumen und das
   `Disposed`-Ereignis wären lautlos ausgefallen.

### Kein Refresh-Token für Maschinen

Bewusst nicht gebaut: ein Gerät hält ein langlebiges Geheimnis und holt sich jederzeit ein neues Token.
Ein Refresh-Token wäre ein zweites Geheimnis mit eigener Ablage, eigenem Widerruf und eigenem Ablauf —
alles doppelt, ohne etwas zu können, was der Geräteschlüssel nicht schon kann.

### Euer Hinweis zur Falle ist umgesetzt

Ihr habt geschrieben, die Klasse sei in diesem Zustand eine Falle und ein `NotImplementedException` oder
ein Vermerk würde das abfangen. Beides ist jetzt da, aber besser: die Klasse **funktioniert**, und die
Teile, die weiterhin nicht umgesetzt sind (die Benutzer-Delegation), sagen es im Protokoll, statt still
`null` zu liefern.

Details: Leitfaden-Abschnitt 66.
