# BUG (PRE228): Die allgemeine Wertwandlung beim Config-Import kennt weder Aufzählungen noch eine Kultur

> Gemeldet aus **MLMManager** (Blazor Server/WASM-Auto, Toolkit `5.0.0-PRE228`, .NET 10). Gefunden
> beim Bau einer eigenen `IConfigExtension` für die CustomerCare-Stammdaten — also beim Lesen von
> `BillingConfigExtension` und `HelpConfigExtension` als Vorbild. Die beiden Befunde unten sind am
> .NET-Laufzeitverhalten **nachgemessen** (Ausgaben weiter unten), nicht am laufenden Import
> beobachtet: MLM hat den Fehler durch eigene Ausdrücke umgangen, bevor er zuschlagen konnte.

## Der Kern in zwei Sätzen

Ein `ChangeDetail` **ohne** `ValueExpression` wird mit `Entity.{Prop}=ChangeType(NewValueRaw,Type)`
eingespielt. Dahinter steht am Ende `System.Convert.ChangeType(value, targetType)` — **ohne
`IFormatProvider`**, also unter der Kultur des einspielenden Benutzers, und **ohne jede Behandlung
von Aufzählungstypen**, auf denen es schlicht wirft.

Das trifft zwei mitgelieferte Sektionen:

| | Stelle | Folge |
|---|---|---|
| **A — Kultur** | `BillingConfigExtension.cs:249,261,332,344` (`Amount`) | Unter einer deutschen Oberfläche wird aus dem Preis `12.50` **still die Zahl 1250**. Kein Fehler, keine Meldung, falsche Preisliste. |
| **B — Aufzählung** | `HelpConfigExtension.cs:337,364,503,523` (`Kind`) | `Convert.ChangeType` wirft. Das reisst den **ganzen Change** mit (`throw;` in der Detail-Schleife) → das Thema bzw. die Ressource wird **gar nicht angelegt**. Da jeder Themen-Insert ein `Kind` trägt, heisst das: **ein Hilfe-Import legt kein einziges Thema an.** |

## Messung

Direkt gegen die .NET-Laufzeit, dieselben Aufrufe, die `TypeConverter.Convert` am Ende macht:

```
--- Kultur: de-DE
--- decimal aus '12.50' via Convert.ChangeType(value,type):
    Ergebnis: 1250
--- dasselbe unter de-CH:
    Ergebnis: 12.50
--- dasselbe unter 'de' (neutral):
    Ergebnis: 1250
--- Enum aus String via Convert.ChangeType(value,type):
    WURF: Ungültige Umwandlung von "System.String" in "System.DayOfWeek".
--- Enum aus Int via Convert.ChangeType(value,type):
    WURF: Ungültige Umwandlung von "System.Int32" in "System.DayOfWeek".
```

Zwei Dinge daran sind bemerkenswert:

**Die Kultur-Falle versteckt sich ausgerechnet in der Schweiz.** `de-CH` trennt Dezimalstellen mit
dem Punkt — dort geht `12.50` durch, und zwar richtig. `de-DE` und die **neutrale** Kultur `de`
trennen mit dem Komma und lesen den Punkt als Tausendertrennzeichen: `12.50` wird zu `1250`. Ein
Haus, das auf `de-CH` entwickelt und auf `de` ausliefert (oder umgekehrt), sieht den Fehler nie an
der Stelle, an der er entsteht. MLMs Vorgabekultur ist **`de`**, nicht `de-CH`.

**Die Aufzählungs-Falle wirft auch aus einem `int`.** Es genügt also nicht, den Wert als Zahl statt
als Namen zu schicken — `Convert.ChangeType` weigert sich auf einem Aufzählungs-Zieltyp in beide
Richtungen.

## Root Cause

`SimpleDataApplyer.cs:131-133` setzt den Vorgabe-Ausdruck:

```csharp
var xp = string.IsNullOrEmpty(detail.ValueExpression)
    ? $"Entity.{detail.TargetProp}=ChangeType(NewValueRaw,Type)"
    : detail.ValueExpression;
```

`ChangeType` ist die externe Methode `DefaultCallbacks.Convert` und landet in
`ITVComponents/TypeConversion/TypeConverter.cs:23-39`:

```csharp
public static object Convert(object value, Type targetType)
{
    …
    var converters = Snapshot();
    var converter = converters.FirstOrDefault(n => n.CapableFor(value, targetType));
    object result = null;
    if (converter?.TryConvert(value, targetType, out result) ?? false)
    {
        return result;
    }

    return System.Convert.ChangeType(value, targetType);   // ← keine Kultur, kein Enum
}
```

Der Wandler-Stapel ist dabei **leer**: eine Suche nach `TypeConversionProvider`-Ableitungen über das
ganze Repository findet ausser dem Vertrag selbst keine einzige Umsetzung, und
`RegisterConverter` wird nirgends gerufen. Der `FirstOrDefault` ist damit heute immer `null`, und
jede Wandlung fällt auf die letzte Zeile durch.

Zum Vergleich: `MethodHelper.cs:512-514` — die Argument-Anpassung beim Methodenaufruf — macht
beides richtig:

```csharp
capableArguments[i] = effective.IsEnum
    ? Enum.ToObject(effective, value)
    : Convert.ChangeType(value, effective, CultureInfo.InvariantCulture);
```

Dieselbe Skript-Umgebung, dieselbe Fragestellung, die richtige Antwort — nur eben nicht auf dem Weg,
den der Config-Import nimmt.

## Warum es bisher niemandem aufgefallen ist

Beide Befunde sind **leise**, und das ist der eigentliche Schaden:

- **A** meldet gar nichts. `1250` ist eine gültige Zahl, der Change gilt als angewendet, der Import
  meldet Erfolg. Auffallen kann das erst jemandem, der hinterher die Preisliste liest.
- **B** ist laut, aber an der falschen Stelle. Die Detail-Schleife wirft weiter (`throw;`,
  `SimpleDataApplyer.cs:151`), der äussere Griff zählt den Change als übersprungen und **löst die
  halbfertige Entität aus dem Änderungsnachweis**. Es fehlt also nicht ein Feld — es fehlt der ganze
  Datensatz.

  Für die Hilfe-Sektion heisst das, so wie ich den Ablauf lese: **jeder** Themen-Insert trägt ein
  `Kind` (Zeile 337), also scheitert **jeder** Themen-Insert. Die Inhalte dazu
  (`HelpTopicContents`) lösen ihr Thema über `MakeLinqAssign` auf, finden es nicht und scheitern
  hinterher. Im Ergebnisprotokoll steht das als „Skipped N failing change(s) on Entity HelpTopics",
  der eigentliche Grund nur im Log — und zwar als `LogDebugEvent`.

  ⚠️ Das ist die Stelle, an der meine Analyse am weitesten über das Gemessene hinausgeht: den
  .NET-Wurf habe ich nachgemessen, den Weg von dort bis zum übersprungenen Change **gelesen, nicht
  laufen sehen**. Ein Hilfe-Import auf einem Zielsystem ohne Themen sollte das in einem Zug
  bestätigen oder widerlegen.

## Vorschlag

Der Reihe nach von „am wenigsten invasiv" bis „am gründlichsten" — die Wahl gehört dem Toolkit, das
den Rattenschwanz besser überblickt:

1. **Nur den Vorgabe-Ausdruck reparieren** (`SimpleDataApplyer`), etwa über eine eigene externe
   Methode `ChangeTypeInvariant`, die genau das tut, was `MethodHelper` schon tut: Aufzählungen über
   `Enum.ToObject`, alles andere über `Convert.ChangeType(…, CultureInfo.InvariantCulture)`. Das ist
   die kleinste Änderung und trifft den Fehler dort, wo er entsteht.
   ⚠️ Zu bedenken: bestehende Dateien, die Zahlen **kultur-abhängig** geschrieben haben, würden
   danach anders gelesen. Nach allem, was ich sehe, schreibt aber jede mitgelieferte Sektion bereits
   invariant (`Money`, `ToString(CultureInfo.InvariantCulture)`) — die Schieflage sitzt nur auf der
   Lese-Seite.
2. **`TypeConverter.Convert` selbst** um dieselben zwei Fälle ergänzen. Wirkt weiter als nötig (die
   Methode wird auch anderswo gerufen), wäre dafür aber an genau einer Stelle richtig.
3. **Die beiden Sektionen einzeln** mit expliziten `ValueExpression` versehen. Behebt die zwei
   bekannten Fälle, lässt die Falle aber für jede künftige Extension stehen — und die nächste tritt
   hinein, weil der Vorgabe-Ausdruck genau so aussieht, als würde er das Richtige tun.

Unabhängig von der Wahl wäre ein **Durchgang durch alle `MakeDetail`-Aufrufe ohne
`ValueExpression`** sinnvoll: alles, was keine Zeichenkette ist, steht unter Verdacht. Bools sind
durchgängig sauber gelöst (`Entity.X=(NewValueRaw=="True")`, an allen Stellen), ganze Zahlen sind
unkritisch (ziffernweise kulturunabhängig, solange kein Gruppentrennzeichen mitgeschrieben wird) —
übrig bleiben Dezimalzahlen, Aufzählungen und Zeitpunkte. Für Zeitpunkte hat die Hilfe-Sektion die
Falle schon einmal einzeln umschifft (`HelpConfigExtension.cs:638-642`, mit einem Kommentar, der
genau dieses Problem beschreibt: „parses with the current culture and hands back Kind=Local") — ein
Hinweis darauf, dass der allgemeine Weg hier schon einmal nicht getragen hat.

## Was MLM getan hat

Die CustomerCare-Sektion umgeht beides mit eigenen Ausdrücken — beide ebenfalls nachgemessen:

```csharp
// Beträge: invariant geschrieben UND invariant gelesen.
$"Entity.{property}='System.Decimal'.Parse(NewValueRaw,'System.Globalization.CultureInfo'.InvariantCulture)"

// Aufzählungen: reisen als Zahl, zurück über Enum.ToObject.
// 'Type' ist die Kontext-Variable, die der Applyer ohnehin mit der Zieleigenschaft füllt —
// damit stimmt der Typ immer, ohne Typnamen im Ausdruck.
$"Entity.{property}='System.Enum'.ToObject(Type,ChangeType(NewValueRaw,'System.Int32'))"
```

Der zweite Ausdruck ist nebenbei ein Hinweis darauf, warum die naheliegende Lösung nicht trägt: den
Aufzählungstyp als Typ-Literal in den Ausdruck zu schreiben scheidet aus, weil
`TypeLiteralNode.Resolve` ohne Assembly-Angabe über `Type.GetType(fullName)` geht und damit nur
findet, was im Kern der Laufzeit liegt. Die Assembly-Form `'X.Y@@"Z.dll"'` gäbe es zwar, macht den
Ausdruck aber von einem Dateinamen abhängig — für eine Konfigurationsdatei, die zwischen Systemen
reist, die schlechtere Wahl.

## Abgrenzung

- **Nicht betroffen:** alles mit ausdrücklichem `ValueExpression`. Die Bool-Felder sind durchgängig
  so gelöst, ebenso die Fremdschlüssel über `MakeLinqAssign`.
- **Nicht betroffen:** ganze Zahlen (`SortOrder`, `TrialDays`) — solange sie ohne
  Gruppentrennzeichen geschrieben werden, und das tun sie.
- **Nicht der Grund** für irgendeinen bisher gemeldeten Fehlschlag: beide Befunde führen zu einem
  *erfolgreichen* Import mit falschem Inhalt, nicht zu einem Abbruch.
- **`IConfigExtensionContext` aus PRE228 selbst ist davon unberührt** und funktioniert wie
  beschrieben — der Befund hier liegt eine Schicht tiefer, im allgemeinen Einspiel-Werk, und ist
  genauso alt wie dieses.

---

## Behebung (PRE229)

Umgesetzt wurde **Vorschlag 1** — der Fehler wird dort behoben, wo er entsteht, und `TypeConverter`
bleibt unangetastet: er hängt an praktisch allem in ITVComponents (Datenzugriff, IPC-Proxies,
Grid-Filter, Workflow-Werte), und ein geändertes Zahlen- oder Datums-Parsen dort wäre eine
Verhaltensänderung mit sehr viel grösserem Radius als der Config-Import.

Neu: **`ITVComponents.EFRepo/DataSync/ChangeValueConverter.cs`**. Der Vorgabe-Ausdruck des Applyers
heisst jetzt `Entity.{Prop}=NewValue`, und `NewValue` wird **in C# gewandelt**, bevor der Ausdruck
läuft (`SimpleDataApplyer.cs`). Damit fällt der Umweg über die Skript-Umgebung für den Normalfall
ganz weg — und mit ihm die Kultur des einspielenden Benutzers.

Der Wandler liest so, wie die Sektionen schreiben:

- **`Nullable<T>` zuerst ausgepackt.** Leerer Text heisst auf einem nullbaren oder Referenz-Ziel
  „kein Wert"; auf einem nicht-nullbaren Werttyp gibt es dafür eine klare `FormatException` statt
  einer beliebigen Parse-Meldung. (`Convert.ChangeType` warf auf `decimal?` bisher genauso wie auf
  einen Aufzählungstyp — der nullbare Fall stand im Report nicht, war aber gleich kaputt.)
- **Aufzählungen** über `Enum.Parse(…, ignoreCase: true)` — das nimmt **beide** Schreibweisen, die
  ein Payload verwenden kann: den Namen *und* die Zahl. Die mitgelieferten Sektionen schreiben den
  Namen (`Kind.ToString()`), MLMs CustomerCare-Sektion die Zahl; beide gehen jetzt durch dieselbe
  Vorgabe. Der eigene Ausdruck in CustomerCare wird damit unnötig, schadet aber nicht.
- **Zahlen** invariant (`CultureInfo.InvariantCulture`) — `12.50` bleibt `12.50`, unter `de`, `de-DE`
  und `de-CH` gleichermassen.
- **Zeitpunkte** invariant **und immer UTC**: `AssumeUniversal | AdjustToUniversal`. Ein Stempel mit
  `Z` oder mit Offset kommt als `Kind=Utc` zurück, einer ohne Zonenangabe wird als UTC gelesen statt
  in die Ortszeit geschoben. `Kind=Local` entsteht gar nicht mehr — genau das, woran Npgsql auf
  `timestamp with time zone` bisher scheiterte.
- **Typen ohne `IConvertible`**, auf denen `Convert.ChangeType` grundsätzlich wirft und die deshalb
  bisher jeden Change mitgerissen hätten: `Guid`, `TimeSpan`, `DateOnly`, `TimeOnly`, `byte[]`
  (Base64), `Uri`, `Version`.

**Zur Behauptung „der Wandler-Stapel ist leer":** nicht ganz — `ITVComponents.Plugins` bringt
`EnumConverter`, `GuidConverter` und `NullableConverter` mit. Sie registrieren sich aber erst, wenn
der Host sie als **Plugin instanziiert**; ein Blazor-Host tut das in aller Regel nicht. Der Befund
stimmt also im Ergebnis, und die Korrektur macht den Import bewusst **unabhängig** davon, ob
irgendwo Wandler registriert sind.

**Zweiter Teil der Korrektur — der Fehlschlag ist nicht mehr unsichtbar.** Die Zuweisungs-Schleife
meldete ihren Fehler über `LogDebugEvent`, und das ist hinter `debugListening` verriegelt: bei einem
gewöhnlichen Host landete **gar nichts** im Log, während die äussere Schleife den ganzen Datensatz
verwarf. Jetzt geht die Meldung über `LogEnvironment.LogEvent(…, LogSeverity.Error)` **und** in das
Einspiel-Protokoll, mit Eigenschaft, Ziel-Typ und Entität — die Zeile
„Skipped N failing change(s)" hat damit endlich einen Grund neben sich stehen.

**Nicht geändert:** die Sektionen selbst. `BillingConfigExtension` und `HelpConfigExtension` bleiben
wie sie sind — mit der reparierten Vorgabe sind `Amount` und `Kind` von selbst richtig. Die
bestehenden ausdrücklichen Ausdrücke (`BoolAssign`, `FromBase64String`, `Created=UtcNow`,
`MakeLinqAssign`) laufen unverändert weiter; sie sind jetzt nur nicht mehr nötig, um der Falle
auszuweichen. Der vorgeschlagene Durchgang durch alle `MakeDetail`-Aufrufe ohne `ValueExpression`
erübrigt sich damit: Dezimalzahlen, Aufzählungen und Zeitpunkte trägt der Vorgabeweg jetzt.

**Offen geblieben:** `ExpressionBuilder.BuildExpression` (der Weg, über den `Key`-Werte beim
Update/Delete aufgelöst werden) ruft weiterhin `TypeConverter.Convert`. Ein Schlüssel auf einer
Aufzählung oder einer Dezimalzahl hätte dort dasselbe Problem. Alle mitgelieferten Sektionen
schlüsseln über Text oder über `KeyExpression`, deshalb wurde das hier nicht mitgezogen — derselbe
Weg bedient nämlich auch die Grid-Filter der Oberfläche, wo Benutzer ihre Werte **kultur-abhängig**
eintippen, und genau dort wäre „invariant" die falsche Antwort.

**Test:** `ITVComponents.EFRepo.Test/ChangeValueConverterTest.cs` — sechs Fälle, die ganze Klasse
läuft unter `CultureInfo("de")`, also unter der Kultur, in der der Fehler zuschlägt. 10 Tests wurden
16, alle grün.
