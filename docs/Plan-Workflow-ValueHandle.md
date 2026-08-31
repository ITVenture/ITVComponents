# Plan: ValueHandle — grosse und fremde Daten im Workflow, ohne sie zu tragen

Status: **umgesetzt** (Phasen 1-6), Stand 2026-08-31, Zweig Future_10.
Loest den frueheren Entwurf „externe Daten ueber einen ObjectProvider" ab (nie geschrieben, nur als
Notiz gefuehrt) — die Begruendung steht in Abschnitt 1.

> **Zwei Korrekturen am Entwurf, die beim Bauen fielen.** Sie stehen hier und nicht nur im Code, weil der
> Plan sonst falsch bliebe:
>
> 1. **`:2427` ist nicht die Aktivitaet, sondern `SendMessage`** — und dessen Payload wird kopiert, in
>    der Outbox abgelegt und beim Empfaenger zu dessen Variablen. Die Stelle ist damit **verboten**, nicht
>    erlaubt: es sind **vier** verbotene Wege, nicht drei. Die Aktivitaet ist `:3968` (`RunActivity`).
> 2. **Das erste Pfad-Segment darf nicht ueber den Member-Zugriff laufen.** Der Payload ist ein
>    `Dictionary<string,object>`; `MemberAccessor` faende dort zuerst ein gleichnamiges *Member*, und ein
>    Parameter namens `Count` oder `Keys` liefe still auf die Zahl der Eintraege. Der Payload-Schluessel
>    wird deshalb direkt nachgeschlagen, erst der Rest ist Pfad.
>
> Und eine Behauptung, die nicht stimmte: der Lesepfad packt **kein** `SmartProperty` aus (§3.4). Das
> steckt im `Scope`, nicht im Member-Zugriff; die echte `ExtendedFormatting`-Abhaengigkeit des Helpers ist
> `IBasicKeyValueProvider` — die ist mitgewandert.

## 1. Ausgangslage

Ein Workflow, der mit fremden oder grossen Daten hantiert, hat heute zwei Moeglichkeiten: er zieht sie
in den Variablen-Stack (und traegt sie damit in jedem Instanz-Blob, jedem History-Eintrag und jedem
Archiv-Datensatz mit), oder er merkt sich einen Schluessel und jede Aktivitaet laedt selbst — mit dem
Ergebnis, dass „wie kommt man an den Auftrag" in jeder Aktivitaet noch einmal steht.

### 1.1 Was verworfen wurde

Der erste Entwurf war ein **Objekt-Hub**: die Variablen tragen nur noch Verweise, die echten Daten
liegen beim Hub, und die Engine meldet ihm ihren Lebenszyklus (Aktivitaet beginnt, Split, Join, Zweig
aufgegeben, Instanz beendet, Wiederaufsatz, Kompensation). Verworfen aus zwei Gruenden:

- **Die Engine haette Ressourcen sperren muessen.** Sobald zwei Zweige denselben Arbeitsbereich
  benutzen, wandert die Zustaendigkeit fuer Sperren in die Workflow-Engine — also die Verantwortung
  fuer Daten, von denen sie nichts versteht.
- **„Zweig aufgegeben" war das teuerste Ereignis.** Ein Zweig endet nicht immer im Join: ein Fault
  anderswo, ein unterbrechender Fristen-Timer, ein Terminate-Ende. Ohne dieses Ereignis leakt der Hub
  Arbeitsbereiche — und es zuverlaessig zu feuern kostet mehr als der ganze Rest.

Der hier beschriebene Entwurf hat **keinen Zustand beim Provider**. Damit faellt die ganze
Ereignisschicht ersatzlos weg, und mit ihr das Sperr-Thema.

### 1.2 Die Idee

Eine **Bindung** kann sagen: „dieser Eingabeparameter kommt nicht aus einer Variablen, sondern von
einem Handler". Der Handler bekommt Argumente aus dem Variablen-Stack und liefert einen Wert. Die
Engine verpackt ihn in einen **`ValueHandle`**: lesen, aendern, und mit `WriteBack()` zurueckschreiben.

Der Stack traegt weiterhin nur Kleinkram — Schluessel, Nummern, Entscheidungen. Was gross oder fremd
ist, wird geholt, wenn es gebraucht wird, und geschrieben, wenn es sich geaendert hat.

## 2. Entscheidungen

| Frage | Entscheidung |
|---|---|
| Name | Bindungsart `ParameterBindingKind.ValueHandle`, Objekt `ValueHandle`, Vertrag `IWorkflowValueHandler` |
| Wer baut den Griff | die **Engine**. Der Handler kann nur lesen und schreiben |
| Was die Aktivitaet sieht | je Bindung: den **Griff** (Automated) oder den **ausgepackten Wert** (User-Task, mit konfiguriertem Auto-Write) |
| Argumente des Handlers | mindestens eines, ueber dieselbe Bindungs-Maschinerie (Literal/Variable/Expression, keine Rekursion) |
| Griff im Variablen-Stack | **verboten**, faultend — Validator, Laufzeit-Riegel und Serialisierung |
| Parallele Regionen | Vorgabe: verboten; per Schalter an der Bindung erzwingbar |
| Property-Pfade | ein Zugriff fuer Lesen **und** Schreiben, ausgelagert aus dem CScript-Member-Zugriff |
| Korrektheit der Daten | Sache des Handlers. Die Engine schreibt einmal, verschweigt nichts und erfindet nichts |

### 2.1 Warum der Griff der Engine gehoert

Der Handler koennte den Griff selbst bauen — dann haette jede Implementierung ihr eigenes
Backing-Feld, ihre eigene WriteBack-Semantik, ihre eigene Vorstellung davon, was „schreiben ohne
vorher gelesen zu haben" bedeutet, und ihr eigenes (oder gar kein) Protokoll. Also:

```csharp
public interface IWorkflowValueHandler : IPlugin
{
    object Read(ValueHandleRequest request);
    void Write(ValueHandleRequest request, object value);
}
```

Aufgeloest ueber den `PluginName` in einem eigenen Plugin-Scope — **das Muster steht bereits**:
`PluginActivityCatalog` macht es fuer `IValuesProvider` genauso (`:79-103`), inklusive Freigabe des
Scopes danach.

> **Nicht verwechseln:** `IValuesProvider` liefert die **Auswahlwerte** eines Parameters im Designer.
> Mit diesem Plan hat er nichts zu tun ausser dem Aufloesungsmuster.

### 2.2 Warum je Bindung Griff **oder** Wert

Aktivitaeten deklarieren typisierte Parameter. Eine Plugin-Aktivitaet, die ein `Order` erwartet,
bekaeme mit einem `ValueHandle` einen Typfehler statt eines Auftrags. Deshalb sagt die Bindung, was
ankommt:

- **`Handle`** — die Aktivitaet kennt den Mechanismus. Sie liest `.Value`, aendert, ruft `WriteBack()`,
  wann sie will. Der Fall fuer selbstgeschriebene Worker: volle Kontrolle darueber, *was* und *wann*.
- **`Value`** — die Aktivitaet bekommt den ausgepackten Wert und weiss von nichts. Die Bindung traegt
  zusaetzlich `WriteBack: Never | OnSuccess`. Bei **Referenztypen** ist das der eigentliche Gewinn: die
  Aktivitaet mutiert das Objekt, das sie bekommen hat — dasselbe, das im Backing-Feld liegt — und die
  Engine schreibt es danach zurueck. Der Mechanismus laeuft damit auch mit Aktivitaeten, die nie dafuer
  geschrieben wurden.

Bei Werttypen und Zeichenketten ist `Value` + `WriteBack` sinnlos (eine Aenderung waere unsichtbar):
Validator-Warnung, und der Griff muss durch.

**Die Benutzer-Aufgabe bekommt den Griff nie zu sehen.** Ihr Payload traegt den ausgepackten Wert —
damit adressieren die Feld-Pfade das echte Objekt, und der Griff landet nicht im Blazor-Circuit, wo er
einen Reconnect ohnehin nicht ueberlebte.

## 3. Phase 1: den Member-Zugriff auslagern

Der Kern, den dieser Plan braucht, existiert seit Jahren — im Interpreter.
`ITVComponents.Scripting.CScript/Helpers/MemberAccessHelper.cs` (387 Zeilen, `internal static`)
beherrscht `GetMemberValue` (`:33`), `SetMemberValue` (`:255`) und `FindMember` (`:331`). Der AST-Knoten
`MemberAccessNode` und `MemberAccessValue` sind nur die Verpackung; `a.b.c` ist dort eine Kaskade von
Knoten, deren letzter einen schreibfaehigen Wert liefert.

**Was schon beidseitig geht:** Property (`CanWrite`), Field (nicht literal), `IDictionary<string,object>`
(anlegen und ueberschreiben), `ObjectLiteral`, `FunctionLiteral`, Event-Anmeldung. Lesend zusaetzlich:
statische Member, Enum-Werte, `$Type`, Methoden/Konstruktoren ueber die Lazy-Executors.

**Das Null-Verhalten ist dort schon eine benannte Entscheidung:** `FindMember` wirft auf einer
Null-Basis, `WeakReferenceMemberAccessValue` ist die `?.`-Variante, die null liefert. Genau die Regel,
die dieser Plan fuer Pfade braucht (lesen: durchreichen, schreiben: Fehler) — schon vorhanden.

### 3.1 Der Schnitt

| in den geteilten Kern | bleibt Skript-Semantik |
|---|---|
| `FindMember`, Property/Field/Dictionary lesen **und** schreiben | `ObjectLiteral`, `FunctionLiteral`, Event-`+=` |
| Existenzpruefung (`MemberAccessMode.CheckExists`) | `$Type` / `instanceSemantics`, Method/Constructor, LazyExecutors |
| Null-Verhalten streng vs. schwach | `ScriptException` als Fehlerform |
| `SmartProperty` (liegt ohnehin in `ITVComponents.ExtendedFormatting`) | |

Die Kopplung, die die Bauform bestimmt, ist **`ScriptingPolicy`**: sie darf nicht wegfallen (daran
haengen `InterpreterSecurityTest` und `SecurityTests`) und gehoert nicht in die Basis.

### 3.2 Neu in `ITVComponents` (neben `ExtendedFormatting`)

- **`MemberSlot`** — Ziel + Member, `Read()` / `Write(value)` / `Exists()`. Das ist `MemberAccessValue`
  ohne Interpreter: kein Locking, kein Inline-Cache, kein Disposal.
- **`MemberAccessor`** — der ausgelagerte Kern von Get/Set/FindMember.
- **`MemberPath`** — die Kaskade: `a.b.c` loest n−1 Segmente lesend auf (schwach) und liefert das
  letzte als Slot (streng beim Schreiben).
- **`IMemberAccessGuard`** — Vorgabe: alles erlaubt. CScript implementiert ihn ueber `ScriptingPolicy`.
- **`IMemberSource`** — Haken fuer fremde Traeger (`ObjectLiteral` und Verwandte).

`MemberAccessHelper` behaelt seine Skript-Zweige und **delegiert die Matrix an den Kern**.

### 3.3 Reihenfolge und Netz

Die Extraktion geschieht **mit sofortiger Delegation**, nicht als Kopie fuer den Workflow. Eine zweite
Implementierung hiesse, dass `user.Address.Street` im Skript und in der Maske auseinanderlaufen koennen,
ohne dass es jemand merkt. Das Netz ist die vorhandene CScript-Test-Suite (10 Dateien, darunter die
beiden Security-Tests) — sie ist der einzige Beweis, dass die Auslagerung wortgetreu ist.

**Nicht angefasst:** `ScriptValue` (710 Zeilen Interpreter-Maschinerie) und der AST-Knoten samt
Cache-Slot.

### 3.4 Zwei Verhaltensweisen, die uebernommen und nicht „repariert" werden

- **SmartProperty-Auspacken im Lesepfad.** Der Helper importiert `ExtendedFormatting`; der Kern muss
  das mitnehmen, sonst verhaelt sich der Workflow anders als das Skript.
- **`FindMember` nimmt `GetMembers(...).FirstOrDefault()`.** Bei Ueberladungen oder Shadowing ist das
  nicht deterministisch. Verhalten beibehalten, im Code dokumentieren: es zu aendern hiesse,
  Skript-Semantik zu aendern.

## 4. Datenmodell

### 4.1 Bindung

```
ParameterBindingKind        + ValueHandle          (ans ENDE, siehe 9.1)

ActivityInputBinding        + HandlerName          (Plugin-Name des IWorkflowValueHandler)
                            + HandlerArguments     (List<ActivityInputBinding>, mind. 1)
                            + Delivery             (Handle | Value)
                            + WriteBack            (Never | OnSuccess)
                            + AllowInParallelRegion (bool, Vorgabe false)
```

### 4.2 Benutzer-Aufgabe

```
UserActivityNode            + WriteBackParameters  (List<string> - welche Eingabe-Bindungen)
UserTaskField.PayloadName   wird zum PFAD ("user.FirstName")
```

**Die Deklaration steht damit nur einmal da.** Der Feld-Pfad ist Lese- *und* Schreibziel; der Knoten
sagt nur, welche Parameter ueberhaupt zurueckgeschrieben werden. Keine zweite Mapping-Tabelle, und
„was ich sehe, schreibe ich zurueck" ist strukturell wahr statt Pflegedisziplin.

**Rueckwaertskompatibel:** erst den Schluessel **exakt** im Payload suchen, erst dann als Pfad deuten —
sonst braeche ein Bestandsschluessel, der einen Punkt enthaelt.

### 4.3 Der Griff

```csharp
public sealed class ValueHandle
{
    public object Value { get; set; }   // Backing-Feld
    public void WriteBack();            // schreibt Value ueber den Handler
    public bool Written { get; }
}
```

`WriteBack()` ohne vorheriges Lesen ist ein Fehler; zweimaliges Schreiben ist erlaubt (der Handler
entscheidet, was das heisst), wird aber protokolliert.

## 5. Zur Laufzeit

### 5.1 Aufloesung

In `ResolveInputs` (`WorkflowEngine.cs:4609`): Handler ueber `HandlerName` im Plugin-Scope aufloesen,
`HandlerArguments` mit derselben Methode aufloesen, `Read` rufen, Griff bauen. Je nach `Delivery`
kommt der Griff oder `handle.Value` in den Payload.

### 5.2 Automated

Die Aktivitaet laeuft **ausserhalb** des Commit-Delegaten — dort darf sie das auch. Bei
`Delivery = Handle` schreibt sie selbst; bei `Value` + `WriteBack = OnSuccess` schreibt die Engine
nach erfolgreicher Ausfuehrung, vor dem Uebernehmen der Ausgaben.

### 5.3 Benutzer-Aufgabe — der Zeitpunkt ist die eigentliche Entscheidung

Beim Parken wird aufgeloest (fuer die Maske) und beim Abschluss **erneut** — der Griff ueberlebt die
Persistierung nicht. Damit beim Abschluss dasselbe Objekt gemeint ist, das der Mensch gesehen hat,
werden die **aufgeloesten Argumente beim Parken am Token festgeschrieben** — genau wie `Assignment` es
heute schon tut („einmal beim Parken ausgewertet und am Token festgeschrieben").

Geschrieben wird **im Commit-Delegaten von `CompleteUserTask` (`:3646`), als letzter Schritt nach allen
Pruefungen** — mit einem **Einmal-Riegel**.

Der Grund ist ein Befund: saemtliche Gueltigkeitspruefungen (`MayResumeOnEvent`, „gibt es das Token
noch", „steht es noch auf `Waiting`", „ist der Knoten eine Benutzer-Aufgabe") liegen **innerhalb** des
Delegaten. Ausserhalb weiss niemand, ob die Aufgabe noch existiert — ein unterbrechender Fristen-Timer
kann das Token laengst auf den Eskalationspfad geschoben haben. Vorher schreiben hiesse: schreiben und
danach erfahren, dass die Aufgabe weg war. Nachher schreiben hiesse: Aufgabe zu, Vorgang weitergelaufen,
Schreibfehler zu spaet.

Der Delegat kann bei einem Versionskonflikt erneut laufen — deshalb der Riegel: der Handler schreibt
**genau einmal**.

Ablauf im Delegaten: Pruefungen → Pfade in das ausgepackte Objekt setzen → **ein** `WriteBack()` je
Griff (nicht je Feld) → Ausgaben auf Variablen → Commit.

**Geschrieben wird in ein frisch gelesenes Objekt.** `DescribeUserTask` (`:3812`) loest den Payload bei
jedem Aufruf neu auf; der Abschluss holt also nicht das, was die Maske gesehen hat, sondern den
aktuellen Stand — und setzt darauf nur die Pfade, die tatsaechlich in der Maske stehen. Hat jemand
anders in der Zwischenzeit ein **anderes** Feld desselben Datensatzes geaendert, bleibt es erhalten.
Das ist die schonendste Variante, die ohne Mitwirkung des Handlers zu haben ist (ein
Lost-Update-Schutz auf Feldebene ist es nicht — dafuer braeuchte es Versionsstempel vom Handler).

### 5.4 Die zwei Restfaelle

- **Konflikt-Wiederlauf, der die Pruefung nicht mehr besteht:** geschrieben ist geschrieben, die
  Aufgabe gilt als `AlreadyCompleted`. Selten — und ein `LogError` mit allen Koordinaten, weil man
  diesen Zustand nur mit einer Logzeile wieder geradebiegt.
- **Der Schreibfehler selbst** (Handler wirft, Plugin fehlt): der Commit laeuft nicht, die **Aufgabe
  bleibt offen**, der Mensch bekommt die Meldung. Dafuer gibt es `UserTaskViewResult.Incomplete`
  bereits. Das ist die einzige Variante, in der niemand Eingaben verliert.

### 5.5 Pfade

Gelesen und geschrieben wird ueber `MemberPath` aus Phase 1 — **mit derselben `ScriptingPolicy`**, die
`CScriptExpressionEvaluator` ohnehin bekommt. Sonst gaebe es zwei Sicherheitsniveaus fuer dieselbe
Frage: `user.Password` als Ausdruck verboten, als Masken-Pfad erlaubt.

Regeln: Null in der Mitte → lesend leer, schreibend Fehler (die Engine kennt den Typ nicht und darf
kein `Address` erfinden). Kein Setter → Laufzeitfehler mit Pfad und Typ im Text; statisch nicht
pruefbar, weil der Typ erst zur Laufzeit feststeht. Konvertierung ueber `ITVComponents.TypeConversion`.

## 6. Die Riegel: kein Griff im Variablen-Stack

Das ist die Regel, die den Mechanismus zusammenhaelt — und sie ist groesser, als sie klingt.
`ResolveInputs` ist **eine** Methode mit **sechs** Aufrufstellen, und drei davon schreiben ihr Ergebnis
in Variablen:

| Aufrufstelle | Ziel | ValueHandle erlaubt |
|---|---|---|
| Aktivitaet (`:2427`, `:3968`) | Payload, nie persistiert | ja |
| Benutzer-Aufgabe (`:3812`) | Masken-Payload | ja (ausgepackt) |
| Start-Signatur (`:4777`) | `instance.Variables` | **nein** |
| Subworkflow-Aufruf (`:4921`) | Variablen der Kind-Instanz | **nein** |
| Kanten-Mapping (`:5937`) | Zweig-Scope | **nein** |

Ueber die drei unteren Wege landete ein Griff im Stack, **ohne dass je eine Ausgabe-Bindung im Spiel
war** — und nach dem naechsten Neustart waere er ein toter Verweis. Deshalb dreifach:

1. **Validator** (Entwurfszeit): die Art an diesen drei Stellen und in Ausgabe-Bindungen ablehnen.
2. **Laufzeit**: `ResolveInputs` bekommt ein Kontext-Flag „dieser Aufruf fuellt Variablen" und faultet.
3. **Serialisierung**: letzter Riegel in `WorkflowJson.SerializeVariables`.

**Faultend, nicht als Log-Zeile.** Still weiterlaufen hiesse, dass die naechsten Schritte mit einem
Griff arbeiten, dessen Tod sich spaeter nicht mehr rekonstruieren laesst.

### 6.1 Parallele Regionen

Zwei Zweige, derselbe Handler mit denselben Argumenten: der letzte gewinnt, und der Zweig-Commit sieht
es nicht — `FindWriteConflict` vergleicht Werte aus dem Blob, und dort steht bei einer
ValueHandle-Bindung nichts. Vorgabe deshalb: **Validator-Fehler**, wenn eine Bindung mit `WriteBack` in
einer parallelen Region steht. Mit `AllowInParallelRegion = true` wird daraus eine Warnung — die
Verantwortung liegt dann beim Handler (Versionsstempel).

## 7. Retry und Kompensation

**Retry** ist im Graphen modelliert (`ErrorFlowId` zurueck auf denselben Knoten, Zaehler ueber
`AttemptVariable`) — es gibt keine eingebaute Politik. Damit wird die Bindung beim zweiten Durchlauf
**neu aufgeloest**: der Handler liest den aktuellen Stand, nicht einen Schnappschuss. Gefaehrlich bleibt
nur die nicht-idempotente Aenderung („Betrag += 100"), und die kann die Engine nicht erkennen. Der
`ValueHandleRequest` traegt Instanz-, Token- und Knoten-Id; wer den Versuchszaehler braucht, gibt ihn
als Argument mit.

**Kompensation: kein automatischer Eintrag.** Die naheliegende Idee — vor dem Schreiben den alten Wert
merken und im Ernstfall zurueckschreiben — ist die falsche: ein generisches „Restore" schreibt ein
komplettes, inzwischen veraltetes Objekt ueber alles, was zwischenzeitlich passiert ist. Das ist ein
Lost Update, den die Engine verursacht, obwohl sie von den Daten nichts versteht. Wer zuruecknehmen
will, modelliert eine Kompensations-Aktivitaet, die ueber **denselben Handler** ihren eigenen Griff
holt — wie bei jeder anderen Aussenwirkung.

## 8. History

Jeder Schreibvorgang hinterlaesst eine Spur: **welcher Handler, welche Argumente, welcher Knoten, wann,
erfolgreich oder nicht**. Nicht der Wert — nur die Koordinaten. Das ist billig und genau das, was man
braucht, wenn jemand fragt, wer was geschrieben hat.

Es gilt dieselbe Regel wie fuer den Stack: der **Griff** gehoert nicht in die History-Schnappschuesse.

## 9. Fallstricke

### 9.1 `ParameterBindingKind` wird NUMERISCH gespeichert

`WorkflowJson` fuehrt bewusst keinen String-Konverter — in den Definitionen steht `"Kind": 1`. Der neue
Eintrag gehoert ans **Ende**. Ein Eintrag in der Mitte macht aus jeder gespeicherten `Expression`-Bindung
still eine `Variable`, und zwar ohne dass irgendetwas rot wird. Praezedenzfall und Muster fuer den Test:
`UserTaskFieldKindTest.The_Numbers_Of_The_Field_Kinds_Are_The_Contract`.

### 9.2 `MutateAndCommit` kann seinen Delegaten mehrfach ausfuehren

Bei einem Versionskonflikt laeuft der Delegat erneut. Deshalb der Einmal-Riegel um den Schreibvorgang
(5.3) — ohne ihn ginge derselbe Datensatz zwei- oder dreimal raus.

### 9.3 Der Griff ueberlebt kein Parken und keinen Circuit

Er haelt einen Plugin-Scope und ein lebendes Objekt. Deshalb: bei der Benutzer-Aufgabe zweimal
aufloesen, und die Maske sieht nur den ausgepackten Wert.

### 9.4 `Value` + `WriteBack` bei Werttypen

Sinnlos, weil eine Aenderung unsichtbar bleibt. Validator-Warnung — statisch nur dort erkennbar, wo der
Parametertyp deklariert ist.

## 10. Phasen

| # | Inhalt | Stand |
|---|---|---|
| 1 | Member-Zugriff auslagern (`MemberSlot`, `MemberAccessor`, `MemberPath`, Guard, Source); CScript delegiert | **fertig** — CScript-Suite gruen, keine Verhaltensaenderung |
| 2 | Bindungsart, `IWorkflowValueHandler`, `ValueHandle`, Aufloesung in `ResolveInputs`, Automated-Weg | **fertig** — inkl. Plugin-Host und WebCoreToolkit-Host |
| 3 | Die Riegel: Validator, Laufzeit, Serialisierung, parallele Regionen | **fertig** — Tests je Riegel |
| 4 | Benutzer-Aufgabe: Pfade in `PayloadName`, `WriteBackParameters`, Zeitpunkt + Einmal-Riegel | **fertig** |
| 5 | Designer: Bindungs-Dialog (Handler, Argumente, Delivery, WriteBack), Feld-Dialog (Pfad), Knoten-Editor (Write-Back-Liste) | **fertig** |
| 6 | History-Spur, Doku (Integration-Guide §25) | **fertig** |

Phase 1 steht fuer sich und ist auch dann ein Gewinn, wenn der Rest liegen bleibt.

**Nicht gebaut, bewusst:** die Validator-Warnung „`Value` + `WriteBack` bei Werttypen" (9.4). Sie ist nur
dort erkennbar, wo der Parametertyp deklariert ist — der Validator kennt den Aktivitaets-Katalog nicht.
Sie gehoert in den Designer, der ihn hat.

## 11. Geklaerte Randfragen

**Einen Handler zu benennen verschafft niemandem etwas.** Die Frage war, ob `HandlerName` dasselbe
Problem hat wie ein Typname in einer Definition (`ViewKey`: „Definitionen sind DB-Daten, ein Typname
darin waere Code-Ausfuehrung per Datenpflege"). Nein: aufgeloest wird ueber die **konfigurierten**
Plugins des Mandanten plus Typpruefung (`scope[name, true] is IWorkflowValueHandler`) — dieselbe Latte
wie bei `ActivityRef`. Kein zusaetzliches Gatter.

> **In die Doku, nicht in einen Riegel:** bei **oeffentlichen** Definitionen (`TenantId = null`) wird
> der Name im Scope des **ausfuehrenden** Mandanten aufgeloest. Das ist gewollt (gleicher Name, andere
> Konfiguration je Mandant), heisst aber auch: eine oeffentliche Definition trifft bei jedem Mandanten
> das, was dort unter diesem Namen eingerichtet ist.

**Assistenten-Modus braucht keine eigene Regel.** Der Wert kommt nie in den Variablen-Stack, und
`DescribeUserTask` loest den Payload bei **jedem Aufruf** neu auf — der Plugin-Scope des Handlers wird
je Aufloesung erzeugt und danach freigegeben. Beschreiben und Abschliessen sind schon innerhalb *einer*
Aufgabe zwei getrennte Aufloesungen; der naechste Schritt eines Assistenten ist die dritte. Es gibt also
nichts, was ueber eine Aufgabe hinaus leben koennte.

**Mehrere Bindungen auf denselben Handler mit denselben Argumenten:** erlaubt, ergibt **zwei** Griffe —
mit Warnung im Validator. Wer es tut, schreibt zweimal, und das soll man sehen.

**Typkonvertierung** laeuft ueber `ITVComponents.TypeConversion`. Das deckt den Bedarf ab, **sofern der
passende Konverter geladen ist** — dafuer sorgt der Host. Die Engine erfindet dort nichts: schlaegt die
Konvertierung fehl, nennt die Meldung Pfad, Quelltyp, Zieltyp und den Verdacht auf einen fehlenden
Konverter, und die Aufgabe bleibt offen (5.4).

**Offen ist damit nichts Blockierendes** — was beim Bauen noch entschieden wird, steht in den Phasen (10).

## 12. Referenzen

- `ITVComponents.Scripting.CScript/Helpers/MemberAccessHelper.cs` — der auszulagernde Kern
- `ITVComponents.Scripting.CScript/ScriptValues/MemberAccessValue.cs`,
  `WeakReferenceMemberAccessValue.cs` — die beiden Null-Verhalten
- `ITVComponents.Workflow/WorkflowEngine.cs:4609` (`ResolveInputs`), `:3646` (`CompleteUserTask`)
- `ITVComponents.Workflow/Model/ActivityBindings.cs` — Bindungsmodell
- `ITVComponents.Workflow.Plugins/PluginActivityCatalog.cs:79-103` — Muster fuer die Plugin-Aufloesung
- `docs/Workflow-Integration-Guide.md` — „Die Feldarten der generischen Maske", Masken-Abschnitt
