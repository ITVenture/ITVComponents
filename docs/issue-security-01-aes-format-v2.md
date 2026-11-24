# Security 01: `AesEncryptor` – Format Version 2 (authentifiziert, zeitgemässe Schlüsselableitung)

**Typ:** Härtung / Format-Erweiterung
**Status:** Teil A und B umgesetzt (2026-11-24). Teil C (Schutz der Mandantenschlüssel) offen.

**Umsetzung (Teil A und B):**
- Neu `AesFormatV2` (Schlüsselableitung, Blob, einheitliche Exception), `GcmSegmentStream` (segmentiertes GCM).
- `AesEncryptor` schreibt Version 2 und liest beide. Neu sind `EncryptWithKey`, `GetEncryptStreamWithKey` (auch mit
  getrenntem IV/Salt) und `IsLegacyFormat`.
- `CipherExtensions`, `PlugInAdminHandler` und `PlugInConstController` verschlüsseln mit der Variante für
  Zufallsschlüssel.
- Lesen eines Version-1-Strings ergibt eine Warnung einmal pro Aufrufstelle. `CryptDecoratorStream` liest den Kopf
  vollständig (`ReadExactly`).
- Die Iterationszahl beim Lesen ist auf 10'000–10'000'000 begrenzt, damit ein gefälschter Wert keine endlose Rechnung
  auslöst.
- Tests in `ITVComponents.Tests/AesEncryptorTests.cs`. Version 1 wird darin über eine unabhängige
  Referenzimplementierung erzeugt.

**Betrifft:**
- `ITVComponents\Security\AesEncryptor.cs`, `CryptDecoratorStream.cs`, `PasswordSecurity.cs`
- Aufrufer:
  - `ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity\Shared\Extensions\CipherExtensions.cs`
    (Mandanten-Verschlüsselung, auch Streams und getrennt gespeicherte IV/Salt)
  - `ISecurityRepository.Encrypt/Decrypt/GetEncryptStream/GetDecryptStream` und Implementierungen
    (`SecurityRepository`, `AssetSecurityRepository`, `CookiePermissionRepo`)
  - `ITVComponents\Json\Converters\JsonStringEncryptConverter.cs` (`encrypt:`-Werte in JSON)
  - `ITVComponents.Formatting\CustomFormat\Impl\StringDecryptor.cs`
  - `PlugInAdminHandler.cs` (MudBlazor AdminViews), `PlugInConstController.cs` (Telerik AdminViews)
  - `ITVComponents.NewtonsoftJson\CustomConverters\JsonStringEncryptConverter.cs`

**Anlass:** Bereinigung der Obsolete-Warnungen (`AesManaged`, `Rfc2898DeriveBytes`). Die Parameter wurden dabei
bewusst unverändert übernommen, damit bestehende Daten lesbar bleiben. Ob das Verfahren selbst noch zeitgemäss ist,
wird hier geklärt.

## Ist-Zustand

- **Verschlüsselung:** AES-256-CBC, PKCS7, zufälliger IV (16 Bytes).
- **Schlüssel:** PBKDF2 mit HMAC-SHA1, **10'000 Iterationen**, 32 Bytes, zufälliges Salt (32 Bytes) pro Wert. Die
  Eingabe kommt immer schon als Bytes an:
  - `PasswordSecurity` wandelt die Passphrase per UTF-32 um.
  - `CipherExtensions.GetEncryptionKey` liefert den Mandantenschlüssel (`Tenant.TenantPassword`, Base64 von
    `AesEncryptor.CreateKey()`, also 32 Zufallsbytes).
- **Kein Integritätsschutz:** weder MAC noch AEAD-Tag.
- **Formate:**
  - **Blob** (`Encrypt(byte[], …)`, Base64 bei Strings): `[Salt-Länge (1)] [IV-Länge (1)] [Salt] [IV] [Ciphertext]`.
    Es gibt keine Versionskennung, das erste Byte ist in allen bestehenden Daten `32`.
  - **Stream** (`GetEncryptStream(stream, entropy)`): Kopf mit `MagicNumber (Int32)`, IV-Länge, Salt-Länge, IV, Salt.
  - **Getrennt** (`Encrypt(…, out iv, out salt)`, `GetEncryptStream(…, out iv, out salt)`): Der Aufrufer speichert IV
    und Salt selbst. **Im Einsatz bei Konsumenten**: Webanwendungen mit mandantenweise verschlüsselten Dokumenten,
    das Salt (teils auch der IV) ist ein eigenes DB-Feld. Im Repo sieht man nur das Durchreichen über
    `ISecurityRepository`.

## Bewertung

| Punkt | Risiko | Begründung |
|---|---|---|
| SHA-1 in PBKDF2 | gering | Die Kollisionsangriffe auf SHA-1 betreffen HMAC/PBKDF2 nicht. NIST will SHA-1 bis 2030 aber ganz auslaufen lassen. |
| 10'000 Iterationen | mittel, abhängig von der Passphrase | OWASP: ca. 600'000 für PBKDF2-HMAC-SHA256. Bei einer schwachen Passphrase ist das Durchprobieren nach einem Datenabfluss günstig. **Ein einzelner Wert genügt dafür.** Das Salt pro Wert verteuert das Erraten der Passphrase nicht, es verhindert nur Vorberechnung und gleiche Schlüssel. |
| CBC ohne Authentifizierung | mittel bis hoch, je nach Einsatz | Veränderte Ciphertexte fallen nicht auf. Wo ein Angreifer Ciphertexte einschleusen und das Gelingen beobachten kann, ist ein Padding-Oracle-Angriff möglich. |
| Mandantenschlüssel im Klartext in `Tenants` | hoch bei DB-Abfluss | Schlüssel und damit verschlüsselte Daten liegen in derselben Datenbank. Gegen einen Abfluss hilft dann weder Verfahren noch Iterationszahl (Teil C). |
| Streckung von Zufallsschlüsseln | Leistung | Mandantenschlüssel sind 256 Bit Zufall und nicht zu erraten. Die Iterationen kosten dort nur Zeit. |

## Teil A: Format Version 2

### Entscheid 1: Schlüsselableitung – zweistufig, mit Cache

| Stufe | Verfahren | Salt | Häufigkeit |
|---|---|---|---|
| 1 (teuer) | PBKDF2-HMAC-SHA256, **600'000** Runden (Zahl steht im Wert), Passphrase → Hauptschlüssel | zufällig, **pro Prozesslauf** | einmal, danach im Cache |
| 2 (billig) | HKDF-SHA256, Hauptschlüssel → Schlüssel für diesen Wert | zufällig, **pro Wert** | jedes Mal |

- Jeder Wert hat weiterhin seinen **eigenen Schlüssel und sein eigenes Zufalls-Salt**. Beide Salts stehen im Wert,
  wie heute ist keins davon geheim.
- **Cache:** nur der Hauptschlüssel aus Stufe 1, im Speicher, pro (Hash der Passphrase, Stufe-1-Salt), begrenzt (z.B.
  16 Einträge, LRU), für die Laufzeit des Prozesses. Ein Wert aus einem früheren Lauf kostet beim ersten Lesen einmal
  die teure Ableitung.
- Das Stufe-1-Salt wechselt mit jedem Neustart. Ein häufigerer Wechsel (zeit- oder mengenbasiert) ist möglich und
  ändert an der Sicherheit nichts.
- **Variante für Zufallsschlüssel** (Mandantenschlüssel): Stufe 1 entfällt, HKDF direkt auf dem Schlüssel.
  - Dafür braucht es eine eigene API, z.B. `AesEncryptor.EncryptWithKey/DecryptWithKey` und die Stream-Pendants.
    `AesEncryptor` kann an den Bytes nicht erkennen, ob sie Zufall oder eine umgewandelte Passphrase sind.
  - `CipherExtensions` nutzt diese Variante. Gerade für die mandantenweisen Dokumente entfällt so die teure Ableitung.
- **Verschlüsselung:** AES-256-GCM (`AesGcm`), 12-Byte-Nonce, 16-Byte-Tag.

### Entscheid 2: Streams – segmentiertes GCM

- Der Stream wird in Segmente von z.B. 64 KiB zerlegt, jedes mit eigenem Tag.
- Die Nonce ist ein Präfix plus Segmentzähler. Das letzte Segment ist im zusätzlich authentifizierten Teil markiert,
  damit Abschneiden und Umsortieren auffallen.
- Abgelehnt: CBC + HMAC am Ende. Dabei würde erst am Schluss geprüft, bis dahin wäre ungeprüfter Klartext
  herausgegeben.

### Entscheid 3: Varianten mit getrennt gespeichertem IV und Salt – Version 2 in denselben Signaturen

Diese Varianten sind bei Konsumenten im Einsatz: mandantenweise verschlüsselte Dokumente. `Iv` und `Salt` sind dort
unbegrenzte String-Felder (`[Required] public string Iv/Salt`), es passt also mehr hinein als bisher.

- **„iv"** = `00 | 02 | Nonce-Präfix (8) | Segmentgrösse (Int32 LE)`, also 14 Bytes. Ein Version-1-IV hat immer 16 Bytes,
  daran erkennt der Lesepfad die Version eindeutig.
- **„salt"** = die Schlüsselparameter (Modus, Iterationen, Salt der Stufe 1, Salt des Werts).
- **Daten** = segmentiertes GCM wie bei Streams. Salt und IV werden mit jedem Segment authentifiziert.
- `Encrypt(…, out iv, out salt)` nutzt wie bisher intern das Stream-Format. Blob und Stream bleiben untereinander
  austauschbar.

### Entscheid 4: Kodierung der Passphrase – bleibt UTF-32

Für die Sicherheit ohne Belang. Eine eigene Kodierung für Version 2 lässt sich auch nicht umsetzen: `PasswordSecurity`
wandelt um, bevor die Version des Werts bekannt ist.

### Entscheid 5: Fehlerverhalten – eine einheitliche Exception

Eine `CryptographicException` mit der Meldung „Der Wert konnte nicht entschlüsselt werden (falscher Schlüssel oder
veränderte Daten)". Sie gilt für **beide** Versionen, auch Padding-Fehler bei Version 1 laufen darauf. So verrät die
Meldung nicht, warum es scheiterte, und es entsteht kein Orakel.

### Lesen und Schreiben

- Version 1 wird weiter gelesen, geschrieben wird nur noch Version 2.
- **Erkennung:**
  - Blob: Ein führendes `0x00` kennzeichnet das versionierte Format (eine Salt-Länge 0 kommt in Version 1 nie vor).
    Danach folgen Versionsnummer, Parameter, Salts, Nonce, Ciphertext und Tag.
  - Stream: neue Magic Number.

## Teil B: Umstellung bestehender Werte

Ein automatisches Umschreiben scheidet aus. Beispielsweise müssten in Einstellungen einzelne verschlüsselte
JSON-Properties erkannt, einzeln neu verschlüsselt und zurückgeschrieben werden. Stattdessen:

- **Warnung beim Lesen von Version 1, nur für Strings.**
  - Die Meldung nennt das alte Format und enthält den Stacktrace, damit nachvollziehbar ist, woher der Wert kommt.
  - Sie erscheint **einmal pro Aufrufstelle und Prozess**: Schlüssel ist ein Hash der obersten Stack-Frames, mit
    Zähler. So flutet ein häufig gelesener Wert das Log nicht.
  - Der Stacktrace kostet Mikrosekunden, das Entschlüsseln in Version 1 schon heute Millisekunden.
- **Öffentlich `AesEncryptor.IsLegacyFormat(string)`** (und für `byte[]`): Damit kann ein Integrator seine Einstellungen
  und Tabellen gezielt absuchen, z.B. alle `encrypt:`-Werte.
- Wer einen Wert neu speichert, schreibt automatisch Version 2. Die Warnungen zeigen also genau das, was seit der
  Migration nicht mehr geschrieben wurde.
- Byte-Blobs und Streams (Dokumente) warnen nicht. Ihre Umstellung hängt an Entscheid 3.

## Teil C (offen): Mandantenschlüssel schützen

**Idee:** `Tenant.TenantPassword` wird mit dem Hauptschlüssel der Anwendung verschlüsselt abgelegt. Das ist die
Passphrase aus `PasswordSecurity`, die ausserhalb der Datenbank liegt (Konfiguration, Secret Store). Bei jeder
Verwendung wird der Mandantenschlüssel zuerst entschlüsselt. Ein DB-Abfluss allein gibt die Mandantenschlüssel dann
nicht mehr preis.

**Abzuklären:** Wo `TenantPassword` gelesen und geschrieben wird. Erste Suche (ohne Migrationen):

| Art | Stellen |
|---|---|
| Lesen, Verschlüsselung | `CipherExtensions` (`GetEncryptionKey`), `DbPluginFormatter` (flach und Baum, je 4), `FormatHelperInfo` (Baum) |
| Lesen und Schreiben, Admin | `PlugInAdminHandler` (MudBlazor), `PlugInConstController` (Telerik), `TenantAdminHandler`, `TenantControllerClass`/`TenantControllerStruct` (Telerik) |
| Schreiben, neue Schlüssel | `TenantPasswordAssignment`, `OnboardingHandler`, `HierarchyOnboardingHandler`, `RegistrationController`, `CreateTenant.cshtml.cs` |
| Konfiguration | `TenantOptions`, `SecurityViewsOptions`, `WebPartInit` (Telerik) |
| SQL | `SqlColumnsSyntaxHelper`, `PostgreSqlColumnsSyntaxHelper` (Spalte in den Prozeduren) |

- **Ziel:** genau eine Stelle, die den Mandantenschlüssel liefert, z.B. im `ISecurityRepository` oder als eigener
  Dienst. Alle Leser gehen über sie. Heute lesen u.a. die beiden `DbPluginFormatter` und die Admin-Stellen die Spalte
  direkt.
- **Migration:** Bestehende Mandantenschlüssel einmalig verschlüsseln. Das ist eine überschaubare Tabelle und lässt
  sich automatisch umstellen, anders als Teil B. Erkennung über `IsLegacyFormat` bzw. ein Präfix.
- **Abhängigkeit:** Der Hauptschlüssel der Anwendung muss bei allen Prozessen, die Mandantenschlüssel brauchen,
  konfiguriert sein (Web, Worker, Agent).

## Tests (Vorschlag)

- Version 2: Hin- und Rückweg für Blob, String (Base64), Stream und die Variante für Zufallsschlüssel.
- Bestehende Version-1-Werte (feste Testvektoren aus dem heutigen Code) werden weiter gelesen.
- Ein manipulierter Ciphertext, Tag, Kopf, ein abgeschnittenes oder umsortiertes Stream-Segment ergeben jeweils
  dieselbe Exception wie ein falsches Passwort.
- Die Iterationszahl aus dem Wert wird benutzt, nicht die aktuelle Vorgabe.
- Cache: Die zweite Entschlüsselung mit gleichem Stufe-1-Salt leitet nicht neu ab. Unterschiedliche Passphrasen teilen
  keinen Eintrag.
- `IsLegacyFormat` erkennt Version 1 und 2 korrekt. Die Warnung beim Lesen von Version 1 kommt einmal pro Aufrufstelle.
