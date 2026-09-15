# Issue: Die Stelligkeit im vertrauten Typnamen bricht bei jedem Bump — lautlos

**Status:** ERLEDIGT — umgesetzt im Toolkit (siehe „Auflösung“ am Ende). **Vorschlag 2 bewusst nicht.**
**Datum:** 2026-09-15
**Quelle:** MiniStore-Session (Konsument). Beim Bump PRE239 → PRE240 aufgetreten und diagnostiziert.
**Toolkit-Stand:** `5.0.0-PRE240`.

## Der Fall

`DbSecurityAccessProvider.ResolveTrustLevelConfig` schlägt den vertrauten Typ **zeichengenau** nach:

```csharp
dict[(c.FullQualifiedTypeName, c.TargetQualifiedTypeName)] = c.TrustLevelConfig;
…
return trustConfigCache.TryGetValue((trustedTypeName, trustingTypeName), out var cfg) ? cfg : null;
```

Im AQN eines generischen Typs steckt die Zahl seiner Typparameter. Der Eintrag lautet also

```
…TreeShared.Security.DbSecurityRepository`46, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity, …
```

PRE240 hat dem Repository (und `SharedAssetInfoProvider`) je **einen Typparameter genommen** — §65.2,
`ClientAppUsers` ist weg, `ClientAppAccesses` und `DevicePairings` kamen dazu. Aus ``46`` wurde ``45``.
Der geseedete Name passt nicht mehr, das Nachschlagen liefert `null`, der Aufrufer gilt als **nicht
vertraut**.

## Warum das teuer ist

Der Bruch zeigt sich **nirgends dort, wo man ihn erwartet**:

- Der Build ist grün — die Zahl steht in einer Datenbankzeile, nicht im Code.
- Die Migration läuft durch — sie prüft die Zeile nicht gegen die Assembly.
- Die **Anmeldung gelingt**. Erst danach ist der Mandant leer.

Im Protokoll steht:

```
No Trust Configuration found for the caller (…DbSecurityRepository`45, …)      ← 1039×
No eligible scopes for user labels [laden@ecke.ch].                            ← 360×
```

Die zweite Meldung ist die, die ein Betreiber sieht und meldet — und sie zeigt auf Rechte und
Mandanten. Dort ist alles richtig. Wir haben `TenantUsers`, `GetUpwardsRoleTreeForLabels`,
`AuthenticationTypes` und die EF-Filter geprüft, bevor die **Stelligkeit in der ersten Meldung**
auffiel. Genau dort steht die Antwort ja — nur liest man `` `45`` als Rauschen, nicht als Nutzlast.

Dasselbe gilt für jeden künftigen Bump: **jede** hinzugefügte oder entfernte Entität am
Sicherheitskontext verschiebt die Stelligkeit aller Typen, die den Kontext generisch führen. Der
Konsument kann das nicht vorhersehen und merkt es erst im Betrieb.

## Vorschläge, in der Reihenfolge, die uns am meisten hülfe

### 1. Beim Nichttreffer sagen, was man erwartet hätte (kleinster Eingriff, grösster Nutzen)

Der Provider hat beim Fehlschlag die ganze Tabelle im Cache. Ein Vergleich über den Namensteil vor
dem Backtick kostet nichts und macht aus einer Sackgasse eine Diagnose:

```
No Trust Configuration found for the caller (…DbSecurityRepository`45, …).
A trust entry for the same type with a DIFFERENT generic arity exists (`46). This usually means the
entry predates a toolkit upgrade that changed the entity set of the security context.
```

Damit wäre unsere Suche nach zwei Minuten vorbei gewesen statt nach einer Stunde.

### 2. Beim Nachschlagen die Stelligkeit nicht verlangen

Der Schlüssel könnte auf Namensraum + Typname ohne `` `n`` normalisiert werden — beim Laden **und**
beim Nachschlagen. Ein Sicherheitsgewinn geht dabei nicht verloren: es gibt keine zwei Typen gleichen
Namens im selben Namensraum, die sich nur in der Stelligkeit unterscheiden und verschieden vertraut
gehören sollen. Wenn doch, bliebe die Stelligkeit als **optionale** Verschärfung erhalten (steht sie
im Eintrag, muss sie passen; fehlt sie, zählt der Name).

### 3. Eine Prüfung, die man bewusst laufen lassen kann

Ein `ISecurityAccessProvider.ValidateTrustEntries()` o. ä., das jede Zeile gegen die geladenen
Assemblies auflöst und die nicht auflösbaren meldet — aufrufbar aus einem Startup-Check oder der
Diagnose-Maske. Wir würden ihn beim Hochfahren rufen und den Fehler damit beim Deployment sehen
statt beim ersten Benutzer. (Die Maske `TrustedComponentAdminHandler` könnte dieselbe Prüfung als
Spalte „auflösbar" zeigen.)

### 4. Vermerk im Leitfaden

Unabhängig von allem Übrigen: dass ein Bump die geseedeten Vertrauens-Einträge invalidieren **kann**,
steht nirgends. Ein Satz bei den Release-Hinweisen jedes Bumps, der die Stelligkeit ändert („die
vertrauten Typen `X` und `Y` haben neue AQNs"), wäre für Konsumenten die billigste Absicherung.

## Was wir getan haben

Eine Migration `TrustArityPRE240`, die beide Zeilen über den Namens-**Präfix** (nicht über die alte
Zahl) auf die neue Stelligkeit zieht, plus eine Notiz in unserem `CLAUDE.md`: **bei jedem
Toolkit-Bump die Stelligkeiten gegen die Assembly prüfen.** Das ist eine Handreichung, keine Lösung —
sie hilft nur dem, der den Fall schon einmal hatte.

**Priorität von unserer Seite: mittel.** Nicht dringend, aber es wird wiederkommen, und beim nächsten
Mal trifft es womöglich eine Installation beim Kunden statt eine Entwicklungsmaschine.
---

## Auflösung (Toolkit)

Umgesetzt sind **1, 3 und 4**. Vorschlag **2 bewusst nicht** — die Begründung steht unten, sie kam von
euch selbst.

### 1. Der Nichttreffer sagt jetzt, was er vorfand

Findet der Provider keinen Eintrag, sucht er in der bereits geladenen Tabelle nach einem Eintrag für
**denselben Typ mit anderer Stelligkeit** und schreibt ihn dazu:

```
No Trust Configuration found for the caller (…DbSecurityRepository`45, …) on …DbSecurityRepository, …
 A trust entry for the SAME type exists, but with different generic arity (expected `45, the entry
 says `46): '…DbSecurityRepository`46, …'. This usually means the entry predates a toolkit upgrade
 that changed the entity set of the security context. Fix the stored row so it carries the caller
 name reported at the start of this message (do not add a second row - the pair is unique), and call
 ISecurityAccessProvider.ValidateTrustEntries() on startup to see this at deployment time instead of
 at the first user.
```

Zwei Dinge über euren Vorschlag hinaus:

- **Der Zieltyp steht jetzt auch in der Meldung** — bisher nannte sie nur den Aufrufer. Bei zwei
  Namen, von denen einer nicht passt, ist das der Unterschied zwischen einer Diagnose und der Hälfte
  davon. Er steht in **gekürzter Form** (Typname ohne Typargumente plus einfacher Assemblyname): der
  vollständige AQN eines geschlossenen generischen Typs mit 45 Argumenten ist einige Kilobyte lang,
  und den wollte niemand 1039-mal im Log.
- **Der Zusatz wird nur beim ersten Nichttreffer je Paar geschrieben.** Genau derselbe Grund: eure
  1039 Zeilen wären sonst 1039 vollständige Namenspaare. Der Provider ist scoped, die Zählung also
  je Anfrage bzw. Circuit.

### 2. Nicht umgesetzt — und zwar aus eurem eigenen Argument

Ihr habt beim Verwerfen von Vorschlag 2 selbst den Finger auf den Punkt gelegt: *„die typen werden ja
als assembly-qualified angegeben und das müsste ja vermutlich geparst werden."* Das stimmt, und es ist
mehr, als es aussieht — ein **geschlossener** generischer Typ trägt seine Typargumente in `[[…]]`
**vor** dem Assembly-Teil, und jedes davon ist selbst wieder assembly-qualifiziert:

```
Outer`2[[Arg1, AsmA, Version=…],[Arg2, AsmB, Version=…]], AsmC, Version=…
         ^ das erste Komma – es trennt NICHT Typ von Assembly
```

Ein Schlüssel, der die Stelligkeit weglässt, müsste also beim **Laden und beim Nachschlagen** korrekt
zerlegen — und dann trüge der Schlüssel des Vertrauens-Mechanismus eine Normalisierung, die stiller
scheitern kann als das, was sie behebt. Für die **Diagnose** ist genau dieselbe Zerlegung völlig in
Ordnung (sie darf danebenliegen, ohne Schaden anzurichten), und dort ist sie jetzt auch: der Zerleger
`TrustTypeName` schneidet am ersten Komma auf **Klammertiefe 0**. Der Nachschlage-Weg selbst bleibt
zeichengenau.

### 3. `ValidateTrustEntries()` gibt es

Auf `ISecurityAccessProvider`, als **Default-Methode** — fremde Umsetzungen brechen dadurch nicht, sie
melden `NotSupported`. Sie löst jede Zeile gegen die geladenen Assemblies auf und protokolliert das
Ergebnis selbst (Warnung bei Befunden, Bericht bei sauberem Stand).

**Ein Detail, das mehr findet als die naheliegende Prüfung:** geprüft wird nicht bloss, ob sich der Typ
*laden* lässt, sondern ob der gespeicherte Name **zeichengenau** dem entspricht, was der geladene Typ
als `AssemblyQualifiedName` führt — denn genau so schlägt der Provider nach. Ein Typ kann sich laden
lassen (die Bindung ist versionstolerant) und der Eintrag trotzdem nie greifen.

Findet die Prüfung einen Namen, der nicht auflöst, sucht sie in den geladenen Assemblies nach einem Typ
gleichen Namens **ohne Stelligkeit** und nennt ihn. Das ist teuer (sie zieht die Typliste der Assembly)
und läuft deshalb erst, wenn das günstige `Type.GetType` bereits gescheitert ist.

Verdrahtet ist sie **bewusst nicht automatisch** — ruft sie beim Hochfahren.

Die Maske `/Security/TrustedComponents` hat die Spalte **„Resolvable"** bekommen, wie von euch
vorgeschlagen: Häkchen oder rotes Zeichen, Tooltip mit dem Grund.

**Nebenbefund der eigenen Prüfung:** eine Zeile mit leerem `TargetQualifiedTypeName` kann **nie**
treffen — das Nachschlagen vergleicht gegen einen echten `AssemblyQualifiedName`, es gibt keinen
Platzhalter. Die Maske legt solche Zeilen an (leeres Zielfeld wird zu `string.Empty`), die Prüfung
meldet sie jetzt.

### 4. Der Vermerk im Leitfaden

**Leitfaden-Abschnitt 67** — und das war schlicht ein Versäumnis: der Abschnitt gehört sachlich zu §65,
wo die Entitäten verschoben wurden. Er nennt die Tabelle der betroffenen Typen:

| Typ | vorher | seit PRE240 |
|---|---|---|
| `DbSecurityRepository` (Shared und TreeShared) | `` `46 `` | `` `45 `` |
| `SharedAssetInfoProvider` | `` `47 `` | `` `46 `` |
| `TenantTemplateHelperBase` | `` `47 `` | `` `46 `` |

Und er hält fest, was die Schlussfolgerung daraus ist: **jede Stelligkeit in einem dieser Dokumente ist
eine Momentaufnahme** und gehört aus der eigenen Assembly abgeschrieben, nie aus einem Dokument. In
`BUG-PRE230` steht ein entsprechender Nachtrag an der Stelle mit ``SharedAssetInfoProvider`47``.

### Euer Befund zum Mechanismus stimmt

Er fällt **zu**, nicht auf: ein gebrochener Eintrag gewährt keine Rechte, er entzieht sie. Ein
Verfügbarkeits-, kein Sicherheitsproblem — was nichts daran ändert, dass er eine Stunde gekostet hat.

Eure Migration `TrustArityPRE240` über den Namens-Präfix ist der richtige Weg und überlebt auch den
übernächsten Bump; §67.4 empfiehlt genau diese Form.
