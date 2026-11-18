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
3. Die Anwendung prüft die Einstellungen gegen den Einstellungstyp. Bei einem Fehler wird nichts angelegt.
4. Die Anwendung ruft am Update-Dienst `CreatePluginInstanceAsync(moduleId, typeName, name)` auf.
5. Der Dienst prüft:
   - Der Aufrufer ist die angemeldete Anwendung dieses Ziels, mit derselben Prüfung wie bei `AssignModulesAsync`.
   - Das Modul ist zugewiesen und installiert.
   - Der Typ hat die Marke `Template`.
   - Der Name ist gültig und noch frei.

   Sind alle Prüfungen erfüllt, schreibt er `plugins.json`.
6. Die Anwendung legt die Einstellungen ab (`DeviceSettingsStore`, wie heute).
7. Wann die Instanz wirkt, hängt vom Modus ab (A.1):
   - `Scope`: Der Lader liest `plugins.json` neu, und die Instanz entsteht, sobald sie verlangt wird.
   - `ScopeStartup`: ab dem nächsten Scope.
   - `Singleton`: erst nach einem Neustart. Der Katalog gibt das als Ergebnis zurück („wirkt nach Neustart“). Der
     Neustart läuft wie ein Durchgang nur mit Modulen über den Update-Dienst: Aufforderung, Aufschub, nie dringend.

Scheitert Schritt 6, wird die Instanz in Schritt 4 wieder entfernt. Dafür ruft die Anwendung den Dienst erneut auf
und protokolliert dabei einen Fehler.

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

- Neue `Scope`-Instanz ohne Neustart: Findet die Factory sie, sobald der Lader `plugins.json` neu gelesen hat?
  Puffert sie die Antwort „kenne ich nicht“ von `HasScopedPlugin`?
- Reicht ein Modus je Typ, oder braucht es eine Liste erlaubter Modi in der Marke (A.1)?
- `Singleton`-Instanz entfernen: erst mit dem Neustart wirksam. Bis dahin läuft sie weiter, das muss der Katalog
  so anzeigen.
- Wie der neue Lader in die Factory des POS-Agenten und des GenericService kommt (`ServiceConfigHelper.PlugIns`
  bzw. `ServiceStartup`): Ist er selbst ein Plugin der Hauptkonfiguration wie `SettingsDrivenLoader`, oder hängt die
  Anwendung ihn beim Start ein?
- Muss eine Instanz in `plugins.json` abgeschaltet werden können (`Disabled`, §72), ohne sie zu entfernen?
- `WindowsService`-Ziel: Der Dienst muss sich an der Pipe als dieser Dienst ausweisen. Zu prüfen ist, ob der Name
  des Prozesses und die SCM-Abfrage dafür genügen.
- Umbau des Dienstes auf mehrere Ziele: Der Stand (`InstallStateStore`) und die Aufschub-Schlüssel
  (`UpdateCoordinator.WorkKey`) sind heute nicht je Ziel getrennt.

## Vorgeschlagene Reihenfolge

1. Marke `ModulePlugin` mit Art und `PluginLoadType`, und die Prüfung in `ModulePackager`.
2. `plugins.json`: Format, Lader in der Anwendung, Abgleich der Infrastruktur-Instanzen beim Umschalten,
   einschliesslich Rückweg.
3. Katalog und Dienst: Vorlagen und Instanzen anlegen bzw. entfernen.
4. Mehrere Ziele, zuerst `SessionApplication` × n, dann `WindowsService`.
5. Web-Masken.
