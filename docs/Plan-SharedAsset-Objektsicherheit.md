# Plan: Geteilte Assets — von der Pfadfreigabe zur Objektsicherheit

Status: **Entwurf, noch nicht umgesetzt.** Stand 2026-08-26, Zweig Future_10.
Setzt auf `docs/Plan-SharedAsset-Pfadkontext.md` auf (umgesetzt).

## 1. Ausgangslage

Der Pfad-Plan hat den Asset-Kontext navigationsfest gemacht: der Schluessel steht im Pfad, der
Kontext ueberlebt Klicks, und er wirkt in beiden Welten. **Was er nicht geaendert hat, ist die Frage,
worauf ein Asset eigentlich zeigt.**

Und die Antwort darauf ist heute unbefriedigend:

> Beim Anlegen wird `SharedAsset.RootPath` gespeichert — bei der Zugriffspruefung aber **nicht
> benutzt**. Geprueft wird ausschliesslich, ob der angefragte Pfad auf eines der **Regex-Muster der
> Vorlage** passt (`SharedAssetInfoProvider.IsTemplateValidForPath`).

Wer „Auftrag 4711" teilt, gibt damit alles frei, was das Muster zulaesst — auch 4712. Solange die
Muster eng gefasst sind, faellt das nicht auf. Als Konzept ist es aber „teile eine **Seite**", nicht
„teile ein **Objekt**".

Dieser Plan macht daraus die zweite Achse: **welches Objekt**.

## 2. Entscheidungen

### 2.1 Argumente statt Record-Identitaeten

Ein Asset zeigt nicht auf „Datensatz N in Tabelle X", sondern traegt **benannte Argumente mit Typ**.
Begruendung:

- Das Toolkit kennt die Datenbank des Hosts nicht.
- Objekte bestehen oft aus mehr als einem Schluesselwert.
- Nicht jede Freigabe ist ein Datensatz (ein Zeitraum, ein Vorgang, ein Mandant).
- URL und Formular denken ohnehin in Argumenten.

„Record in Tabelle X" ist damit der **Spezialfall** eines Arguments, nicht das Modell.

### 2.2 Zwei Fragen, zwei Zeitpunkte

Der erste Entwurf wollte die Rechte der Vorlage erst gelten lassen, wenn die Argumente bestaetigt
sind. **Das geht nicht** — Rechte und Features werden am Eintritt geprueft, die Argumente sind erst
danach bekannt. In jedem Host, ausnahmslos.

| Frage | Wann | Womit |
|---|---|---|
| Darf der ueberhaupt hier rein? | Eintritt (Autorisierung) | Rechte + Features der Vorlage, Pfadmuster — **unveraendert** |
| Darf **dieses Objekt** an ihn raus? | Ausgang, sobald das Objekt bekannt ist | Argumentpruefung |

Objektsicherheit ist eine **zweite Achse**, keine Verschaerfung der ersten.

### 2.3 Der Riegel gehoert an den Ausgang

Damit aus „der Handler muss dran denken" ein „wenn er nicht dran denkt, kommt nichts raus" wird:

- **MVC**: ein Filter, der nach dem Model-Binding greift, die gebundenen Werte als Bestaetigung
  anbietet (Namensgleichheit) und **nach** der Action prueft, ob eine Bestaetigung vorliegt. Fehlt
  sie, wird das Ergebnis verworfen statt ausgeliefert. Global registrierbar ueber den bestehenden
  `MvcRegistrationMethod`-Haken.
- **Blazor**: dort gibt es keinen Ausgang, aber eine Render-Grenze — eine Komponente
  `<AssetScope Args="…">`, die ihren Inhalt erst rendert, wenn die Bestaetigung passt. Dasselbe
  Muster wie `SecureView`, eine Achse weiter.
- **FileHandler und Konsorten**: dort, wo das Token aufgeloest ist und **bevor** gestreamt wird.

### 2.4 Kein dritter Ring

Erwogen und **verworfen**: ein globaler Query-Filter auf der gebundenen Entitaet, solange ein
Asset-Kontext laeuft (analog zum Mandantenfilter). Er waere der einzige Ring, der auch Code schuetzt,
der nie von Assets gehoert hat — kostet aber je Argument eine Entitaets- und Spaltenangabe und
greift dort ein, wo schon der Mandant sitzt. Zwei Ringe reichen.

### 2.5 Unter-Objekte: Normalisierung nach oben

Ob Position 815 zu Auftrag 4711 gehoert, kann nur der Host wissen. Also: die Vorlage benennt je
Argument optional einen **Aufloeser**, den der Host implementiert („gib mir zu `positionId` den
`orderId`"). Die Pruefung loest das Argument der Anfrage nach oben auf, bis es auf der geteilten
Ebene liegt, und vergleicht dort.

Einmal je Argumenttyp, nicht je Endpunkt.

### 2.6 Die Route-Vorlage ist der Schluessel, nicht der konkrete Pfad

`/sales/order/{id}` ist Stelle **und** Argumentquelle zugleich. Daraus faellt ab:

- die Argumentnamen stehen schon drin — zu ergaenzen sind nur Typ, Pflicht und die Argumente, die
  **nicht** in der Route stehen (Query, Body);
- Typen kommen aus den Constraints (`{id:int}`);
- **die Maske kann die URL bauen** (Vorlage + Werte -> `/sales/order/4711`). Mit einem konkreten
  Pfad als Schluessel ginge das nicht.

### 2.7 Ad-hoc-Freigaben: der schmale Fall

Ein Objekt, ein Empfaenger, kurze Pflichtfrist, nichts persistiert. Damit entfallen Empfaengerlisten,
Weitergabe und das ganze Filter-Thema im Ticket.

### 2.8 Der Empfaenger steht neben dem Platzhalter, nicht darin

`#ANONYMOUS#` ist heute ein **exakter** Vergleich in `AssetIsAccessible` — die
sicherheitskritischste Zeile des Mechanismus. Eine Empfaengeradresse in das Label zu falten
(`#ANONYMOUS#mw@example.com#`) wuerde daraus einen Praefix-Vergleich machen. Stattdessen: das Label
bleibt der Filter-Platzhalter, die Adresse kommt als **eigener Wert** daneben (am Asset gespeichert
bzw. im Ticket verschluesselt, als Claim am Prinzipal, als Namensspalte im Protokoll).

**Merke: die Adresse ist eine Behauptung, kein Nachweis.** Wer den Link hat, ist wer der Link sagt.
Fuer Zuordnung und Protokoll in Ordnung, als Identitaet nicht — und im Klartext in der URL hat sie
nichts verloren (Server-Logs, Referer).

### 2.9 Der Bestand bleibt unangetastet

Eine Vorlage **ohne** Argumente verhaelt sich exakt wie heute. Objektsicherheit bekommt, wer
Argumente pflegt. Kein Zwang, keine Umstellung bestehender Freigaben.

## 3. Datenmodell

### 3.1 Die Registry — zwei Tabellen in `ICoreSystemContext`

Was ein Endpunkt versteht, ist eine Eigenschaft des **Codes**, nicht des Mandanten. Die Tabellen sind
deshalb **mandantenfrei** und liegen in `ICoreSystemContext`, neben `Features` und `GlobalSettings`.

```
AssetConsumer
  AssetConsumerId      int, PK
  DeclarationKind      Path | Type          }
  DeclarationKey       string(1024)         }-- unique zusammen
  FirstSeenUtc         DateTime
  LastSeenUtc          DateTime

AssetConsumerArgument
  AssetConsumerArgumentId  int, PK
  AssetConsumerId          int, FK (Cascade)
  ArgumentName             string(128)      }-- unique je Konsument
  ArgumentType             string | int | long | guid | date
  Required                 bool
  SortOrder                int
```

**Warum zwei Tabellen und nicht eine flache:** `LastSeenUtc` gehoert dem Endpunkt, nicht jedem
Argument — flach muesste eine Sichtung N Zeilen anfassen und koennte innerhalb desselben Endpunkts
abweichende Zeitstempel hinterlassen. Ausserdem werden „Argument weg" und „Endpunkt weg"
unterscheidbar, und `SortOrder` gibt der Teilen-Maske die Feldreihenfolge des Endpunkts statt einer
alphabetischen.

**Warum zwei Arten von Schluessel:** siehe 9.1 — der FileHandler laesst sich nicht ueber den Pfad
identifizieren.

### 3.2 Erweiterung `AssetTemplate`

| Neu | Bedeutung |
|---|---|
| `Arguments` (Tabelle) | Name, Typ, Pflicht, optional Aufloeser-Schluessel |
| `Consumers` (Tabelle) | Verweise auf registrierte Konsumenten (Route-Vorlagen bzw. Typen) |
| `ArgumentEnforcement` | `None` \| `Confirmed` \| `Strict` (siehe 5.4) |
| `AllowAdHoc` + `MaxAdHocDuration` | ob und wie lange Tickets erlaubt sind |
| `ValidityRuleKey` | benanntes Host-Praedikat („Auftrag ist offen") |
| `AuditMode` | Protokollierung: aus \| Einstiege \| Einstiege+Verweigerungen |

Die bestehenden `PathTemplates` (Regex) **bleiben** — sie decken Unterressourcen ab, die nie ein
Argument tragen und trotzdem im Kontext geladen werden muessen (Bilder, Skripte, statische Anhaenge).
Ob eine Vorlage mit Konsumenten sie noch braucht, steht in 11.

### 3.3 Erweiterung `SharedAsset`

| Neu | Bedeutung |
|---|---|
| `ArgumentValues` (Tabelle) | Name + Wert je Argument der Vorlage |
| `RecipientLabel` | Empfaengerangabe (z.B. E-Mail), **neben** dem Filter-Platzhalter |

### 3.4 Protokoll

```
SharedAssetAccess
  SharedAssetAccessId
  SharedAssetId?       -- null bei Ad-hoc
  TemplateSystemKey    -- traegt den Ad-hoc-Fall
  TicketNonce?         -- identifiziert das Ticket
  TenantId
  RecipientLabel?
  RequestPath
  ArgumentsJson
  Granted              bool
  DenyReason?          -- kurz, maschinenlesbar
  Created              DateTime (UTC)
```

### 3.5 Migrationen

**Pflicht am Host, drei Schritte:** Registry (2 Tabellen), Vorlagen-/Asset-Erweiterung (3 Tabellen +
Spalten), Protokoll (1 Tabelle). Kein Altbestand-Nachtrag — alles neu, und bestehende Vorlagen
bleiben ohne Argumente gueltig.

## 4. Die Registry zur Laufzeit

Ein **Singleton** `IAssetArgumentRegistry`. Ein Endpunkt meldet im Konstruktor, welche Argumente er
versteht; die Registry legt Unbekanntes an.

- **Der Singleton haelt keinen Kontext, nur die Fabrik.** Persistiert wird ueber
  `ICoreSystemContextFactory.UseAsync(...)` — ein frischer Kontext je Schreibvorgang. Kein neues
  Interface noetig, `ICoreSystemContext` ist genau dafuer da.
- **Auf dem heissen Pfad wird nicht geschrieben.** Die Meldung kommt aus einem Konstruktor, also
  potenziell bei jedem Seitenaufbau: Abgleich gegen das In-Memory-Set, und nur wirklich Unbekanntes
  geht in eine Warteschlange, die im Hintergrund **gebuendelt** wegschreibt. Siehe 9.6.
- **Einmal laden beim ersten Zugriff.** Damit kennt die Maske auch, was seit dem Neustart niemand
  besucht hat — das ist der eigentliche Zweck der Persistenz.
- **Route-Vorlagen kommen aus den Endpoint-Metadaten**, nicht aus Handarbeit: MVC ueber die
  Action-Descriptors, Blazor ueber die `@page`-Direktiven der Router-Assemblies. Ausdruecklich zu
  nennen sind nur Argumente ausserhalb der Route und Faelle, in denen dieselbe Klasse mehrere Routen
  mit **unterschiedlichen** Argumenten bedient.
- **Abgleich innerhalb eines Konsumenten ersetzt.** Eine Meldung ist die vollstaendige Liste aus
  Sicht dieses Endpunkts — verschwundene Argumente werden geloescht, neue kommen dazu.
- **Konsumenten werden nie automatisch geloescht.** Dass sich einer seit dem Neustart nicht gemeldet
  hat, heisst nicht, dass es ihn nicht mehr gibt. Dafuer `LastSeenUtc` in der Maske und Loeschen von
  Hand.
- **Schalter**: das Wegschreiben ist abschaltbar (Vorgabe **an**). Anders als bei der
  Auto-Permission-Registrierung entstehen hier nur Deklarationen, die nichts gewaehren. Ohne
  Schreiben bleibt die Registry im Speicher und die Maske so schlau wie die laufende Instanz.

**Die Registry ist keine Sicherheitsschranke.** Sie fuettert die Teilen-Maske und die Validierung.
Sicher wird es durch den Riegel in 5.

## 5. Der Riegel zur Laufzeit

### 5.1 Bestaetigen

`ISharedAssetContext.Require(name, value)` bzw. `Require(new { orderId = … })` — scoped, mit Werten.
Zeitpunkte:

| Wo | Wann |
|---|---|
| MVC | im Filter automatisch aus den gebundenen Werten, zusaetzlich manuell in der Action |
| Blazor | in `<AssetScope>` bzw. sobald der Datensatz geladen ist |
| FileHandler | **nach** dem Aufloesen des Tokens, vor dem Streamen |

### 5.2 Vergleichen

Der Vergleich **normalisiert nach Typ**. `"04711"` und `4711` muessen dieselbe Antwort geben, und
zwar von Anfang an — siehe 9.4.

### 5.3 Aufloesen

Passt der gemeldete Wert nicht zur geteilten Ebene, wird er ueber den Aufloeser der Vorlage nach oben
normalisiert (2.5), dann verglichen.

### 5.4 Strenge

| Grad | Wirkung |
|---|---|
| `None` | wie heute: nur Pfadmuster |
| `Confirmed` | ohne Bestaetigung wird die Antwort **nicht ausgeliefert** |
| `Strict` | zusaetzlich: jede weitere Bestaetigung im selben Kontext muss **dieselben** Werte liefern |

Vorgabe fuer jede Vorlage **mit** Argumenten: mindestens `Confirmed`. Der vergessene Check wird damit
zur sichtbaren leeren Seite statt zum stillen Loch.

## 6. Ad-hoc-Tickets

**Zweiter Marker**: `~` = persistiertes Asset, `~!` = Ticket. Ein Zeichen mehr, und der Parser weiss,
ob er ueberhaupt in die Datenbank muss.

**Nutzlast** (verschluesselt mit dem Mandantenschluessel — dasselbe Primitiv wie der heutige
anonyme Token): Vorlagen-Schluessel, Mandant, Argumentwerte, Gueltigkeitsfenster, Nonce, optional die
Empfaengerangabe. **Rechte reisen nicht mit** — die stehen in der Vorlage, die persistiert bleibt.
Das haelt die URL kurz und macht einen Grob-Widerruf moeglich (Vorlage abschalten).

**Gueltigkeit als Bedingung, nicht als Datum.** „Bis der Auftrag abgeschlossen ist" ist kein Ablauf;
dafuer benennt die Vorlage eine Regel, die der Host implementiert. Ohne das baut man es spaeter als
Ablaufdatum nach und liegt immer daneben.

**Widerruf** ist die ehrliche Schwaeche — in dieser Reihenfolge: kurze Pflichtlaufzeit aus der
Vorlage, dann eine kleine Sperrliste widerrufener Nonces, im Notfall Schluesselrotation am Mandanten.

## 7. Protokollierung

- Geschrieben werden **Einstiege** (erste Anfrage je Kontext) und **jede Verweigerung**.
- **Nicht** jede Anfrage: Unterressourcen machen daraus sofort DB-Spam — dieselbe Lektion wie beim
  SystemLog.
- Ad-hoc-Tickets ueber `TicketNonce` + `TemplateSystemKey` statt ueber eine Asset-Id.
- Die Verweigerungen sind die Haelfte, die man hinterher braucht.

## 8. Masken

1. **Teilen im Kontext** — ein Knopf auf der Seite: fragt die passenden Vorlagen ab, belegt die
   Argumente **aus dem aktuellen Kontext vor** (dafuer die Registry), legt an, zeigt den fertigen
   Link zum Kopieren. Das ist der eigentliche Nutzen; ohne Vorbelegung tippt der Benutzer eine
   Nummer ab, die zwei Zentimeter weiter oben steht.
2. **Meine Freigaben** — je Mandant: Liste, Titel, Vorlage, Argumente, Gueltigkeit, Empfaenger, Link
   neu erzeugen, loeschen. Deutlich sichtbar, welche Freigaben `%` bzw. anonym sind.
3. **Konsumenten** — Master/Detail ueber die Registry (Art, Schluessel, `LastSeenUtc`; Argumente als
   Untertabelle), mit Loeschen von Hand.
4. **Vorlagen** — die bestehende Maske um Argumente, Konsumenten, Strenge, Ad-hoc, Gueltigkeitsregel
   und Protokollierung erweitern.

**Beide Ebenen fehlen heute vollstaendig** — weder MVC noch Blazor haben je eine Teilen- oder
Freigabe-Maske gehabt; nur die Vorlagen sind gepflegt. Assets sind bisher ausschliesslich ueber
Host-Code oder direkt in der Datenbank entstanden.

## 9. Fallstricke

### 9.1 Der FileHandler laesst sich nicht ueber den Pfad identifizieren

`/File/{token}` ist **ein** Pfad, hinter dem beliebig viele Implementierungen stehen. Welche es ist,
weiss man erst nach dem Aufloesen des Tokens — und der Plugin-Name taugt nicht als Identitaet, weil
zwei Mandanten unter demselben Namen verschiedene Klassen fahren koennen.

Deshalb `DeclarationKind = Type`: der CLR-Typ ist eine Eigenschaft des Codes, mandantenfrei und beim
Konstruieren bekannt.

**Dekodiert wird nichts doppelt.** Das Token wird ausgewertet, wo es heute ausgewertet wird — im
File-Endpunkt. Danach ruft der Handler `Require(...)`. Genau dieser Fall ist der Beweis dafuer, dass
der Riegel an den Ausgang gehoert und nicht in die Middleware.

**Folge, die man kennen muss:** typgeschluesselte Deklarationen lassen sich nicht gegen die
Pfadmuster einer Vorlage halten. Die statische Pruefung (9.2) deckt nur pfadgeschluesselte Endpunkte
ab; fuer den FileHandler bleibt der Laufzeit-Riegel. Kein Loch, aber keine Vorwarnung in der Maske.

Und: der File-Endpunkt kennt eine Datei-Kennung, das Asset einen Auftrag. Der FileHandler ist damit
nicht der Sonderfall, sondern der Normalfall, an dem man sieht, warum es den Aufloeser aus 2.5
braucht.

### 9.2 Die Registry ist unvollstaendig — die Pruefung darf nur warnen

Ein Singleton, der sich beim Konstruieren fuellt, kennt nur, was seit Prozessstart besucht wurde;
nach einem Deployment ist er zunaechst leer. Deshalb:

| Wann | Was | Verhalten |
|---|---|---|
| Vorlage speichern | verlangt ein registrierter Konsument diese Argumente? | **Warnung** — „unbekannt" ist nicht „falsch" |
| Asset/Ticket erzeugen | sind alle Pflichtargumente belegt und typkonform? | **Fehler** — braucht nur die Vorlage, ist immer verlaesslich |

Die Grenze zwischen diesen beiden ist die, die man spaeter bereut, wenn man sie nicht zieht.

### 9.3 Der vergessene `Require`

Faengt der Ausgangs-Riegel (5.4). Wichtig ist die **Vorgabe**: Vorlage mit Argumenten ⇒ mindestens
`Confirmed`. Sonst haengt die Objektsicherheit am Wohlverhalten jedes einzelnen Endpunkts.

### 9.4 Der Typvergleich

`"04711"` gegen `4711` ist die Zeile, die sonst in einem halben Jahr als „der Link geht manchmal
nicht" wiederkommt. Geschlossener Typsatz, kanonische Form, ein Vergleich — und Tests genau dafuer.

### 9.5 Ad-hoc ist nicht einzeln widerrufbar

Siehe 6. Wer das nicht akzeptieren kann, nimmt ein persistiertes Asset — das ist der Preis dafuer,
dass nichts in der Datenbank steht.

### 9.6 Der Schreibsturm

Beim Auto-Permission-Registrar war inline schreiben und invalidieren die Ursache des teuersten
Fehlers dieser Ecke (`RESOURCE_SEMAPHORE`). Die Registry meldet aus **Konstruktoren** — also
gebuendelt im Hintergrund, nie auf dem Anfragepfad.

### 9.7 Der Ablauf wirkt im Circuit verzoegert

Die Asset-Claims haengen am Prinzipal, der beim Circuit-Start eingefroren wird. `NotAfter` und die
Gueltigkeitsregel wirken erst bei der naechsten HTTP-Anfrage. Uebernommen aus dem Pfad-Plan (dort
8.5) und weiterhin bewusst offen.

## 10. Phasen

1. **Registry.** Zwei Tabellen in `ICoreSystemContext`, Singleton mit In-Memory-Set und gebuendeltem
   Schreiben, Ableitung der Route-Vorlagen aus den Endpoint-Metadaten. Admin-Maske (Master/Detail).
   Wirkt noch auf nichts — reine Deklaration.
2. **Argumente an Vorlage und Asset.** Datenmodell, Vorlagen-Maske, harte Pruefung beim Erzeugen
   (9.2 untere Zeile), Warnung beim Speichern (obere Zeile).
3. **Der Riegel.** `Require`, MVC-Filter, `<AssetScope>`, Strenge-Grade, Aufloeser-Vertrag.
   FileHandler als erster echter Konsument.
4. **Teilen-Maske und Freigabe-Verwaltung.** Erst ab hier ist der Mechanismus ohne Host-Code
   bedienbar.
5. **Ad-hoc-Tickets.** Zweiter Marker, Nutzlast, Gueltigkeitsregel, Sperrliste.
6. **Protokoll.** Tabelle, Einstiege + Verweigerungen, Ansicht.
7. **Leitfaden.**

Phase 1 und 2 sind unabhaengig nutzbar; ab Phase 3 wird es sicherheitsrelevant, ab Phase 4 alltags-
tauglich.

## 11. Offene Punkte

- **Brauchen Vorlagen mit Konsumenten die Regexe noch?** Gefuehl: ja, fuer Unterressourcen ohne
  Argumente. Am ersten echten Fall besser zu beantworten als am Reissbrett.
- **Wie genau meldet sich ein Endpunkt?** Konstruktor-Injektion ist gesetzt; wieviel sich aus den
  Endpoint-Metadaten ableiten laesst, entscheidet sich beim Bauen.
- **Weitergabe**: darf ein Empfaenger weiterteilen? Heute faktisch nein, weil er die Maske nicht hat.
  Mit Phase 4 wird das eine Entscheidung und keine Nebenwirkung mehr.
- **Mehrere Argumente, teilweise erfuellt** — reicht die Bestaetigung eines Arguments, wenn die
  Vorlage zwei kennt? Vorschlag: alle Pflichtargumente, sonst keine Auslieferung.

## 12. Referenzen

- `docs/Plan-SharedAsset-Pfadkontext.md` — die Grundlage; dieser Plan baut auf dessen
  `ISharedAssetContext` und dem Pfad-Abschnitt auf.
- `docs/Migration-Future_10-MLM.md` Abschnitt 50 — was der Host fuer den Pfad-Kontext tun musste.
- `SharedAssetInfoProvider.IsTemplateValidForPath` — die Stelle, an der heute allein die Regexe
  entscheiden.
