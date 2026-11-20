# ISSUE: Modul-Plugins – Installation, Angebot und Instanzen trennen; ein Update-Dienst für mehrere Anwendungen

**Status:** offen · **Datum:** 2026-11-18 · **Version:** 5.0.0-PRE277 ·
**Quelle:** MiniStore, Erweiterungen für POS-Agent und Hintergrund-Dienst

## Worum es geht

Ein Modul kommt heute vollständig auf den Rechner, bis zu dem Punkt, an dem die Anwendung es benutzen soll:

1. Im Web wird einer Anwendung ein Modul zugewiesen.
2. Die Anwendung meldet das dem Update-Dienst (`AssignModulesAsync`).
3. Der Dienst lädt das Modul aus dem Feed, beendet die Anwendung, entpackt nach `modules\<id>\` und startet sie neu
   (§116).
4. **Lücke:** Kein Schritt macht aus dem Modul ein Plugin. Die Konstruktionszeile `[modules\<id>\X.dll]<Typ>`
   (§115.2) muss schon in der Konfiguration der Anwendung stehen. Wer sie dort einträgt, ist nicht geregelt.

Daraus folgen drei offene Fragen:

- Wie werden die Plugins eines Moduls registriert?
- Was gilt, wenn ein Modul **mehrere** Plugins anbietet?
- Wie entsteht eine **zweite Instanz** desselben Plugins mit anderer Konfiguration, z.B. ein zweiter Bondrucker?

Dazu kommt eine vierte Frage: Ein Rechner kann **mehrere** Anwendungen haben, z.B. einen Benutzer-Agenten für Druck
und Bedienung und einen Windows-Dienst für Hintergrundaufgaben. Beide sollen über denselben Update-Dienst
aktualisiert und erweitert werden.

## Stand heute (gelesen auf PRE277)

| Teil | Was er tut | Was er nicht tut |
|---|---|---|
| `ModuleFiles` / `ModuleManager` (§116) | entpacken, umschalten, zurückschalten; schreibt `module.json` | fasst keine Plugin-Konfiguration an |
| `ModuleContextSelector` (§115) | Assemblies unter `modules\<id>\` in den Kontext des Moduls laden | findet keine Plugins, lädt nur, was konfiguriert ist |
| `IAgentObjectCatalog` (§72) | konfigurierte Bereichs-Plugins aufzählen, beschreiben, ihre Einstellungen ablegen | legt keine Instanz an und entfernt keine |
| Benutzerdatei (§72, §123) | `%ProgramData%\…\appsettings.json`, **für jedes Konto beschreibbar**; mit §123 auf `ITVenture:AgentServices:Devices` beschränkbar | – |
| `UpdateServiceOptions` | **eine** Anwendung: `ProductId`, `UpgradeCode`, `ApplicationPath`, `ModulesDirectory`, `HubServiceName` | keine zweite Anwendung |
| `ApplicationControl` | Anwendung in einer Windows-Sitzung starten (`StartInSession`), per Prozess-Id beenden | keinen Windows-Dienst stoppen oder starten |

## Zielbild: drei getrennte Schritte

| Schritt | Wer | Was | Rechte |
|---|---|---|---|
| **1. Installieren** | Update-Dienst | Dateien des Moduls (wie heute) | privilegiert |
| **2. Anbieten** | Modul (Marke) + Anwendung (Katalog) | Welche Plugin-Typen das Modul hat, mit Art, Anzeigename und Einstellungstyp | – |
| **3. Instanzen** | Web → Katalog → Update-Dienst | Instanz anlegen oder entfernen, mit Name und Typ | **Existenz** privilegiert, **Einstellungen** unprivilegiert |

Ein Modul installieren heisst noch nicht, ein Gerät zu haben. Eine Instanz anlegen heisst noch nicht, dass sie
eingerichtet ist. Das sind getrennte Vorgänge mit getrennten Rechten.

## A. Die Marke im Modul

Ein Attribut im Vertrag `ITVComponents.Plugins` (Namensraum `ITVComponents.Plugins.Modules`) markiert die Typen,
die ein Modul anbietet:

```csharp
[ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope, DisplayName = "Epson TM (ESC/POS)")]
public class EscPosPrinter : IReceiptPrinter, IHasDeviceSettings<EscPosPrinterSettings>, IPlugin { … }

[ModulePlugin(ModulePluginKind.Infrastructure, PluginLoadType.Scope, Name = "windowsPrinters")]
public class WindowsPrinterChoices : IDeviceChoiceProvider, IPlugin { … }
```

| Art | Instanzen | Name | Wer legt sie an |
|---|---|---|---|
| `Infrastructure` | genau eine | vom Modul fest vorgegeben (`Name`), z.B. der Name aus `ChoiceSource` | der Update-Dienst, im selben Durchgang wie das Umschalten des Moduls |
| `Template` | 0 bis n | vergibt das Web | auf Anforderung aus dem Web |

Für die Marke gilt:

- **Ohne Marke wird nichts angeboten.** Hilfsklassen eines Moduls bleiben unsichtbar, auch wenn sie `IPlugin`
  implementieren.
- **Die Assembly ist die Quelle**, keine Liste in `module.json`, die vom Code abweichen kann.
- Der Update-Dienst liest die Marke über `MetadataReader`, so wie `ModulePackager` die Vertragsmarke liest. Er
  **lädt keinen Code eines Moduls**.
- `ModulePackager` (§124) prüft beim Publish, ob die Marke stimmt:
  - Eine Marke an einem Typ ohne `IPlugin` ist ein Fehler.
  - Zwei Infrastruktur-Plugins mit demselben Namen sind ein Fehler.
  - Eine Marke an einem offen-generischen Typ ist ein Fehler (A.2).
  - Ein Modul ohne jede Marke ergibt eine Warnung.

### A.1 Der Lademodus gehört zur Marke

Jedes Plugin wird in einem der drei Modi aus `PluginLoadType` geladen, wie jede andere Plugin-Zeile auch
(`PluginConfigurationItem.LoadType`):

| `PluginLoadType` | Wann es entsteht | Typisch im Modul |
|---|---|---|
| `Singleton` | beim Start des Dienstes bzw. der Anwendung, einmal | Hintergrund-Arbeiter, Listener, Timer, Anbindung an ein Fremdsystem |
| `ScopeStartup` | beim Öffnen jedes Scopes | was jeder Scope von Anfang an braucht, z.B. ein Initialisierer |
| `Scope` | auf Verlangen, wenn ein anderes Plugin es im Konstruktor anfordert oder eine Remote-Verbindung das Objekt anspricht | Geräte (Drucker, Schublade, Terminal), Auswahl-Anbieter |

Für den Modus gilt:

- **Der Modus steht in der Marke.** Ob ein Typ als Singleton oder auf Verlangen gebaut werden muss, weiss nur der
  Autor des Moduls. Ein Bondrucker als `Singleton` würde beim Start gebaut, ob er gebraucht wird oder nicht, und ein
  Fehler beim Bau verhinderte den Start. Ein Listener als `Scope` liefe nie an.
- **Das Web wählt den Modus nicht.** `CreateObjectAsync` nimmt ihn aus der Marke und schreibt ihn in
  `plugins.json`.
- **Prüfung:** Der Dienst schreibt keinen anderen Modus als den der Marke. Der Lader lehnt eine Abweichung ab,
  falls die Datei von Hand geändert wurde, und protokolliert einen Fehler.
- **Mehrere Modi für denselben Typ** sind nicht vorgesehen. Braucht ein Modul beides, bekommt es zwei Typen mit je
  eigener Marke. Ob das reicht, ist ein offener Punkt.

Der Modus bestimmt auch, was **beim Anlegen und Ändern** einer Instanz nötig ist (siehe C.2) und wo sie sichtbar
ist:

| Modus | Neue Instanz wirkt … | Geänderte Einstellungen wirken … | Im Geräte-Katalog (§72) |
|---|---|---|---|
| `Scope` | beim nächsten Anfordern, ohne Neustart | beim nächsten Bau, also im nächsten Scope | ja, das sind die Bereichs-Plugins, die der Katalog heute schon aufzählt |
| `ScopeStartup` | ab dem nächsten Scope, ohne Neustart | ab dem nächsten Scope | nein |
| `Singleton` | erst nach einem Neustart | erst nach einem Neustart | nein |

Der Katalog zählt heute nur `Scope`-Plugins auf (`SettingsDrivenLoader.GetScopedPluginNames`). Für Vorlagen und
Instanzen der beiden anderen Modi braucht er deshalb eine eigene Aufzählung, getrennt von den Geräten. Ein
Hintergrund-Arbeiter ist kein Gerät, das einer Kasse zugeordnet wird.

### A.2 Generische Typargumente

Ein offen-generisches Plugin braucht seine Typargumente aus `GenericTypeDefinition`. Deren `TypeExpression` ist
**kein Typname, sondern ein Ausdruck**: `GenericArgumentHelper.BuildGenericArguments` gibt ihn an
`ExpressionParser.Parse`. Wer ihn schreibt, führt Code aus. Er darf darum weder aus dem Web kommen noch als freier
Text in `plugins.json` oder in der Benutzerdatei stehen.

Wer das Argument kennt, entscheidet, wie es zustande kommt:

| Fall | Wer kennt das Argument | Regel |
|---|---|---|
| **Modul legt es fest** | Autor des Moduls | Die Marke steht nur an einem **geschlossenen** Typ, z.B. `class EscPosPrinter : PrinterBase<EscPosSettings>`. Kein eigener Mechanismus. |
| **Anwendung liefert es** (z.B. `TContext` = DbContext des Hosts) | Hersteller der Anwendung | Zurückgestellt, siehe unten. |
| **Web wählt es** | niemand verlässlich | Nicht vorgesehen. |

**Umgesetzt wird zuerst nur der erste Fall:**

- `ModulePackager` meldet einen Fehler, wenn die Marke an einem offen-generischen Typ steht.
- Der Lader lehnt einen offen-generischen Typ ebenso ab, falls ein Modul ohne Prüfung in den Feed kam, und
  protokolliert einen Fehler.
- `plugins.json` bekommt **kein** Feld für Typargumente.

**Zurückgestellt: Typargumente aus der Anwendung (Bindungen).** Kommt dazu, sobald ein Modul es wirklich braucht.
Das Attribut wird so angelegt, dass die Erweiterung ohne Bruch möglich ist.

- Die Marke nennt je offenem Typparameter einen **Bindungsnamen**:
  `[ModulePluginGenericArgument("TContext", Binding = "ServiceDbContext")]`.
- Die **Hauptkonfiguration** der Anwendung ordnet dem Bindungsnamen `GenericTypeDefinition`-Zeilen zu,
  einschliesslich `$$genericArgumentProvider`. Der Ausdruck steht damit in der Datei des MSI, der ohnehin vertraut
  wird.
- Der Lader beantwortet `HasParamsFor`/`GetGenericParams` über die Bindung und benutzt dafür
  `BuildGenericArguments`. Die Regel bleibt an einer Stelle.
- Die Typargumente einer Instanz ergeben sich aus Marke und Anwendung. Zwei Instanzen derselben Vorlage haben in
  derselben Anwendung immer dieselben Argumente. `plugins.json` bleibt unverändert.
- **Fehlende Bindung:** Der Katalog bietet die Vorlage nicht an und protokolliert eine Warnung mit dem Namen der
  Bindung. Eine bestehende Instanz überspringt der Lader, protokolliert einen Fehler, und die Anwendung startet
  trotzdem (wie „Modul fehlt“, C.4).
- **Bindung verletzt die Constraints** (`FinalizeTypeArguments` scheitert): wie fehlende Bindung, der Fehler betrifft
  nur die Instanz.
- Der Update-Dienst liest per `MetadataReader` nur, **welche** Bindungen ein Typ verlangt. Auflösen kann sie nur die
  Anwendung. Die Prüfung liegt darum im Katalog, vor `CreatePluginInstanceAsync`.
- `ModulePackager` prüft dann: Jeder offene Typparameter hat genau eine Bindung, und keine Bindung nennt einen
  Parameter, den der Typ nicht hat.

## B. Wo die Instanzen stehen

Für die Ablage der Plugin-Zeilen gibt es drei Möglichkeiten:

- **Benutzerdatei:** Scheidet aus. Sie ist für jedes Konto beschreibbar (§123). Eine Plugin-Zeile bestimmt, welcher
  Code geladen wird. Ein Eintrag dort wäre genau die Lücke, die §123 für `AssemblyResolving` geschlossen hat.
- **Hauptkonfiguration der Anwendung:** Scheidet ebenfalls aus.
  - Die Datei gehört dem MSI. Ein Update oder eine Reparatur ersetzt sie oder lässt die Änderung je nach
    Komponentenregel stehen. Beides ist nicht verlässlich.
  - Konfiguration des Herstellers und des Standorts wären vermischt.
  - Der Update-Dienst müsste das Konfigurationsformat jeder Anwendung kennen.
- **Gewählt: eine dritte Datei, die nur der Update-Dienst schreibt:**
  - Ort: `<DataDirectory>\targets\<targetId>\plugins.json`, unter `ProtectedDirectory`. SYSTEM und Administratoren
    haben Vollzugriff, angemeldete Benutzer dürfen nur lesen. Das ist schon heute so eingerichtet.
  - Die Anwendung liest sie beim Start zusätzlich zu ihren eigenen Plugins.
  - Die Datei überlebt jedes MSI-Update.

Die Datei enthält **Daten, keine Konstruktionszeilen**:

```json
{ "instances": [
  { "name": "Bondrucker1", "module": "MiniStore.Printer.EpsonTM", "assembly": "MiniStore.Printer.EpsonTM.dll",
    "type": "MiniStore.Printer.EpsonTM.EscPosPrinter", "kind": "template", "loadType": "Scope" },
  { "name": "windowsPrinters", "module": "MiniStore.Printer.EpsonTM", "assembly": "MiniStore.Printer.EpsonTM.dll",
    "type": "MiniStore.Printer.EpsonTM.WindowsPrinterChoices", "kind": "infrastructure", "loadType": "Scope" }
] }
```

Die Zeile `[modules\<module>\<assembly>]<type>` baut erst der Lader daraus, bei Plugins mit Einstellungen mit
`$DeviceSettings`. Dafür gilt:

- Kein Feld ist freier Text, der in eine Konstruktionszeile gelangt. Jedes Feld wird geprüft:
  - Der Name folgt den Regeln aus §72.
  - Das Modul ist installiert.
  - Die Assembly liegt im Modulordner.
  - Der Typ trägt die Marke.
- **Der Lader ist ein eigener `IDynamicLoader`** nach dem Muster von `SettingsDrivenLoader`. Er liefert die
  Instanzen je `PluginLoadType` (`LoadDynamicAssemblies`) und die `Scope`-Instanzen über `GetScopedPlugin`. Die
  Factory fragt ihn damit an denselben Stellen wie jeden anderen Lader. Er liest `plugins.json` bei einer Änderung
  neu, damit eine neue `Scope`-Instanz ohne Neustart gefunden wird.
- Ein Plugin mit demselben Namen in der Hauptkonfiguration geht vor. Die Instanz wird dann nicht geladen, und der
  Lader protokolliert einen Fehler.

**Die Einstellungen** einer Instanz bleiben, wo sie seit §72 liegen: in der Benutzerdatei unter
`ITVenture:AgentServices:Devices:<Name>`, beschränkt nach §123. Ob eine Instanz existiert, entscheidet der
privilegierte Dienst. Wie sie eingestellt ist, legt die Anwendung ab. Das passt zur Trennung zwischen Installieren
und Konfigurieren.

## C. Abläufe

### C.1 Modul zuweisen (erweitert §116)

Der Durchgang läuft wie heute. Neu ist ein Schritt **beim Umschalten**: Der Dienst gleicht die
Infrastruktur-Instanzen des Moduls in `plugins.json` ab.

- Neue Infrastruktur-Instanzen werden angelegt, weggefallene entfernt.
- Der Abgleich gehört zum Umschalten. Schaltet M3 zurück, kommt auch der vorherige Stand von `plugins.json` zurück.
- **Template-Instanzen** bleiben bei einem Update stehen.
- Fehlt der Typ einer Template-Instanz in der neuen Fassung, ist das Modul-Update ein Fehler. Es wird nicht
  installiert, und `UpdateStatus.Modules` meldet den Grund (z.B. „type X is used by Bondrucker1 and missing in
  2.1.0“). Verwaiste Instanzen entstehen so gar nicht erst.

### C.2 Instanz anlegen

1. Web → Katalog der Anwendung: `GetTemplatesAsync(contractTypeName)` liefert die Vorlagen der installierten Module,
   die den Vertrag erfüllen, mit Anzeigename, Modul und Einstellungsfeldern (`DescribeSettings` über den Typ, wie
   heute).
2. Web → Katalog: `CreateObjectAsync(templateKey, name, settingsJson)`.
3. Die Anwendung prüft die Einstellungen gegen den Einstellungstyp. Bei einem Fehler wird nichts angelegt. Sonst legt
   sie die Einstellungen **gleich hier** ab (`DeviceSettingsStore`, wie heute). Eine `Scope`-Instanz kann verlangt
   werden, sobald sie in `plugins.json` steht, und soll nie mit leeren Einstellungen entstehen.
4. Die Anwendung ruft am Update-Dienst `CreatePluginInstanceAsync(moduleId, typeName, name)` auf.
5. Der Dienst prüft:
   - Der Aufrufer ist die angemeldete Anwendung dieses Ziels, mit derselben Prüfung wie bei `AssignModulesAsync`.
   - Das Modul ist zugewiesen und installiert.
   - Der Typ hat die Marke `Template`.
   - Der Name ist gültig und noch frei.

   - Liegt eine neue Fassung des Moduls schon bereit, hat auch sie den Typ als Vorlage. Sie wurde ohne die neue
     Instanz geprüft (C.1).

   Sind alle Prüfungen erfüllt, schreibt er `plugins.json`.
6. Lehnt der Dienst ab, entfernt die Anwendung die Einstellungen aus Schritt 3 wieder.
7. Wann die Instanz wirkt, hängt vom Modus ab (A.1):
   - `Scope`: Der Lader liest `plugins.json` neu, und die Instanz entsteht, sobald sie verlangt wird.
   - `ScopeStartup`: ab dem nächsten Scope.
   - `Singleton`: erst nach einem Neustart. Der Katalog gibt das als Ergebnis zurück („wirkt nach Neustart“). Der
     Neustart läuft wie ein Durchgang nur mit Modulen über den Update-Dienst: Aufforderung, Aufschub, nie dringend.

### C.3 Instanz entfernen

`RemoveObjectAsync(name)` am Katalog, dann `RemovePluginInstanceAsync(name)` am Dienst.

- Nur `Template`-Instanzen lassen sich entfernen.
- Die Einstellungen in der Benutzerdatei entfernt die Anwendung.
- Eine Instanz, die an einer Kasse gebunden ist (`ClientAppServiceBinding`), meldet das Web vor dem Entfernen.

### C.4 Modul abwählen

Template-Instanzen des Moduls sind nach dem Abwählen verwaist. Dafür gilt:

- Das Web warnt vor dem Abwählen, wenn Instanzen bestehen. Die Liste liefert `GetObjectsAsync` bzw. der Stand des
  Dienstes.
- Beim Entfernen des Moduls entfernt der Dienst alle seine Instanzen aus `plugins.json`.
- Der Lader überspringt jede Instanz, deren Modul oder Typ fehlt. Er protokolliert dazu einen Fehler, und die
  Anwendung startet trotzdem. Der Katalog führt die Instanz als „Modul fehlt“.

## D. Mehrere Anwendungen an einem Update-Dienst

**Heute:** Ein Dienst betreut genau **eine** Anwendung, und zwar eine, die in der Windows-Sitzung eines Benutzers
läuft:

- Er startet sie mit `StartInSession`.
- Er beendet sie über die Prozess-Id.
- Er wechselt zwischen Sitzungen nach §113.

Zwei Dienste nebeneinander, mit eigenem `HubServiceName`, gingen für zwei solche Anwendungen wohl, sind aber nicht
erprobt. Einen **Windows-Dienst als Ziel** kann der Update-Dienst gar nicht bedienen.

**Vorschlag:** `UpdateServiceOptions.Targets`, eine Liste von Zielen, die sich Feed, Signaturprüfung und
Download teilen:

```json
"Targets": [
  { "Id": "PosAgent",  "Kind": "SessionApplication", "ProductId": "MiniStore.PosAgent",
    "ApplicationPath": "C:\\Program Files\\…\\PosAgent.exe", "UpgradeCode": "{…}" },
  { "Id": "TaskHost",  "Kind": "WindowsService", "ProductId": "MiniStore.TaskHost",
    "ServiceName": "MiniStoreTaskHost", "ApplicationPath": "C:\\Program Files\\…\\TaskHost.exe", "UpgradeCode": "{…}" }
]
```

Je Ziel gibt es eigene Werte für:

- installierte Fassung, `UpgradeCode`;
- Modulordner, Modulzuweisung, `plugins.json`;
- Aufschub und Stand.

**Gemeinsam** sind Feed, Zertifikate, Paketablage, Pipe und Hub. Es läuft immer nur **ein** `msiexec` zur selben
Zeit. Die Ziele kommen der Reihe nach an die Reihe.

| | `SessionApplication` | `WindowsService` |
|---|---|---|
| Beenden | Aufforderung, Aufschub, Frist (wie heute) | Aufforderung über die Pipe, Aufschub für laufende Aufgaben; nach Frist `ServiceController.Stop` |
| Starten | `StartInSession` in der aktiven Sitzung | `ServiceController.Start` |
| Rollen / Sitzungswechsel (§113) | ja | nein, es gibt genau eine Instanz |
| Anmeldung an der Pipe | Ordner von `ApplicationPath` (wie heute) | ebenso, dazu: der Prozess ist der Dienst `ServiceName` |

Das Ziel ergibt sich aus der Anmeldung: Ordner und Produkt der Anwendung bestimmen das Ziel. Eine Anwendung kann
nie Module oder Instanzen eines **anderen** Ziels anfordern.

Bringen Agent und Dienst **ein gemeinsames MSI** mit, gibt es zwei Ziele mit demselben `UpgradeCode`. Die Fassung
der Anwendung ist dann eine, Module und Instanzen bleiben je Ziel getrennt. In `requires` (§116.1) steht das Ziel
über seine `ProductId`.

**Rückwärtskompatibel:** Ohne `Targets` bilden die heutigen Einzelwerte ein Ziel `default` vom Typ
`SessionApplication`.

## E. Abgrenzung

- **Hochladen** beim Publish gehört nicht dazu (§124, bewusst zurückgestellt).
- **Web-Masken** sind hier nur grob umrissen: Vorlage wählen, Instanz anlegen und entfernen, im bestehenden Reiter
  für die Geräte der ClientApp. Sie werden mit der Umsetzung ausgearbeitet.
- **Spiegel im Web:** Es gibt weiterhin keinen (§72). Wird der Rechner neu aufgesetzt, werden Module und Instanzen
  neu angelegt.

## Offene Punkte / zu prüfen

**Geklärt (2026-11-19):**

- Neue `Scope`-Instanz ohne Neustart: Die Factory puffert kein „kenne ich nicht“. `RequestScopePlugin` und
  `DescribeScopedPlugin` fragen die Lader bei jedem Aufruf neu. **Falle:** Bei genau einem Lader überspringt die
  Factory `HasScopedPlugin` und ruft direkt `GetScopedPlugin`. Beide dürfen darum nie werfen.
- Einbindung des Laders: Er ist ein Plugin der Hauptkonfiguration wie `SettingsDrivenLoader`, mit dem Pfad zu
  `plugins.json` als Parameter. Die Anwendungen brauchen keinen eigenen Startcode.
- Ein Modus je Typ (A.1). Eine Liste erlaubter Modi lässt sich später ohne Bruch ergänzen.
- `Disabled` in `plugins.json`: vorerst nicht. Abschalten heisst entfernen, die Einstellungen in der Benutzerdatei
  bleiben stehen.
- `Singleton`-Instanz entfernen: Der Katalog zeigt sie bis zum Neustart als „entfernt, wirkt nach Neustart“.
- Ohne `Targets` liegt `plugins.json` gleich unter `targets\default\`. Abschnitt D muss den Pfad nicht verschieben.
- Vorlagen im Katalog (C.2, Schritt 1): Die **Anwendung** liest die Marken der installierten Module, über
  `ModuleContextSelector`. `DescribeSettings` braucht den echten Typ ohnehin. Der Dienst prüft die Marke beim Anlegen
  trotzdem selbst, per `MetadataReader`.
- Gleicher Infrastruktur-Name in zwei Modulen: Beim Umschalten ist das ein Fehler. Das zweite Modul wird nicht
  installiert, und `UpdateStatus.Modules` nennt den Grund (wie ein fehlender Template-Typ, C.1).
- Generische Typargumente: siehe A.2.

- Instanznamen: Buchstabe am Anfang, danach `[A-Za-z0-9_.-]`, höchstens 64 Zeichen
  (`ModulePluginMarks.IsValidInstanceName`). Strenger als §72, weil der Name in `plugins.json`, im Pfad
  `Devices:<Name>` und in Logs landet.
- `$DeviceSettings`: Der Lader liest **am Typ** ab, ob er `IHasDeviceSettings<T>` implementiert (Metadaten, über den
  Namen, ohne Verweis auf die Agent-Dienste). `plugins.json` hat dafür kein Feld. Ein Modul-Plugin hat darum genau
  zwei mögliche Konstruktoren: ohne Parameter oder mit den Geräte-Einstellungen.
- Eine Instanz, die beim Bau scheitert, wird protokolliert und übersprungen. Die Anwendung startet trotzdem.
- Eine Version, die ein Modul-Update wegen einer verwaisten Template-Instanz verweigert, gilt als gescheitert
  (`FailedModules`) und wird erst mit einer neueren Version wieder versucht, auch wenn die Instanz inzwischen entfernt
  wurde.

## Stand der Umsetzung

- **Schritt 1** (Marke, Prüfung beim Publish): `ModulePluginAttribute`, `ModulePluginMarks` (eine Quelle, auch in
  `ModuleBuild` kompiliert), Prüfung in `ModulePackager` (`ITVMOD001` Fehler, `ITVMOD003` Warnung).
- **Schritt 2** (`plugins.json`, Lader, Abgleich):
  - Format: `ModulePluginInstance` / `ModulePluginInstanceFile` (ITVComponents.Plugins), Felder als Text, damit ein
    unbekannter Wert nur die eine Instanz unbrauchbar macht.
  - Dienst: `ModulePluginRegistry` unter `<DataDirectory>\targets\default\plugins.json`. Prüft eine vorbereitete
    Version vor dem Umschalten (C.1). Gleicht beim Umschalten, Zurückschalten und Entfernen ab (C.4) und beim Start
    des Dienstes für Module, die schon vorher installiert waren.
  - Anwendung: `ModulePluginLoader` als Plugin der Hauptkonfiguration:
    `[ITVComponents.Plugins.dll]<ITVComponents.Plugins.Modules.ModulePluginLoader>"<plugins.json>","<modules>",$factory`.
    Vorrang anderer Lader über `PluginFactory.FindScopedPluginLoader`.
- **Schritt 3** (Vorlagen und Instanzen anlegen bzw. entfernen):
  - Dienst: `CreatePluginInstanceAsync` / `RemovePluginInstanceAsync` an `IUpdateServiceFacade`, Ergebnis
    `PluginInstanceResult`. Nur eine angemeldete Anwendung darf das, wie bei `AssignModulesAsync`. Die Prüfungen
    liegen in `ModuleManager` (zugewiesen, installiert) und `ModulePluginRegistry` (Marke `Template` in der aktiven
    und einer bereitliegenden Fassung, Name gültig und frei; entfernen nur `Template`). Anlegen und `CheckPrepared`
    laufen unter derselben Sperre der Datei.
  - Anwendung: `UpdateServiceClient` erfüllt `IModulePluginInstanceService` (ITVComponents.Plugins).
    `ModulePluginLoader` zählt die Vorlagen der installierten Module auf (`GetTemplates`, aus den Metadaten) und
    nennt angelegte Instanzen jedes Modus (`GetInstance`).
  - Katalog: `IAgentObjectCatalog.GetTemplatesAsync` / `CreateObjectAsync` / `RemoveObjectAsync`, mit
    `AgentTemplateInfo` (Schlüssel `<Modul>/<Typ>`, Lademodus, Einstellungsfelder) und `AgentObjectChangeResult`
    (`EffectiveAfterRestart` bei `Singleton`). Konfiguration:
    `[ITVComponents.AgentServices.dll]<ITVComponents.AgentServices.PluginAgentObjectCatalog>$factory,$modulePlugins,$updates`.
    Einstellungen von Modul-Instanzen der Modi `Singleton` und `ScopeStartup` lassen sich wie die von Geräten lesen
    und ändern. `IDeviceSettingsStore` hat dafür `Remove` bekommen.
- **Schritt 4a** (mehrere Ziele vom Typ `SessionApplication`):
  - Konfiguration: `UpdateServiceOptions.Targets` (`UpdateTargetOptions`: `Id`, `Kind`, `ProductId`,
    `ApplicationPath`, `ApplicationArguments`, `UpgradeCode`, `ModulesDirectory`, `RequireApplicationPath`).
    `ResolveTargets` macht daraus je Ziel eine Kopie der Optionen. Ohne `Targets` bilden die Einzelwerte das Ziel
    `default`. Beim Start abgelehnt wurden damals `WindowsService` (Schritt 4b) und zwei Ziele mit demselben
    `UpgradeCode` (gemeinsames MSI, siehe unten); abgelehnt bleiben zwei Ziele im selben Ordner. Die Id muss sich als Ordnername eignen und darf nur einmal vorkommen.
  - Je Ziel (`UpdateTarget`) gibt es `AgentRegistry`, `UpdateCoordinator`, `RoleCoordinator`, `ModuleManager`, den
    Stand unter `targets\<Id>\state.json` und `plugins.json`. Das Ziel `default` behält seinen Stand direkt in
    `DataDirectory`, damit die Umstellung auf `Targets` nichts verliert. Damit sind auch Stand und Aufschub-Schlüssel
    je Ziel getrennt.
  - Gemeinsam: Feed-Adresse, `HttpClient`, Paketablage (mit Sperre je Datei), Pipe und Hub. Ein Riegel lässt immer nur
    ein `msiexec` laufen; das zweite Ziel wartet mit beendeter Anwendung. Den Feed liest jedes Ziel selbst (eigenes
    `ETag`).
  - Fassade: Die Anmeldung wählt das Ziel über das Produkt und bei mehreren Zielen desselben Produkts über den Ordner.
    Jeder spätere Aufruf geht nur an das Ziel des Aufrufers. `GetStatusAsync` ohne Anmeldung antwortet nur noch, wenn
    es genau ein Ziel gibt.
  - Verträge: `TargetId` an `AgentRegistration`, `UpdateStatus`, `ShutdownRequest` und `RoleChange`. Der Client
    verwirft Ereignisse eines anderen Ziels. Ein Ereignis ohne `TargetId` (älterer Dienst) gilt weiter.
  - Logs der Ziele tragen `[<Id>]` (ausser beim Ziel `default`).
- **Schritt 4b** (Ziel `WindowsService`):
  - Konfiguration: `Kind = "WindowsService"` braucht `ServiceName`. Zwei Ziele dürfen nicht denselben Dienst nennen.
    Die Einzelwerte ohne `Targets` bleiben immer `SessionApplication`.
  - Anmeldung: Ordner wie bisher, dazu fragt der Update-Dienst (LocalSystem) den SCM nach dem Dienst
    (`QueryServiceStatusEx`). Angenommen wird nur, wenn die Prozess-Id des Dienstes die des Pipe-Aufrufers ist (vom
    Betriebssystem, nicht vom Aufrufer). Abgelehnt wird mit Grund, wenn der Dienst nicht installiert ist, nicht läuft,
    seinen Prozess teilt (kein `SERVICE_WIN32_OWN_PROCESS`) oder der SCM nicht antwortet (dann als Fehler im Log).
  - `ServiceApplicationControl`: Starten über den SCM, ausser der Dienst läuft schon (oft startet ihn das MSI selbst).
    Nach der Frist eines dringenden Updates wird der Dienst gestoppt. Hängt er länger als 60 s im Stoppen, wird sein
    Prozess beendet. Die Sitzung spielt keine Rolle.
  - Keine Rollen: Ein Dienst-Ziel hat keinen `RoleCoordinator`. Die Anmeldung meldet immer `Active`, und
    `ReportReleasedAsync` hat keine Wirkung.
  - SCM-Aufrufe direkt über `advapi32` (`UpdateNativeMethods`), weil `ServiceController` die Prozess-Id nicht liefert.
  - Ein Dienst, der aufgefordert wird zu enden, stoppt sich selbst (über seinen Host). Nur dann endet die
    Pipe-Verbindung und der Durchgang läuft weiter. Das ist Sache der Anwendung, wie bei `SessionApplication`.

- **Schritt 5** (Web-Masken, im Reiter „Geräte“ der ClientApp):
  - Lader: `ModulePluginLoader.GetInstances()` liefert jede Instanz aus `plugins.json` als
    `ModulePluginInstanceStatus`, also auch die unbrauchbaren, mit `Problem` und `ModuleMissing`.
  - Katalog: `GetTemplateChoicesAsync(templateKey, settingName, draftJson)` liefert Auswahlwerte für eine Vorlage, bevor
    es das Gerät gibt. Dafür gibt es eine gemeinsame Auswahllogik mit `GetChoicesAsync`. `GetModuleObjectsAsync()`
    liefert alle Instanzen aus Modulen in jedem Lademodus als `AgentModuleObjectInfo`, mit `Removable` (nur
    `Template`), `Usable`, `Problem` und `ModuleMissing`. Ein Agent ohne Modul-Plugins antwortet leer.
  - Web: `IAgentServiceAdministration` hat `GetTemplatesAsync(clientAppId, slot)` (Vertrag der Rolle),
    `GetTemplateChoicesAsync`, `CreateObjectAsync`, `GetModuleObjectsAsync` und `RemoveObjectAsync` bekommen.
    Schreiben und das Lesen von Vorlagen brauchen `Apps.Write`, die Liste `Apps.View`.
  - Maske `ClientAppServiceSlots`: Jede Rolle hat „Neu aus Vorlage“ (Vorlage, Name, Einstellungen mit Auswahlwerten).
    Danach steht das neue Gerät in der Rolle bereit. Gespeichert wird es erst mit „Speichern“. Darunter steht die
    Liste „Geräte aus Modulen“ mit Lademodus, Zustand (geladen / Modul fehlt / nicht geladen) und den Rollen, die das
    Gerät benutzen. „Entfernen“ fragt nach und warnt, wenn Rollen es noch benutzen. Diese Rollen bleiben besetzt.
    Ein älterer Agent ohne die neuen Aufrufe wird erkannt. Die Rollen bleiben dann bedienbar, und die Maske sagt es.

- **Warnung vor dem Abwählen** (C.4):
  - `IClientAppUpdateSync.GetModuleObjectsAsync(tenantId, clientAppId)` fragt den Katalog einer Kasse über den
    `IAgentConnector`. Eine Kasse, die aus ist, oder ein älterer Agent gilt als nicht erreicht (`ModuleObjectsResult`).
  - `IUpdateAdminHandler.CheckClientAppModuleRemovalAsync` / `CheckTemplateModuleRemovalAsync` liefern einen
    `ModuleRemovalCheck`: welche Module wegfallen, welche Geräte aus Vorlagen dieser Module je Kasse mitgehen
    (Infrastruktur nicht) und welche Kassen nicht gefragt werden konnten. Sie ändern nichts.
    - Je Kasse: Ein Modul, das die Vorlage der Kasse mitbringt, fällt nicht weg.
    - Je Vorlage: Gefragt wird jede aktive Kasse der Vorlage in allen Mandanten, die das Modul nicht selbst
      zugewiesen hat (wie beim Verteilen).
  - Masken: `ClientAppUpdates` (übersetzt) und die Vorlagen in `UpdateReleases` (englisch wie die übrige
    Plattform-Seite) fragen vor dem Speichern nach, wenn Geräte mitgehen oder eine Kasse nicht erreicht wurde.
    Abbrechen wird protokolliert.
  - Rollen: Jede Warnung nennt auch die Rollen der Kasse (`ModuleRemovalWarning.Bindings`: Rolle, Gerät, aktiv), die
    ein Gerät des Moduls benutzen. Dazu zählen auch Infrastruktur-Geräte des Moduls; eine solche Bindung allein löst
    die Rückfrage aus. Die Bindungen liest der Handler aus dem Kontext, wenn dieser `IAgentServiceBindingContext`
    implementiert. Ohne Agent-Dienste gibt es keine Bindungen, der Vertrag des Handlers verlangt sie darum nicht.
    Die Rollen bleiben nach dem Abwählen besetzt und werden nicht bedient, bis jemand sie neu bindet.

- **Gemeinsames MSI** (D): Ziele mit demselben `UpgradeCode` bilden eine Gruppe; der Durchgang beendet die
  Anwendungen aller Ziele der Gruppe, bevor das MSI läuft.
  - Gruppe = Ziele mit demselben `UpgradeCode` (`InstallGroup`, im Plugin gebildet). Das erste Ziel in der
    Konfiguration ist der Lead: Nur er wählt das Basispaket (unter seiner `ProductId`). Folger liefern in `SelectBase`
    null und haben nur Modul-Durchgänge.
  - Halten: Eine Gruppe hat höchstens einen Halter (Lead mit Basispaket = ganze Gruppe, sonst nur das eigene Ziel).
    Belegt wird vor dem Beenden bzw. Installieren in `DecideAsync`. Freigegeben wird überall, wo `shutdownRequested`
    gelöscht wird (EndPass, Defer, kein Paket, neues Paket, Abwahl), sowie bei Dispose. Die Freigabe weckt alle Mitglieder.
    Ein Ziel, dessen Gruppe ein anderes hält, überspringt seinen Durchgang (auch Pending und Rollback).
  - Lead mit Basispaket: Er wartet auf die Agenten aller Mitglieder. Die Aufforderung geht an jedes Mitglied mit
    dessen `TargetId`; dort werden `shutdownRequested` (ein Ende zählt nicht als Absturz) und `sessionsToRestart`
    gesetzt. Nach Ablauf der Frist beendet jedes Mitglied seine Agenten über seine eigene `IApplicationControl`.
  - M1: Der Lead plant auch die Module jedes Folgers für die neue Fassung. Fehlt eins, wird blockiert.
  - Installieren: Der Lead staged die Module aller Mitglieder und schreibt jedem Folger ein eigenes
    `Pending` (gleiches Paket und Log, eigene Module und Sitzungen). Nach dem Lauf schliesst er jeden Folger mit
    demselben Ergebnis ab (`CompleteAsync`: Module aktivieren bzw. verwerfen, Sitzungen neu starten). Startet der
    Dienst neu, schliesst jedes Ziel sein `Pending` selbst ab.
  - Weiterleiten: Defer eines Folgers ohne eigene Arbeit geht an den Halter. `CheckNow` eines Folgers ruft auch den
    Lead. `HoldsApplication` eines Folgers ist wahr, solange der Lead die ganze Gruppe hält (als Flag an der Gruppe,
    keine verschachtelten Sperren). Der Status eines Folgers ohne eigene Arbeit spiegelt die Basis-Felder des Leads.

**Noch offen:**

- Ein älterer Client an einem Dienst mit mehreren Zielen sieht die Ereignisse aller Ziele. Darum müssen die Anwendungen
  vor der Umstellung auf `Targets` den neuen Client haben. Mit nur einem Ziel ändert sich nichts.

## Vorgeschlagene Reihenfolge

1. Marke `ModulePlugin` mit Art und `PluginLoadType`, und die Prüfung in `ModulePackager` (nur geschlossene Typen,
   A.2).
2. `plugins.json`: Format, Lader in der Anwendung, Abgleich der Infrastruktur-Instanzen beim Umschalten,
   einschliesslich Rückweg.
3. Katalog und Dienst: Vorlagen und Instanzen anlegen bzw. entfernen.
4. Mehrere Ziele, zuerst `SessionApplication` × n, dann `WindowsService`.
5. Web-Masken.
