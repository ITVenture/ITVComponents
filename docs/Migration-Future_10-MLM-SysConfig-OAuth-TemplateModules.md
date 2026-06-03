# SysConfig-Erweiterung — globale OAuth-Services & TemplateModules (Branch `Future_10`)

Companion zu [`Migration-Future_10-MLM.md`](Migration-Future_10-MLM.md) und
[`Migration-Future_10-MLM-Packaging.md`](Migration-Future_10-MLM-Packaging.md).

Diese Erweiterung betrifft den **System-Konfigurations-Abgleich** (`SysConfigurationHandler`):
Bisher fehlten zwei globale/neutrale Entity-Gruppen im Export/Compare. Jetzt sind sie drin:

1. **Globale `ExternalOAuthServices`** (`TenantId == null`)
2. **`TemplateModules`** (Module → Configurator → Parameter, plus Module → Script)

> **TL;DR:** Additive Änderung. **Kein Code-Diff, keine neue EF-Migration.** Wer den
> System-Config-Transfer nutzt, muss die Config **neu exportieren** und ein paar
> Betriebs-Hinweise beachten (OAuth-Secrets, TemplateModule-Test).

---

## 1. Kein Code-Change, keine neue DB-Migration

Die betroffenen Entities existierten alle bereits (`ExternalOAuthService`,
`TemplateModule`, `TemplateModuleConfigurator`, `TemplateModuleConfiguratorParameter`,
`TemplateModuleScript`). Es wurde **nur die Export-/Compare-Logik** ergänzt — **kein
Schema-Change**. Aus dieser Änderung folgt daher **keine** neue EF-Migration und keine
Anpassung am Konsumenten-Code.

(Unabhängig von der laufenden Paket-Migration — die bleibt davon unberührt.)

---

## 2. System-Config neu exportieren

Damit die zwei neuen Sektionen mitsynchronisiert werden, muss der bestehende
System-Config-Export **nach dem Upgrade neu gezogen** werden. Das exportierte JSON
(`SystemTemplateMarkup`) enthält jetzt zusätzlich:

```jsonc
{
  // ... bestehende Sektionen ...
  "ExternalOAuthServices": [ /* nur globale Services (TenantId == null) */ ],
  "TemplateModules":       [ /* Module mit Configurators + Parameters + Scripts */ ]
}
```

Alte, vor dem Upgrade exportierte Configs funktionieren weiter — aber **ohne** diese
Sektionen (siehe Punkt 5).

---

## 3. ClientSecret wird NICHT transportiert (bewusst)

`ExternalOAuthService.ClientSecret` ist **nicht** Teil des Exports — Secrets sollen nicht
in einer (ggf. gespeicherten/versionierten) Config mitreisen.

- **Beim Import** wird ein globaler OAuth-Service mit **leerem** Secret angelegt.
  ⇒ Das echte `ClientSecret` muss am **Zielsystem manuell** nachgetragen werden.
- **Bei Updates** wird ein vorhandenes Secret am Ziel **nie überschrieben/angefasst**.

Identisches Verhalten wie bei der Tenant-Template-Logik.

---

## 4. TemplateModule-Import: vor Produktiveinsatz testen

Der TemplateModule-Abgleich ist **neu** und bisher nur **compile-**, nicht
**apply-getestet**. Hintergrund: TemplateModule ist 3-stufig, und ein
`TemplateModuleConfigurator` ist nur **je Modul** eindeutig (nicht global). Die
Parent-Auflösung Configurator→Parameter nutzt daher ein neues, modul-qualifiziertes,
null-sicheres Linq-Filter-Muster, dessen Apply-Pfad zur **Laufzeit** als Script läuft
(vom Compiler nicht abgedeckt).

**Empfohlener Roundtrip-Test (in Test-/Zweitsystem, nicht Prod):**

1. In **System A** ein/mehrere TemplateModule(s) mit Configurators, Parametern und
   Scripts anlegen.
2. System-Config aus A **exportieren**.
3. Config in **System B** (frisch / ohne diese Module) **importieren/anwenden**.
4. In B prüfen:
   - Module vorhanden, `RequiredFeature` korrekt verknüpft.
   - Configurators je Modul vorhanden (`Name`, `ConfiguratorTypeBack`,
     `CustomConfiguratorView`, `DisplayName`).
   - Parameter am **richtigen** Configurator (wichtig: zwei Module mit
     gleichnamigem Configurator dürfen sich nicht vermischen).
   - Scripts (`ScriptFile`) je Modul vorhanden.
5. Anschließend ein **Update-Roundtrip**: in A einen Parameterwert ändern, erneut
   exportieren/anwenden → in B greift nur das Delta.

> Wenn dieser Test grün ist, ist der Pfad freigegeben. Bei Auffälligkeiten bitte
> melden — dann erweitern wir den Mechanismus gezielt.

---

## 5. Backward-Compatibility ist abgesichert

- Wird eine **alte** Config **ohne** die neuen Sektionen importiert, werden diese
  **übersprungen** — es wird **nichts gelöscht**.
- Wird eine Config **mit** Sektion (auch leerer) importiert, wird **voll**
  synchronisiert — inkl. Deletes. Das ist die etablierte Semantik aller Sektionen:
  *„Export = vollständiger Soll-Zustand"*.

⇒ Ein versehentliches Massen-Delete durch Upload einer veralteten Datei ist
ausgeschlossen.

---

## 6. Edge-Case (nur zur Kenntnis)

Modulnamen (`TemplateModuleName`) sollten **kein `"`-Zeichen** enthalten — der Name wird
in den Parent-Auflösungs-Filter interpoliert. Identifier-artige Namen (Normalfall) sind
problemlos.

---

## 7. Eigene Handler-Subklasse? (i.d.R. nicht relevant)

Falls eine **eigene Subklasse** von `SysConfigurationHandler` `DescribeSystem` **oder**
`PerformCompareInternal` überschreibt (unüblich — normalerweise wird nur die konkrete
Strategie-Klasse instanziiert), müssen die zwei neuen Aufrufe selbst nachgezogen werden:

- in `DescribeSystem`: `ExternalOAuthServices` + `TemplateModules` befüllen,
- in `PerformCompareInternal`: `CompareExternalOAuthServices(...)` +
  `CompareTemplateModules(...)` aufrufen.

Bei Standard-Nutzung (nur Instanziierung der konkreten Handler-Klasse) ist nichts zu tun.
