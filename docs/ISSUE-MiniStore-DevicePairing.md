# Issue: Geräte-Kopplung (Device-Code-Fluss) als Toolkit-Baustein

**Status:** OFFEN — Anfrage an das Toolkit
**Datum:** 2026-09-15
**Quelle:** MiniStore-Session (Konsument). MiniStore koppelt Kassenterminals: auf jedem Laden-PC läuft
ein Agent, der seinen Gerätedienst am ServiceHub anmeldet und sich dafür ausweisen muss.
**Toolkit-Stand:** `5.0.0-PRE239`.
**Hängt zusammen mit:** `ISSUE-MiniStore-ApiKey-HashedResolver.md` (der Schlüssel, den die Kopplung
ausgibt) und `ISSUE-MiniStore-JwtAuthInit-Unimplemented.md` (die Alternative über JWT).

## Warum das ins Toolkit gehört

Der Weg, wie sich eine **Maschine** an einer Toolkit-Anwendung anmeldet, ist zur Hälfte vorhanden und
zur Hälfte gar nicht:

| Baustein | Zustand |
|---|---|
| `ApiKeyAuthInit` (Client) → `X-Api-Key` | fertig |
| `ApiKeyAuthenticationHandler` (Server) | fertig |
| `IGetApiKeyQuery` als Erweiterungspunkt | fertig |
| `ClientApp` mit `ClientKey`/`ClientSecret` | fertig, aber ohne Anmeldeweg |
| `JwtAuthInit` | Attrappe (eigenes Issue) |
| **Wie der Schlüssel auf das Gerät kommt** | **fehlt vollständig** |

Genau dieses letzte Stück erfindet sonst jeder Konsument neu — und zwar jedes Mal mit denselben
Fallen: Wie lang ist der Code, wie lange gilt er, was passiert beim Nachdrücken, wo wird der Schlüssel
gespeichert, wie widerruft man ihn. Es ist ausserdem der einzige Teil, bei dem ein Fehler *still* ist:
ein zu schwacher Code oder ein nicht ablaufender Kopplungsvorgang fällt im Betrieb nie auf.

## Der Ablauf

Der übliche Device-Code-Fluss (RFC 8628 in Geist, nicht im Buchstaben — es geht nicht um OAuth):

```
Agent                                Anwendung                          Benutzer (angemeldet)
  │                                      │                                      │
  │ 1. StartAsync()                      │                                      │
  │─────────────────────────────────────>│                                      │
  │    deviceCode (geheim)               │                                      │
  │    userCode   "K7M4-9QPZ" (sichtbar) │                                      │
  │    Ablauf, Poll-Intervall            │                                      │
  │<─────────────────────────────────────│                                      │
  │                                      │  2. Maske: Code eingeben             │
  │                                      │<─────────────────────────────────────│
  │                                      │     Ziel wählen, bestätigen          │
  │                                      │  → technischer Benutzer + Schlüssel  │
  │ 3. PollAsync(deviceCode)             │                                      │
  │─────────────────────────────────────>│                                      │
  │    Schlüssel — EINMALIG im Klartext  │                                      │
  │<─────────────────────────────────────│                                      │
```

**Der Benutzer tippt acht Zeichen.** Das ist der ganze Punkt: kein Schlüssel zum Abschreiben, kein
Passwort in einer Konfigurationsdatei, keine Datei, die per USB-Stick wandert.

## Vorschlag

### Vertrag

```csharp
public interface IDevicePairingService
{
    /// <summary>Called by the device. Creates a pending pairing and returns the codes.</summary>
    Task<PairingRequest> StartAsync(string deviceLabel, CancellationToken ct = default);

    /// <summary>
    /// Called by a signed-in user who confirms the pairing. <paramref name="target"/> is what the
    /// host binds the device to (a terminal id, a machine name — the toolkit does not interpret it).
    /// </summary>
    Task<PairingConfirmation> ConfirmAsync(string userCode, string target, string roleName,
                                           CancellationToken ct = default);

    /// <summary>Called by the device while it waits. Returns the secret exactly ONCE.</summary>
    Task<PairingResult> PollAsync(string deviceCode, CancellationToken ct = default);

    /// <summary>Revokes a key. The device stops working on its next call.</summary>
    Task RevokeAsync(string target, CancellationToken ct = default);
}
```

`PairingResult` trägt einen Zustand (`Pending`, `Confirmed`, `Expired`, `Denied`) und im Fall
`Confirmed` den Schlüssel — **einmalig**; jeder weitere Aufruf liefert ihn nicht mehr.

### Datenmodell

Zwei Tabellen im Sicherheitsschema:

- **`DevicePairings`** — `DeviceCodeHash`, `UserCode`, `CreatedUtc`, `ExpiresUtc`, `State`,
  `ConfirmedByUserId`, `Target`, `RoleName`, `SecretDeliveredUtc`.
- **`DeviceKeys`** — `KeyHash`, `UserName` (der technische Benutzer), `Target`, `CreatedUtc`,
  `RevokedUtc`.

Die zweite bedient zugleich den gehashten `IGetApiKeyQuery` aus dem verwandten Issue — beide Anfragen
treffen sich also an derselben Tabelle.

### Was das Toolkit entscheiden sollte, weil es sonst jeder falsch macht

1. **Der `userCode` meidet verwechselbare Zeichen.** Kein `0`/`O`, kein `1`/`I`/`l`. Er wird
   vorgelesen und abgetippt, oft von einer Person, die nebenbei Kunden bedient.
2. **Der `deviceCode` ist das Geheimnis, der `userCode` nicht.** Der `userCode` darf auf einem
   Bildschirm im Laden stehen; der `deviceCode` verlässt den Agenten nie. Deshalb steht in der
   Tabelle nur sein **Hash**.
3. **Ablauf ist Pflicht, nicht Option** — ein Kopplungsvorgang, der ewig offen bleibt, ist ein
   dauerhaft gültiger Einstieg. Vorschlag: 10 Minuten, konfigurierbar.
4. **Der Schlüssel wird genau einmal ausgeliefert.** `SecretDeliveredUtc` setzen und danach nur noch
   den Zustand melden. Sonst holt ihn ein zweiter Mitleser später nochmals ab.
5. **Das Abfragen (`PollAsync`) braucht eine Bremse.** Ohne sie ist der achtstellige `userCode`
   ratbar. Ein Mindestintervall in der Antwort plus serverseitige Begrenzung je `deviceCode`.
6. **Bestätigen verlangt ein Recht.** Wer koppeln darf, ist eine Entscheidung des Hosts — der
   Vertrag sollte einen Rechtenamen annehmen oder das Gate dem Aufrufer überlassen.

### Was hostspezifisch bleibt

Das Toolkit soll **nicht** wissen, *was* gekoppelt wird. `target` ist eine undurchsichtige
Zeichenkette; bei MiniStore ist es die Id eines Kassenterminals aus einem Fachkontext, den das
Sicherheitsschema nicht kennt und nicht kennen soll. Ebenso bleibt die Maske beim Host — auch wenn
eine mitgelieferte Blazor-Komponente (Code-Eingabe + Zielauswahl) willkommen wäre, so wie es
`<ShareButton />` für Freigaben gibt.

### Endpunkte

`MapDevicePairingEndpoints()` im Stil der übrigen Endpunkt-Erweiterungen, mit `AllowAnonymous` für
`start`/`poll` (das Gerät hat ja noch keine Identität) und Rechteprüfung auf `confirm`.

## Was wir bis dahin tun

Das entscheidet sich nach eurer Antwort: Ist der Baustein absehbar, warten wir und bauen 6b-2 mit
einem von Hand hinterlegten Schlüssel durch — der Kanal lässt sich damit vollständig prüfen. Kommt er
nicht bald, bauen wir eine schlanke Fassung in MiniStore und ersetzen sie später.

**Priorität von unserer Seite: keine** — aber Interesse, denn es ist der einzige noch fehlende Teil
zwischen Agent und Hub.
