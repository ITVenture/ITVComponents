# Security 01: `AesEncryptor` – Format Version 2 (authentifiziert, zeitgemässe Schlüsselableitung)

**Typ:** Härtung / Format-Erweiterung (zur Prüfung)

**Betrifft:**
- `ITVComponents\Security\AesEncryptor.cs`, `CryptDecoratorStream.cs`, `PasswordSecurity.cs`
- Aufrufer:
  - `ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity\Shared\Extensions\CipherExtensions.cs` (10 Stellen,
    auch Streams und getrennt gespeicherte IV/Salt)
  - `ITVComponents\Json\Converters\JsonStringEncryptConverter.cs` (`encrypt:`-Werte in JSON)
  - `ITVComponents.Formatting\CustomFormat\Impl\StringDecryptor.cs`
  - `PlugInAdminHandler.cs` (MudBlazor AdminViews), `PlugInConstController.cs` (Telerik AdminViews)
  - `ITVComponents.NewtonsoftJson\CustomConverters\JsonStringEncryptConverter.cs`

**Anlass:** Bereinigung der Obsolete-Warnungen (`AesManaged`, `Rfc2898DeriveBytes`). Die Parameter wurden dabei
bewusst unverändert übernommen, damit bestehende Daten lesbar bleiben. Die Frage, ob das Verfahren selbst noch
zeitgemäss ist, gehört in ein eigenes Issue.

## Ist-Zustand

- **Verschlüsselung:** AES-256-CBC, PKCS7, zufälliger IV (16 Bytes).
- **Schlüssel:** PBKDF2 mit HMAC-SHA1, **10'000 Iterationen**, 32 Bytes, zufälliges Salt (32 Bytes). Die
  „Passphrase" ist `Encoding.UTF32` des Passworts bzw. der Entropie aus `PasswordSecurity.InitializeAes`.
- **Kein Integritätsschutz:** Weder MAC noch AEAD-Tag.
- **Formate:**
  - **Blob** (`Encrypt(byte[], …)`, Base64 bei Strings): `[Salt-Länge (1)] [IV-Länge (1)] [Salt] [IV] [Ciphertext]`.
    Es gibt keine Versionskennung, das erste Byte ist in allen bestehenden Daten `32`.
  - **Stream** (`GetEncryptStream(stream, entropy)`): Kopf mit `MagicNumber (Int32)`, IV-Länge, Salt-Länge, IV, Salt.
    Eine neue Magic Number kann eine neue Version kennzeichnen.
  - **Getrennt** (`Encrypt(…, out iv, out salt)`, `GetEncryptStream(…, out iv, out salt)`): Der Aufrufer speichert IV
    und Salt selbst, z.B. in eigenen Spalten. Es gibt kein Format und keine Stelle für eine Version oder ein Tag.

## Bewertung

| Punkt | Risiko | Begründung |
|---|---|---|
| SHA-1 in PBKDF2 | gering | Die Kollisionsangriffe auf SHA-1 betreffen HMAC/PBKDF2 nicht. HMAC-SHA1 gilt als PRF weiterhin als sicher. NIST will SHA-1 bis 2030 aber ganz auslaufen lassen. |
| 10'000 Iterationen | mittel, abhängig von der Passphrase | OWASP empfiehlt heute ca. 1,3 Mio. (PBKDF2-HMAC-SHA1) bzw. 600'000 (PBKDF2-HMAC-SHA256). Bei einer kurzen oder erratbaren Passphrase ist das Durchprobieren nach einem Datenabfluss günstig. Bei langer, zufälliger Passphrase nicht relevant. |
| CBC ohne Authentifizierung | mittel bis hoch, je nach Einsatz | Veränderte Ciphertexte fallen nicht auf (Bit-Flipping im ersten Block via IV). Wo ein Angreifer Ciphertexte einschleusen und das Gelingen der Entschlüsselung beobachten kann (Fehlermeldung, Laufzeit), ist ein Padding-Oracle-Angriff möglich. |
| Schlüsselableitung bei jedem Aufruf | Leistung | 10'000 Runden pro `Encrypt`/`Decrypt`. Bei höheren Iterationszahlen wird das spürbar und spricht für einen Schlüssel-Cache je (Passphrase, Salt) oder ein Schema mit einmal abgeleitetem Hauptschlüssel. |

## Vorschlag

1. **Version 2 für Blob und Stream**
   - **Blob:** Das erste Byte `0x00` kennzeichnet ein versioniertes Format, denn eine Salt-Länge 0 kommt in Version 1
     nie vor. Danach folgen eine Versionsnummer, Parameter-ID, Salt, Nonce, Ciphertext und Tag.
   - **Verfahren:** AES-256-GCM (`AesGcm`). Die Schlüsselableitung ist PBKDF2-HMAC-SHA256 mit einer Iterationszahl,
     die im Wert mitgeführt wird. So lässt sie sich später erhöhen, ohne das Format zu ändern.
   - **Stream:** `AesGcm` arbeitet nur in einem Durchgang. Für Streams braucht es entweder segmentiertes GCM (Blöcke
     mit eigenem Tag und Zähler-Nonce) oder Encrypt-then-MAC (CBC + HMAC-SHA256 am Ende). Siehe Entscheid 2.
2. **Lesen:** Version 1 wird weiter gelesen. **Schreiben:** nur noch Version 2.
3. **Umstellung bestehender Werte:** beim nächsten Schreiben (lazy). Optional kommt ein Werkzeug dazu, das die
   bekannten Stellen (TenantSecurity-Tabellen, Plugin-Konstanten, Einstellungen) einmalig umschreibt.
4. **Getrennte IV/Salt-Varianten:** Für sie braucht es einen eigenen Weg, siehe Entscheid 3.

## Entscheide, die vor der Umsetzung fallen müssen

1. **Iterationszahl und Kosten:** Wie viele Runden, und braucht es einen Cache für abgeleitete Schlüssel? Der Cache
   wäre pro Prozess, mit Schlüssel aus Passphrase-Hash und Salt.
2. **Streams:** segmentiertes GCM oder CBC + HMAC?
3. **Getrennte IV/Salt-Varianten** (`out iv, out salt`): Tag und Version im Ciphertext mitführen (Signatur bleibt,
   Inhalt ändert sich)? Oder die Varianten auslaufen lassen (`[Obsolete]`) zugunsten des Blob-Formats? Zuerst ist zu
   prüfen, welche Konsumenten sie nutzen und was dort gespeichert ist.
4. **Passphrase-Kodierung:** UTF-32 der Passphrase bleibt für Version 1 nötig. Für Version 2 böte sich UTF-8 an.
   Das ist wirkungslos für die Sicherheit, aber üblich.
5. **Fehlerverhalten:** Eine fehlgeschlagene Tag-Prüfung muss laut scheitern (Exception), mit einheitlicher Meldung
   ohne Unterscheidung „falsches Passwort" oder „manipuliert". So gibt es kein Orakel.

## Tests (Vorschlag)

- Version 2: Hin- und Rückweg für Blob, String (Base64) und Stream.
- Bestehende Version-1-Werte (feste Testvektoren aus dem heutigen Code) werden weiter gelesen.
- Ein manipulierter Ciphertext, ein manipulierter Tag und ein manipulierter Kopf ergeben jeweils eine Exception.
- Die Iterationszahl aus dem Wert wird benutzt, nicht die aktuelle Vorgabe.
- Ein falsches Passwort ergibt dieselbe Exception wie eine Manipulation.
