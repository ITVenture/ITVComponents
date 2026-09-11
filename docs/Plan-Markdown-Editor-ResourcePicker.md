# Markdown-Editor mit WYSIWYG und Ressourcen-Auswahl

**Stand:** gebaut und vom Anwender durchgetestet (2026-09-11, Zweig Future_10). Aus dem Test kamen
zwei Nachtraege, beide eingearbeitet: der Namensdialog beim Einfuegen und der Einfuegeweg ueber die
Editor-Befehle (Falle 6).

**Anlass:** Der Hilfe-Editor (`HelpTopicDialog`) war ein reiner Markdown-Quelltext-Editor (Monaco).
Wer ein Bild einbetten wollte, musste den Ressourcen-Namen aus dem Kopf oder aus einem zweiten
Browserfenster holen und `![...](resource:...)` von Hand tippen. Ein Tippfehler fiel erst in der
Vorschau auf - und dort nur als fehlendes Bild, ohne Hinweis, welcher Name gemeint war.

Vorlage war die Designstudie aus MLM (`MLMManager/docs/Designstudie-MD-Editor-Resource-Picker.md`),
wo dasselbe fuer Produkt-Beschreibungen bereits gebaut ist. Uebernommen wurden die Entscheidungen,
nicht der Code eins zu eins - die Schichtung ist im Toolkit eine andere.

---

## Was gebaut wurde

### 1. `MarkdownEditor` im Basis-Paket

`ITVComponents.WebCoreToolkit.Blazor.MudBlazor/SharedComponents/MarkdownEditor.razor`

WYSIWYG-Editor auf Basis des mitgelieferten TOAST UI Editors (MIT, 3.2.2, unter
`wwwroot/vendor/toastui`), der trotzdem **Markdown** speichert. Bewusst im **Basis-Paket** und nicht
in den AdminViews: das Feld ist allgemein und steht damit jeder Maske offen (Hilfe-Themen,
Beschreibungstexte, Workflow-Masken), nicht nur dem Hilfe-Editor.

Merkmale:

- **Nachgeladen, restlos.** In der Host-Seite haengt **nichts** davon: der Wirt
  `wwwroot/markdown-editor.js` ist ein ES-Modul, das die Komponente beim ersten Aufbau per
  `import()` holt (derselbe Weg wie `FileUpload`/`FileDownload` nebenan), und er zieht dann seinerseits
  das Bundle nach (rund 700 KB js+css). Der Initial-Load einer Anwendung waechst dadurch um **null
  Bytes**; wer nie ein Markdown-Feld oeffnet, laedt weder das eine noch das andere.
- **Rueckfall.** Scheitert das Laden, schaltet die Komponente auf ein Textfeld mit rohem Markdown um
  und protokolliert den Grund. Eine leere Flaeche gibt es nicht.
- **Der Editor besitzt seinen Text.** Gemeldet wird gebuendelt (600 ms) und beim Verlassen des
  Feldes; wer im selben Klick speichert, holt ihn ueber `GetValueAsync()`.
- **Dunkles Thema** wird an der Helligkeit der Seitenfarbe gemessen (Parameter `Dark` ueberschreibt).
  MudBlazors Dunkel-Modus haengt am ThemeProvider; ihn bis in jede Maske durchzureichen waere ein
  Parameter, den jeder Aufrufer vergessen kann.
- Sprachdateien fuer de/fr/it werden nach der aktuellen UI-Kultur nachgeladen; fehlt eine, spricht
  der Editor englisch (mit Konsolen-Warnung).

### 2. `IMarkdownResourcePicker` - die Bruecke

`ITVComponents.WebCoreToolkit.Blazor.MudBlazor/SharedComponents/MarkdownResourcePicker.cs`

Kleine Abstraktion in der Basis, damit der Editor die Ressourcen-Bibliothek der Hilfe **nicht kennen
muss** (die liegt in den AdminViews und bringt EF-Kontext und Berechtigungen mit):

| Mitglied | Wofuer |
|---|---|
| `UrlPrefixes` | `"resource:"` -> `"/{mandant}/help/res/"`, fuer die Anzeige im Editor |
| `CanPickAsync` / `CanUploadAsync` | ob Knopf und Einfuege-Haken ueberhaupt erscheinen |
| `PickAsync(kind)` | oeffnet die Auswahl, liefert den Verweis zerlegt (`MarkdownResourceReference`) |
| `UploadAsync(upload)` | legt eine eingefuegte Datei ab, liefert den Ressourcen-Namen |

Ist kein Dienst registriert, fehlen Knopf und Haken - die Maske bleibt vollstaendig benutzbar.

### 3. Auswahl und Umsetzung im Hilfesystem

- `HelpViews/Components/Admin/HelpResourcePickerDialog.razor` - Pfadzeile, Suche, Kacheln mit
  Vorschau; Ebene fuer Ebene geladen (`ListNodesAsync`). Ein Klick liefert Art, Adresse
  (`resource:name`) und Text getrennt - siehe Falle 6.
- `HelpViews/Components/Admin/HelpResourceImportDialog.razor` - fragt den Namen ab, BEVOR ein
  eingefuegtes Bild in der Bibliothek landet. Vorgeschlagen wird der Dateiname, sonst `Image`, und
  zwar bereits auf einen freien hochgezaehlt (`Image-2`, `Image-3`); vergebene Namen meldet der
  Dialog sofort statt erst beim Speichern.
- `HelpViews/Handlers/Impl/HelpMarkdownResourcePicker.cs` - die Umsetzung; registriert in
  `AddMudBlazorHelpViews`. Ab dieser Registrierung hat **jedes** Markdown-Feld der Anwendung den
  Knopf.
- `HelpTopicDialog` benutzt jetzt den `MarkdownEditor` statt des `CodeEditor`.

### 4. `CodeEditor.InsertAsync`

`TenantSecurityViews/Components/Shared/CodeEditor.razor` kann jetzt an der Einfuegemarke einfuegen
(Monaco `GetSelection` + `ExecuteEdits`, mit intakter Rueckgaengig-Kette). Nicht fuer die Hilfe
gebraucht, aber der naheliegende Weg fuer Knoepfe, die in den Code-Dialogen eine Vorlage oder einen
Platzhalter beisteuern.

---

## Entscheidungen, die man kennen sollte

**Die Vorschau kommt vom echten Endpunkt.** Der Auswahldialog zeigt Bilder ueber
`/help/res/{name}` - also genau ueber den Weg, den der Verweis spaeter nimmt. Damit ist der Dialog
sein eigener Selbsttest: was hier nicht erscheint, erscheint auch im Hilfe-Text nicht, und man sieht
es, bevor der Verweis im Text steht.

**Der Name wird zum Alt-Text.** Er ist das Einzige, was ein Vorlese-Programm zu hoeren bekommt, wenn
das Bild fehlt - und besser als ein leeres `![]`, das beim Einfuegen niemand mehr ausfuellt.

**Der Name wird VOR dem Hochladen erfragt, nicht danach vergeben.** Er ist der Schluessel der
Ressource und steht unmittelbar danach als `resource:name` im Text. Ein spaeteres Umbenennen kostet
den Gang in die Ressourcen-Verwaltung UND das Nachziehen jedes Verweises im Text - und beim vierten
Screenshot weiss niemand mehr, welcher welcher war. Das Feld im Dialog kostet dagegen einen
Tastendruck. (Aus dem ersten Anwendertest.)

**Eine Art je Aufruf.** Bilder, Videos und "Sonstiges" liegen in derselben Bibliothek, gehoeren aber
nicht in dieselbe Schreibweise: ein Video in einem `![...]` saehe im Text aus wie ein kaputtes Bild.

**Kein zweiter Pflegeweg.** Hochgeladen, benannt und einsortiert wird weiterhin unter
`/Help/Admin/Resources`. Der Dialog ist eine Auswahl.

**Die Server-Vorschau bleibt.** Obwohl der WYSIWYG-Modus schon zeigt, was dasteht, ist der
`Preview`-Knopf die Wahrheit: er rendert ueber `IHelpContentRenderer` - dieselbe Pipeline wie beim
Leser, mit abgeschaltetem Roh-HTML und `module:`-Verweisen nur fuer Angemeldete.

---

## Sechs Fallen, die hier schon eingebaut sind

**1. UMD neben Monacos AMD-Loader.** Die Host-Seite bindet fuer den `CodeEditor` Monacos `loader.js`
ein, und der setzt ein globales `define` mit `define.amd`. Das TOAST-UI-Bundle prueft genau darauf,
bevor es `window.toastui` setzt - es meldete sich sonst bei Monacos Loader an, und `toastui` bliebe
undefiniert. `markdown-editor.js` nimmt `define` fuer die Dauer des Ladens weg und stellt es danach
unveraendert zurueck.

**2. `resource:` versteht der Editor nicht.** Seine eigene Markdown-Maschine haelt `resource:foo`
fuer eine unbekannte Adresse - der Autor saehe beim Schreiben lauter kaputte Bilder, also genau das,
was die Auswahl verhindern soll. Geloest ueber einen `MutationObserver`, der `img[src]` im Editor auf
die echte Adresse umbiegt. **Das faelscht den Text nicht:** der Editor rendert Bilder im WYSIWYG ueber
eine ProseMirror-NodeView **ohne contentDOM**, und fuer die ignoriert ProseMirror DOM-Aenderungen;
`getMarkdown()` serialisiert ausserdem aus den Knoten-Attributen, nicht aus dem DOM. Im gespeicherten
Text steht weiterhin `resource:name`.

**3. Eingefuegte Bilder als `data:`-URI.** Ohne Eingriff schreibt der Editor ein aus der
Zwischenablage eingefuegtes Bild als `data:`-URI in den Text - der landete in der Datenbank und in
jeder Auslieferung. Der Haken `addImageBlobHook` schickt es stattdessen in die Bibliothek und fuegt
`resource:name` ein. Ohne schreibende Berechtigung wird es abgelehnt (mit Meldung), nicht stumm
eingebettet.

**4. 32 KB je Nachricht.** Eine Blazor-Server-Verbindung nimmt je Interop-Nachricht standardmaessig
nur 32 KB an - ein eingefuegter Screenshot risse sie ab. Der Haken uebergibt deshalb eine
**JS-Stream-Referenz** (`DotNet.createJSStreamReference`), keine Bytes im Argument.

**5. Zwei Sprachen mit gleichem Text.** Der Editor besitzt seinen Text und meldet ihn nur gebuendelt;
fuer die Maske sehen zwei Sprachen mit (noch) gleichem Inhalt deshalb aus wie "nichts zu tun". Wuerde
der Wechsel der Bindung ueberlassen, bliebe der Text der einen Sprache in der anderen stehen und
landete beim Speichern dort. Deshalb laedt `HelpTopicDialog.SwitchCultureAsync` den anderen Text
**ausdruecklich** ueber `MarkdownEditor.SetValueAsync`; der Parameter-Weg zieht nur nach, wenn der
Aufrufer wirklich einen neuen Wert hereingibt (`lastParameterValue`).

**6. Rohtext an der Einfuegemarke ist kein Markdown-Einfuegen.** `editor.insertText()` ruft in BEIDEN
Modi `replaceSelection`, und das legt einen Text-Knoten an. Im Quelltext-Modus faellt das nicht auf -
dort IST der Inhalt Text. Im WYSIWYG-Modus steht der Verweis danach als Text im Dokument, und beim
Serialisieren escaped der Editor die Sonderzeichen: aus `![name](resource:name)` wird
`!\[name\](resource:name)`, und im fertigen Dokument erscheint statt des Bildes sein Alt-Text. Wer
den Fehler sucht, sieht im Quelltext etwas, das fast richtig aussieht.

Der Weg ist stattdessen `editor.exec('addImage' | 'addLink', ...)`: der geht an den Befehl der
jeweiligen Betriebsart - im Quelltext-Modus schreibt er rohes Markdown, im WYSIWYG-Modus legt er
einen echten Bild- bzw. Verweis-Knoten an, und escapen tun beide nur den Alt- bzw. Verweistext, wie
es sich gehoert. Deshalb reicht die Auswahl den Verweis **zerlegt** zurueck (Art/Adresse/Text) und
nicht als fertige Zeichenkette. `MarkdownEditor.InsertResourceAsync` ist der oeffentliche Weg dafuer;
`InsertAsync(string)` bleibt, was es ist - Rohtext, mit dieser Einschraenkung dokumentiert.

(Gefunden im ersten Anwendertest; die Designstudie hatte `insertText` fuer beide Modi als richtig
beschrieben - das war nie geprueft worden.)

---

## Einstellungen

`HelpSystemOptions.PastedMediaFolder` (Vorgabe `"Pasted"`) - der Ordner der Bibliothek, in dem
eingefuegte Bilder landen. Leer = Wurzel. Eingefuegte Bilder tragen einen erfundenen Namen
(Dateiname bzw. Zeitstempel) und brauchen spaeter meist eine Nacharbeit; in einem eigenen Ordner sind
sie dafuer auffindbar. Schlaegt das Einsortieren fehl, bleibt die Ressource in der Wurzel und ist
ueber ihren Namen trotzdem erreichbar - ein abgebrochener Einfuegevorgang waere der teurere Ausgang.

## Voraussetzung im Wirt

Keine. Der Editor braucht **keinen** Eintrag in der Host-Seite - weder ein Skript noch ein
Stylesheet: er holt sich alles selbst, und die Adressen reicht die Komponente relativ herein, damit
sie im PathSegment-Modus am `<base href="/{mandant}/">` nicht vorbeizielen.

Fuer das Aussehen der Dialoge gilt weiterhin, was fuer die ganze Mud-Schicht gilt:
`<ITVentureReferences />` in der statisch gerenderten Host-Seite (bringt `itv-mudblazor.css`, darin
auch die Regeln fuer den Editor-Rahmen).

## Was offen bleibt

- **Hochladen aus dem Auswahldialog.** Die Datei kommt heute ueber Einfuegen/Ziehen in den Text oder
  ueber `/Help/Admin/Resources` in die Bibliothek. Ein "Neu hochladen"-Knopf im Dialog waere die
  naechstliegende Erweiterung - den Namensdialog dafuer gibt es inzwischen.
- **Zielordner im Namensdialog waehlen.** Heute nennt er den Ordner nur; gesetzt wird er ueber
  `PastedMediaFolder`.
- **Ein `module:`-Picker nach demselben Muster.** Dieselbe Luecke: der Pfad wird heute von Hand
  getippt. Eine Auswahl ueber die Navigations-Eintraege waere die Entsprechung.
- **Der Weg ueber `insertText`** bleibt fuer Rohtext bestehen und ist damit weiterhin der falsche
  Weg fuer Auszeichnungen im WYSIWYG-Modus. Eine allgemeine "Markdown an der Einfuegemarke einfuegen"-
  Funktion gaebe es nur ueber einen Moduswechsel mit Neuaufbau des Dokuments - dabei geht die
  Einfuegemarke verloren, und das waere der schlechtere Tausch.
