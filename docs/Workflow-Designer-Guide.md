# Workflow-Designer — Elemente, Eigenschaften und Bedienung

Diese Seite beschreibt den grafischen Workflow-Designer (`/Workflow/Editor/new` bzw.
`/Workflow/Definitions/{id}/{version}/edit`): wie er bedient wird, welche Elemente es gibt und was deren
Eigenschaften bedeuten. Sie ergänzt den [Workflow-Integration-Guide](Workflow-Integration-Guide.md), der die
Laufzeit und die Einbettung in den Host beschreibt.

> Voraussetzung: Permission `Workflow.Design` und das Feature `ITVWorkflow`. Ohne beides ist der Editor
> nicht sichtbar.

---

## 1. Aufbau und Bedienung

Der Editor besteht aus drei Bereichen:

| Bereich | Zweck |
|---|---|
| **Kopfleiste** | Id/Name/Version, Import/Export, Validieren, Speichern. |
| **Toolbox (links, schmal)** | Eine Spalte von Symbolen — je ein Symbol pro Elementtyp. Der Tooltip erklärt jedes. Ein Klick fügt das Element in die Mitte des sichtbaren Ausschnitts ein; **ziehen** legt es dort ab, wo man loslässt. Die **Deadline** 🔔 kann man dabei direkt auf einen Schritt ziehen — sie hängt sich an ihn. |
| **Canvas** | Die Zeichenfläche. Sie füllt genau das Fenster — die Seite scrollt nie horizontal; große Graphen erreicht man über **Zoom/Pan**. |

### Auswählen, Bearbeiten, Löschen

- **Einfachklick** auf einen Knoten oder eine Verbindung **wählt** ihn aus (Rahmen bzw. dickere Linie).
- **Doppelklick** öffnet das **Eigenschaften-Popup** des Elements. Alternativ: Element auswählen und in der
  Canvas-Leiste auf **Properties** klicken.
- **Entf** (Delete) löscht das ausgewählte Element. Alternativ: **Delete**-Knopf in der Canvas-Leiste, oder
  der Löschen-Knopf im Popup.
- Klick auf **leere Fläche** hebt die Auswahl auf.

Das Eigenschaften-Popup ist **maximierbar** (Vollbild-Symbol unten links im Dialog). Das schafft Platz für
den Monaco-Editor, wenn Variablen über längere **Ausdrücke** (CScript) zustande kommen. Änderungen im Popup
wirken sofort auf das Modell; **Done** übernimmt zusätzlich den letzten Stand der Ausdrucks-Editoren (Monaco
meldet seinen Text erst beim Verlassen).

### Verschieben, Verbinden, Fehlerpfad

- Einen Knoten **ziehen** verschiebt ihn; die anhängenden Kanten routen sich live neu (nur waagrecht/senkrecht,
  ohne Knoten zu kreuzen).
- **Verbinden:** vom **Port** am rechten Rand eines Knotens auf einen Zielknoten ziehen. Es entsteht eine
  Verbindung (SequenceFlow).
- Aktivität und Subworkflow haben **zwei** Ports: ein **grüner** (Erfolgspfad, rechte Mitte) und ein **roter**
  (Fehlerpfad, rechts unten). Vom roten Port gezogen, wird die neue Kante direkt zum **Fehler-Ausgang** des
  Knotens (rot gezeichnet). Ein zweiter Zug am roten Port ersetzt den bisherigen Fehler-Ausgang; die alte
  Kante bleibt als normale Verbindung bestehen.

### Zoom und Pan

- **Mausrad** zoomt um den Cursor.
- **Ziehen auf leerer Fläche** verschiebt den Ausschnitt (Pan).
- Die Knöpfe unten rechts: **Zoom in (+)**, **Zoom out (−)**, **Fit** (passt den ganzen Graphen ins Fenster).
- Fit läuft auch beim Öffnen und nach einem Import automatisch.

### Import / Export / Validieren / Speichern

- **Export** lädt die aktuelle Definition als portable JSON-Datei herunter.
- **Import** ersetzt die Zeichenfläche aus einer JSON-Datei — es wird **nicht** automatisch gespeichert; Id
  und Version selbst setzen und speichern.
- **Validate** prüft die Definition und zeigt Fehler (rot) und Warnungen (gelb) über dem Canvas.
- **Save** speichert. Bei einer bestehenden Definition wählt man zwischen *Überschreiben* der Version und
  *als neue Version speichern* (die alte Version und ihre laufenden Instanzen bleiben unangetastet). Fehler
  (nicht Warnungen) blockieren das Speichern.

### Workflow-Einstellungen (Zahnrad in der Kopfleiste)

Was für die **ganze Definition** gilt und an keinem Knoten hängt:

- **Default priority for new instances** — die Dringlichkeit, mit der neue Instanzen dieses Workflows von
  den Hintergrund-Workern aufgegriffen werden. **Kleinere Stufe = wichtiger** (Highest 0 … Lowest 4); leer =
  Normal. Wer eine Instanz von Hand startet, kann den Wert für diesen einen Fall übersteuern. Ein
  Subworkflow erbt die Stufe seines Aufrufers und ignoriert seine eigene Vorgabe.
- **Execution log detail** — ab welcher Stufe Einträge ins Ablauf-Protokoll geschrieben werden. Unterhalb
  liegende Einträge werden **nicht** bloß ausgeblendet, sie entstehen gar nicht erst. „Milestones" lässt
  die Schritt-für-Schritt-Einträge (jeder betretene und beendete Knoten) weg — der übliche Griff gegen ein
  zugewachsenes Protokoll. Fehler kommen unabhängig von dieser Einstellung immer durch. Leer = es gilt,
  was der Host eingestellt hat.

---

## 2. Gemeinsame Konzepte

Diese Konzepte tauchen in mehreren Eigenschaftenseiten auf.

### Datenfluss-Bindungen (Inputs / Outputs)

Der Variablen-Stack einer Instanz ist eine Menge benannter Werte. Knoten lesen und schreiben ihn über
Bindungen:

- **Input-Bindung** — belegt einen Zielnamen aus einer **Quelle**:
  - **Constant** — ein fester Wert.
  - **Variable** — der Wert einer bestehenden Instanz-Variable (durchreichen/umbenennen).
  - **Expression** — ein **CScript**-Ausdruck, der den Wert berechnet.
- **Output-Bindung** — bildet ein Ergebnis (Quelle) auf eine **Ziel-Variable** ab.

### Scope-Modus (Konsolidierung)

Viele Knoten (Aktivität, Subworkflow, Join, Start, Ende, Verbindung) tragen einen **Scope-Block**:

- **Additiv (Extend, Standard)** — die Ergebnisse des Knotens werden dem bestehenden Stack **hinzugefügt**;
  alles Übrige bleibt.
- **Replace (Schalter „Consolidate scope / Strict signature")** — nach dem Knoten besteht der Stack **genau**
  aus den deklarierten Ausgaben (bzw. der Signatur) — **plus** der Liste **„keep"** darunter. Alles andere
  wird verworfen. So hält man den Stack sauber.
  - **Wichtig:** Replace/Konsolidierung gehört auf einen **einzelnen Strang** (z. B. nach einem Join), nicht
    in eine parallele Region.
- **keep-Liste** — Variablen, die trotz Replace erhalten bleiben.

### Ausdrücke (CScript)

Bedingungen, Timer-/Korrelations-Ausdrücke, Zuweisungen und berechnete Inputs sind **CScript**. Der Editor
zeigt sie im Monaco-Editor (JavaScript-Syntax kommt CScript am nächsten). Für längere Ausdrücke das Popup
**maximieren**.

### Mehrsprachige Texte (Kultur-JSON)

Titel/Beschriftungen (z. B. bei Benutzer-Aufgaben) sind entweder reiner Text **oder** ein
Pro-Kultur-Datensatz nach demselben Muster wie Navigationseinträge, z. B.
`{"de":"Freigabe","fr":"Approbation"}`.

---

## 3. Die Elemente

Jedes Element hat ein Symbol in der Toolbox, einen Zweck, Ports und eine Eigenschaftenseite.

### Start ▶ (`fa-play`)

- **Zweck:** Einstiegspunkt. **Genau einer** je Definition. Definiert zugleich die **Signatur** des Workflows
  — auch, wenn er als Subworkflow aufgerufen wird.
- **Ports:** ein Ausgang.
- **Eigenschaften:**
  - **Start parameters** (Input-Bindungen) — die deklarierte Signatur. Jede Zeile wird beim Anlegen der
    Instanz gegen die übergebenen Startwerte aufgelöst (Konstante = Vorgabe, Variable = durchreichen,
    Ausdruck = berechnen).
  - **Scope** mit Schalter **„Strict signature (replace)"** — die Instanz startet mit genau diesen Parametern
    (plus keep-Liste); alles andere, was der Aufrufer übergibt, wird verworfen.
  - **Start form** — die Felder, die ein **Mensch** ausfüllt, wenn er den Workflow von Hand startet
    (*Instances → New instance*). Gleiche Feldbeschreibung und gleicher Feld-Dialog wie bei der
    Benutzer-Aufgabe, nur ohne die Payload-Optionen („Display only", „Payload name"): beim Start gibt es
    noch keine Instanz und damit keinen Payload. Keine Felder = der Workflow wird ohne Eingaben gestartet.
  - **Form description** — optionale Anleitung über den Feldern (Klartext oder Kultur-JSON).

> **Maske ≠ Signatur.** Der **Feldname** der Maske ist der Name der *übergebenen* Variable — also genau
> das, wogegen die Signatur anschließend aufgelöst wird. Bei **strikter** Signatur überlebt nur, was oben
> in den Start parameters (oder in der keep-Liste) steht; der Editor rechnet das aus und nennt die Felder,
> die sonst verloren gingen. Die Maske ist rein beschreibend — die Engine liest sie nicht, ein
> programmatischer Start bleibt unverändert möglich.

### Activity ⚙ (`fa-gears`)

- **Zweck:** ein automatisierter Schritt (eine registrierte Aktivität / ein Worker).
- **Symbol im Graphen:** ⚙️ vor dem Namen, in einem **abgerundeten Rechteck** (ausführende Knoten).
- **Ports:** grüner Erfolgs-Port + roter Fehler-Port.
- **Eigenschaften:**
  - **Activity** — der Aktivitäts-Typ. Kennt der Host einen **Katalog**, gibt es ein Dropdown und ein
    **getyptes Formular** aus den deklarierten Parametern; sonst ein Freitext-Verweis plus generischer
    **Key/Value**-Editor auf `Configuration` und freie **Inputs/Outputs**.
  - **Parameters** — je Eingabe eine Bindung (Constant/Variable/Expression), je Ausgabe eine Ziel-Variable.
    Getypte Widgets je Parameter-Art (Bool, Zahl, Mehrzeilig, Passwort, Auswahl, Ausdruck).
  - **Scope** (Konsolidierung).
  - **Iteration** (eigener Reiter, optional) — führt die Aktivität **je Element einer Sammlung** aus statt
    einmal. Für den einen langen Schritt in einem sonst seriellen Ablauf (1000 Dateien signieren), ohne
    dafür 1000 Stränge im Graphen zu erzeugen: es bleibt **ein** Strang, ein Commit, ein Wiederaufsatzpunkt.
    - **Collection comes from input parameter** — welche der Eingabe-Bindungen die Sammlung liefert.
    - **Item / Index → input parameter** — unter welchen Namen der Element-Lauf sein Element (und optional
      seinen Index) bekommt. Leer = das Element ersetzt die Sammlung unter demselben Namen.
    - **Items at a time** — 1 = nacheinander (Standard), 0 = so viele wie Prozessorkerne. Über 1 muss die
      Aktivität **thread-sicher** sein: sie wird einmal aufgelöst und aus mehreren Threads gerufen.
      Schreibzugriffe auf Variablen wirken dann nur je Element und werden verworfen (mit Warnung im
      Protokoll) — Ergebnisse gehören in die **Outputs**, die sammelt die Engine je Ausgabeparameter zu
      einer Liste in Eingabe-Reihenfolge.
    - **Keep going when an item fails** + **Still pending / Failures / Number of successful items → output
      parameter** — alle Elemente versuchen und am Ende über den **Fehler-Ausgang** die Listen mitgeben
      (statt beim ersten Fehler abzubrechen). Die Teilergebnisse bleiben in beiden Fällen erhalten.
      **Für einen Retry die „still pending"-Liste weitergeben**: sie führt die blanken Originale und ist
      die einzige, die auch die nach einem Abbruch nie versuchten Elemente enthält. Die **„failures"**-Liste
      ist die Diagnose-Sicht — je Fehler ein Eintrag mit `Item`, `Index`, `Message` und, falls die
      Aktivität abgestürzt statt kontrolliert abgelehnt hat, `ExceptionType` + `ExceptionDetail`
      (Stacktrace).
    - **Carrying results across attempts** — für Wiederholungs-Schleifen: **The finished item is the
      per-item output parameter** sagt, was das fertige Element ist; **Succeeded items** sammelt genau
      diese Ergebnisse; **Prepend already finished items from input parameter** stellt das Ergebnis der
      vorigen Durchläufe voran. Damit bearbeitet jeder Durchlauf nur den Rest, das Ergebnis ist am Ende
      trotzdem vollständig. Merke: „fertig" trägt die **Ergebnis**-Form, „offen" die **Eingabe**-Form —
      erst dadurch passt die Schleife zusammen.
  - **Execution target** — Name des Hosts, auf dem die Aktivität laufen muss (z. B. „backend"). Leer = jeder
    Runner. Bedient kein laufender Runner dieses Ziel, **parkt** der Strang, bis ein passender ihn übernimmt.
  - **Fehler-Ausgang** (im Popup sichtbar, gezogen am roten Port): bei Fehler (Exception oder `ctx.Fail`)
    nimmt das Token diesen Pfad statt die Instanz zu faulten.
    - **Error message → variable** — legt die Fehlermeldung in dieser Variable ab.
    - **Attempt count → variable** — Zähler, der pro Fehlversuch hochzählt und bei Erfolg zurückgesetzt wird;
      darüber lassen sich **Wiederholungen** verzweigen. Die eine andere ausgehende Kante ist der Erfolgspfad.

### User task 👤 (`fa-user-check`)

- **Zweck:** wartet, bis eine Person eine Aufgabe aus ihrer Arbeitsliste erledigt.
- **Symbol im Graphen:** 👤 vor dem Namen, in einem **abgerundeten Rechteck**.
- **Ports:** ein Ausgang.
- **Eigenschaften:**
  - **Task key** — der fachliche Schlüssel dieser Aufgabenart (z. B. `ApproveInvoice`). Filtert die
    Arbeitsliste und löst das Formular auf.
  - **Required permission** — wer Aufgaben dieses Knotens sehen/erledigen darf. Leer = die allgemeine
    Aufgaben-Permission genügt.
  - **View key** — optional: Schlüssel einer vom Host registrierten Komponente. Leer = erst Task-Key, dann
    generisches Formular.
  - **Title / Description** — Anzeige, mehrsprachig als **Kultur-JSON** (`{"de":"…","fr":"…"}`) oder reiner Text;
    im Editor als JSON-Editor dargestellt.
  - **Format data (CScript-Objekt)** — optional. Ist es gesetzt, werden **Titel UND Beschreibung** (nach der
    Kultur-Auflösung) als Formatierungs-**Prototyp** behandelt: der Ausdruck liefert ein Objekt, dessen Member
    die Platzhalter füllen. Beispiel: Beschreibung `{"de":"Rechnung [InvoiceNo:0000000000]"}`, Format-Data
    `{ InvoiceNo: rechnungsNr }` → angezeigt „Rechnung 0000012345". Die Werte kommen aus dem **Variablen-Stack**.
    Ist der Text **Kultur-JSON**, wird **jede Sprach-Property** formatiert — der Text bleibt also mehrsprachig.
    Leer = Titel und Beschreibung werden unverändert gezeigt. (Formatierungs-Syntax: siehe `ITVComponents.Formatting`.)
    - **Timing:** Der **Titel** wird schon **beim Entstehen der Aufgabe** formatiert und so eingefroren — damit er
      auch in der **Arbeitsliste** fertig (und mit dem damaligen Stand) steht. Die **Beschreibung** wird erst
      **beim Öffnen** formatiert, zeigt also den **aktuellen** Stand.
  - **Title from the data (CScript)** — optionaler Ausdruck, der den Titel aus den Daten bildet (Ergebnis ist
    reiner Text, daher nicht mehrsprachig). Gewinnt über den statischen Titel.
  - **Assignment (CScript → user name)** — wird **einmal** ausgewertet, wenn die Aufgabe erscheint. Leer =
    Aufgabe geht in den Pool. Ein fehlschlagender Ausdruck **faultet die Instanz bewusst** (eine unzugewiesene
    Aufgabe wäre sonst für alle sichtbar).
  - **Due in hours** — nur Anzeige/Sortierung; eine überfällige Aufgabe läuft **nicht** von selbst weiter.
    Soll die Frist etwas *auslösen* (erinnern, eskalieren, automatisch ablehnen), hängt eine **Deadline**
    (🔔) an diesen Schritt — nicht ein Timer in einem parallelen Zweig, der würde den Hauptfluss am Join
    aufhalten.
  - **What the form sees** (Inputs) — Payload, aufgelöst beim Öffnen der Aufgabe (immer aktueller Stand).
  - **What the form returns** (Outputs) + **Scope** — Ergebnis des Formulars auf Variablen abbilden.
  - **Generic form** — Felder für die generische Maske, falls keine Komponente registriert ist (Name, Art,
    Pflicht/Nur-Anzeige, Payload-Name, bei *Choice* die Optionen als `wert=beschriftung` je Zeile). Keine
    Felder = die Aufgabe wird nur bestätigt.

### Subworkflow ▤ (`fa-diagram-project`)

- **Zweck:** ruft eine andere Definition als Kind auf und wartet auf deren Ende.
- **Symbol im Graphen:** 🔗 vor dem Namen, in einem **abgerundeten Rechteck** (ausführende Knoten).
- **Ports:** grüner Erfolgs-Port + roter Fehler-Port.
- **Eigenschaften:**
  - **Definition** — die aufzurufende Definition (Dropdown der verfügbaren Ids, sonst Freitext).
  - **Version** — leer = jeweils neueste.
  - **Inputs → child start variables** — speisen die Startvariablen des Kindes (Konstante/Variable/Ausdruck
    des Elternprozesses; laufen danach noch durch die Signatur des Kindes).
  - **Outputs → parent variables** — bilden Endvariablen des Kindes auf Eltern-Variablen ab.
  - **Scope** (Konsolidierung).
  - **Fehler-Ausgang** — wie bei der Aktivität; mit **Attempt count**-Variable lässt sich der Subworkflow
    durch eine zurückschleifende Fehlerkante **wiederholen** (je Versuch ein frisches Kind).

### Wait ⏳ (`fa-hourglass-half`)

- **Zweck:** parkt das Token, bis ein passendes **Signal** eintrifft.
- **Symbol im Graphen:** ⏳ (Sanduhr) vor dem Namen, in einem **Sechseck** (wartende Knoten).
- **Ports:** ein Ausgang.
- **Eigenschaften:**
  - **Signal name** — auf welches Signal gewartet wird.
  - **Correlation (CScript)** — Ausdruck, der den Korrelationsschlüssel bildet; nur ein Signal mit gleichem
    Schlüssel weckt genau dieses Token.

### Timer ⏰ (`fa-clock`)

- **Zweck:** parkt das Token bis zu einem Fälligkeitszeitpunkt.
- **Symbol im Graphen:** 🕐 (Uhr mit Zifferblatt) vor dem Namen, in einem **Sechseck** (wartende Knoten).
- **Ports:** ein Ausgang.
- **Eigenschaften:**
  - **Due (CScript: DateTime oder TimeSpan)** — ein absoluter Zeitpunkt (DateTime) oder eine Verzögerung
    (TimeSpan).

### Exclusive gateway (XOR) ⋔ (`fa-code-branch`)

- **Zweck:** nimmt **genau einen** ausgehenden Pfad, abhängig von Bedingungen.
- **Symbol im Graphen:** `×` in einer Raute.
- **Ports:** ein Ausgang (mehrere Kanten möglich).
- **Eigenschaften:**
  - **Default flow** — die Kante, die genommen wird, wenn keine Bedingung greift. Die **Bedingungen** stehen
    an den ausgehenden **Verbindungen** (siehe unten).

### Parallel gateway (AND) ✛ (`fa-arrows-split-up-and-left`)

- **Zweck:** als **Split** teilt es in alle Zweige auf; als **Join** (mehrere eingehende Kanten) führt es sie
  wieder zusammen.
- **Symbol im Graphen:** `+` in einer Raute.
- **Eigenschaften:**
  - Als **Split** nichts einzustellen — jeder Zweig bekommt seine **eigene Kopie** des Variablen-Stacks, so
    können sich parallele Zweige nicht gegenseitig überschreiben.
  - Als **Join**: **Result of the parallel region** (Outputs) — bildet Variablen der zusammengeführten Zweige
    auf die Namen ab, mit denen es weitergeht. Leer = alles, was die Zweige geschrieben haben, fließt weiter.
    Schrieben **zwei Zweige denselben Namen**, entscheidet man hier, indem man jedem Zweig einen eigenen
    Ergebnisnamen gibt. Plus **Scope** (Konsolidierung).

### Deadline 🔔 (`fa-bell`)

- **Zweck:** Eine **Frist am Schritt** (BPMN: Boundary-Timer). Hängt an einem Schritt, an dem das Token
  *parkt*, und löst nach Ablauf einen **Nebenpfad** aus — typisch eine Erinnerung. Der Hauptfluss läuft
  dabei unverändert weiter.
- **Symbol im Graphen:** 🔔 in einem kurzen Sechseck (wie der Timer, nur klein), der Name darunter. Er
  klebt halb überlappend am unteren Rand seines Schritts; mehrere reihen sich von rechts nach links auf.
- **Man sieht ihm an, was er tut** — der Unterschied zwischen „erinnert nebenher" und „bricht den Schritt
  ab" ist zu gross, um nur im Popup zu stehen:

  | | Kontur | Bedeutung |
  |---|---|---|
  | **nicht unterbrechend** | **blau, gestrichelt** | Nebenpfad läuft nebenher, der Schritt wartet weiter |
  | **unterbrechend** | **orange, durchgezogen** | der Hauptfluss nimmt den Pfad, der Schritt wird abgebrochen |

  Gestrichelt/durchgezogen ist dieselbe Lesart wie in BPMN und bleibt auch dort erhalten, wo die Farbe
  nicht ankommt (Schwarzweiss-Ausdruck, Farbfehlsichtigkeit). Die Konturfarbe schlägt die Auswahlfarbe —
  ausgewählt zeigt sich in der Strichstärke, wie bei den Fehler-Kanten. Die beiden Punkte rechts in der
  Canvas-Leiste sind die Legende dazu.
- **Ports:** ein Ausgang, **kein** Eingang (er wird nicht angeflossen, sondern hängt an seinem Schritt).
- **Anhängen per Ziehen:** die Glocke aus der Toolbox **auf einen Schritt ziehen** — der Rahmen des
  Schritts wird dicker, sobald er als Ziel in Frage kommt; beim Loslassen dockt der Timer dort an. Genauso
  hängt man ihn später um: den Timer selbst auf einen anderen Schritt ziehen. Neben einem Schritt
  losgelassen springt er an seinen bisherigen zurück — seine Lage ist abgeleitet, nicht gezeichnet.
  (Das gilt für die ganze Toolbox: jedes Werkzeug lässt sich statt anklicken auch an die Stelle ziehen,
  an der der Knoten entstehen soll.)
- **Eigenschaften:**
  - **Attached to** — der Schritt, an dem er hängt. Angeboten werden nur Schritte, an denen wirklich
    geparkt wird: Benutzer-Aufgabe, Subworkflow, Aktivität mit Ausführungsziel. Dasselbe entscheidet, wo
    das Ziehen andocken darf — die Regel steht am Modell (`BoundaryTimerNode.CanHost`), nicht dreifach
    in Validator, Maske und Designer.
  - **Deadlines** — eine Liste, der Reihe nach: die erste Frist zählt ab dem Parken, jede weitere ab der
    vorigen Auslösung. **Add deadline** bzw. das Stift-Symbol öffnen einen Dialog mit demselben
    CScript-Editor wie beim gewöhnlichen Timer (Ausdruck **oder** Block mit `return`); die Pfeile ändern
    die Reihenfolge, denn die ist die Fachaussage. Ein Eintrag darf eine **Zahl** (= Stunden), einen
    **TimeSpan** oder einen **DateTime** liefern — `24`, `'System.TimeSpan'.FromHours(36)`, `faelligAm`.
    Beim **unterbrechenden** Timer gibt es genau *eine* Frist; weitere kämen nie dran, deshalb bietet die
    Maske dort keine an.
  - **Repeat the last deadline forever** — „danach alle 2 Stunden", bis der Schritt weiterläuft. Die
    wiederholte Frist muss eine **Dauer** sein: ein absoluter Zeitpunkt wäre ab der zweiten Runde
    vergangen, und der Timer verstummt dann (statt in einer Schleife zu feuern).
  - **Count variable** — bekommt die Nummer der Auslösung (1 beim ersten Mal) in den Scope des Nebenpfads.
  - **Interrupting** — aus (normal): Nebenpfad, der Schritt wartet weiter. An: das **Haupt**-Token nimmt
    den Pfad, der Schritt wird abgebrochen (eine wartende Aufgabe verschwindet aus der Arbeitsliste).

> **Das ist NICHT ein AND-Split.** Beim Split müsste jeder Strang wieder gejoint werden — der Hauptfluss
> hinge dann bis zum Ablauf der Frist, auch wenn die Aufgabe längst erledigt ist. Der Nebenpfad hier
> hängt niemanden auf: er bekommt eine **Kopie** des Scopes, schreibt nicht zurück, und wird verworfen,
> sobald das Haupt-Token weiterzieht.

### Side end ⏹ (`fa-circle-stop`)

- **Zweck:** Schliesst einen **Nebenpfad** ab. Verbraucht das Token — und **beendet den Workflow nicht**.
- **Ports:** ein Eingang, kein Ausgang.
- **Warum es das braucht:** Der letzte Schritt eines Nebenpfads bräuchte sonst eine ausgehende Kante
  (sonst Fehler), und ein regulärer End-Knoten würde das *Ergebnis der ganzen Instanz* festschreiben,
  obwohl nur die Eskalation gelaufen ist. Der Validator meldet es als Fehler, wenn ein Nebenpfad den
  End-Knoten erreichen kann.

### End ⚑ (`fa-flag-checkered`)

- **Zweck:** schließt die Instanz ab. **Genau einer** je Definition. Definiert das **Ergebnis**.
- **Ports:** keiner (Endpunkt).
- **Eigenschaften:**
  - **Result** (Outputs) — bildet Instanz-Variablen auf Ergebnisnamen ab. Ist etwas deklariert, wird der Stack
    beim Abschluss **auf genau dieses Ergebnis reduziert** — und genau das erhält ein aufrufender Workflow.
    Leer = der komplette Stack ist das Ergebnis. Die **keep**-Liste hält zusätzliche Variablen neben dem
    Ergebnis.

---

## 4. Verbindungen (SequenceFlow)

Eine Verbindung führt von einem Quell- zu einem Zielknoten. Doppelklick öffnet ihre Eigenschaften:

- **Label** — Beschriftung (auch im Graphen sichtbar).
- **Condition (CScript, für XOR-Gateways)** — die Bedingung, unter der ein Token diese Kante nimmt. Relevant
  vor allem an ausgehenden Kanten eines **XOR**-Gateways; die Kante ohne/mit nicht erfüllter Bedingung wird
  über den **Default flow** des Gateways abgedeckt.
- **Mapping on arrival (optional)** — läuft, **wenn** ein Token diese Kante nimmt (nachdem die Bedingung
  entschieden hat). Damit normalisiert man die Variablen, die der Zielknoten sieht — typisch, um mehrere
  Pfade auf gleiche Namen zu bringen. Ergänzt das Mapping am Zielknoten.
- **Scope** — nach dieser Kante besteht der Scope aus dem Mapping oben plus der keep-Liste (bei Replace).

Eine **rot** gezeichnete Verbindung ist der **Fehler-Ausgang** ihres Quellknotens (siehe Activity/Subworkflow).

---

## 5. Validierung

**Validate** (und jedes **Save**) prüft u. a.:

- genau **ein Start** und **ein Ende**,
- erreichbare Knoten und keine ins Leere zeigenden Ausgänge,
- konsistente Fehler-/Default-Ausgänge.

Eine Definition mit **Fehlern** lässt sich trotzdem **speichern** (Zwischenstand), wird dabei aber als
**„Nicht startbar"** markiert (rotes Badge in der Kopfleiste): die Engine verweigert den Start einer solchen
Definition mit einer klaren Meldung, bis die Fehler behoben und erneut gespeichert wird. **Warnungen** setzen
das Flag nicht. **Bereits laufende Instanzen** sind nicht betroffen — das Flag sperrt nur den START.

Wird ein Knoten oder eine Kante gelöscht, räumt der Editor verwaiste Verweise (Fehler-/Default-Ausgang)
automatisch mit auf, damit keine ins Leere zeigende Id zurückbleibt.

## 6. Darstellung der Verbindungen

Die Linien laufen **rechtwinklig** (nur waagrecht/senkrecht) und weichen Knoten aus. Zwei Regeln zur
Überlagerung:

- Kanten, die vom **selben Ausgang** starten (Parallel-**Split**), teilen sich das Anfangsstück und trennen
  sich erst, wo ihre Wege auseinanderlaufen.
- Kanten, die auf **dasselbe Ziel** zulaufen (Parallel-**Join**), teilen sich das Endstück auf dem
  Schlussspurt zum Knoten.
- Nur **Hin- und Rückweg** zwischen zwei Knoten (an derselben Seite ein aus- und ein eingehendes Ende) werden
  bewusst **nebeneinander** gelegt, damit sie unterscheidbar bleiben.
