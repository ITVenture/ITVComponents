# Issue: Die Beschriftungen von `[DeviceSetting]` lassen sich nicht übersetzen

**Status:** umgesetzt (2026-11-16, noch nicht committet). Punkt 1 wie vorgeschlagen
(`SettingFieldProjection.Translate` an beiden Masken, auch am Server-Schritt des Assistenten). Punkt 2
**abweichend**: die Prüfung läuft am Agenten, dessen Kultur nicht die des Benutzers ist. `SettingError`
trägt darum `Kind` (`Required`/`Invalid`/`Unknown`) und `Detail`, `Message` nennt das Feld beim **Namen**,
und die Maske formuliert über `SettingFieldProjection.DescribeError` lokalisiert mit der übersetzten
Beschriftung. Ältere Agenten (ohne `Kind`) → Rückfall auf `Message`.
**Datum:** 2026-11-16
**Quelle:** MiniStore-Session (Konsument), beim Vorbereiten der Mehrsprachigkeit (de/fr/it/en).
**Toolkit-Stand:** `5.0.0-PRE271`.

## Der Fall

Der MiniStore stellt alle Oberflächen auf vier Sprachen um. Die Einstellungen der Kassengeräte
(Epson-Bondrucker, Schublade) kommen über den Agent-Katalog ins Web und werden dort in
*Connectivity → ClientApps → Reiter „Geräte“* (`ClientAppServiceSlots`) bzw. im Terminal-Assistenten
(`TerminalWizardDialog`, Geräteschritt) gepflegt. Ihre Beschriftungen stehen am Agenten im Attribut:

```csharp
[DeviceSetting("Anschluss", Order = 1, HelpText = "…")]
[DeviceSetting("Windows-Drucker", Order = 2, ChoiceSource = WindowsPrinterChoices.PluginName, …)]
[DeviceSetting("Port", Order = 4, HelpText = "Nur beim Anschluss „Netzwerk“. Epson-Drucker: 9100.")]
```

Diese Texte erscheinen in jeder Sprache wörtlich. Ein französischsprachiger Ladeninhaber sieht
„Anschluss“ und „Zeichentabelle“.

## Was im Toolkit schon da ist

`DeclaredFieldsForm` kann übersetzen: es hat den Parameter `TranslateText` und wendet ihn auf
`Label`, `HelpText` und die Auswahl-Beschriftungen an (`DeclaredFieldsForm.razor:329–340`).
`CustomCompanyInfoTabs` übergibt ihn bereits. Und für die Navigation gibt es die JSON-Konvention
(`StringExtensions.Translate` → `DictionaryExtensions.Translate`): `{"de":"…","fr":"…","Default":"…"}`
wird nach `CurrentUICulture` aufgelöst, alles andere bleibt wie es ist.

## Die betroffenen Stellen

| Stelle | Was fehlt |
|---|---|
| `AgentServiceViews/ClientAppServiceSlots.razor:103` | `DeclaredFieldsForm` ohne `TranslateText` |
| `BillingViews/Components/Payments/TerminalWizardDialog.razor:67, 94` | dito (Server- und Geräteschritt) |
| `AgentServices/DeviceSettingsReflection.cs:130, 145` | Die Prüfmeldungen setzen `field.Attribute.Label` roh ein (`'{Label}' is required.`). Mit einem JSON-Label stünde dort das ganze JSON |

## Vorschlag

1. In den beiden Masken `TranslateText` übergeben, mit derselben JSON-Konvention wie bei der
   Navigation (`v => v.Translate(CultureInfo.CurrentUICulture.Name)`). Ein Agent, der weiter einfache
   Texte schreibt, merkt nichts davon.
2. In `DeviceSettingsReflection` die Beschriftung in den Meldungen ebenfalls über `Translate` auflösen,
   oder den Feldnamen statt der Beschriftung melden und die Maske formulieren lassen.
3. Dasselbe für die Beschriftungen, die ein `IDeviceChoiceProvider` liefert, da sie durch dieselbe
   Maske laufen. Das ergibt sich mit Punkt 1 von selbst.

Eine Alternative wäre ein Ressourcen-Schlüssel samt Ressourcentyp am Attribut. Das passt hier aber
schlecht: die Ressourcen lägen im Agenten, angezeigt wird im Web. Die JSON-Konvention reist mit dem
Text über den Hub und braucht auf keiner Seite eine Ressourcendatei.

## Umgehung

Keine. Bis dahin bleiben die Beschriftungen der Geräte-Einstellungen deutsch.
