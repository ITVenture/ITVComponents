# Issue: `:-->`-Auflösung auch für die WebPart-Assembly-Liste (Anstoß aus MLM)

**Status:** UMGESETZT (2026-09-08) — alle vier Punkte inkl. Nebenbefund umgesetzt,
siehe „Umsetzung“ am Ende. Enthalten ab der auf `5.0.0-PRE214` folgenden Version. Offen ist nur noch
konsumentenseitig: der Behelf in `MLMManager.Web/Program.cs` kann ersatzlos entfallen, sobald MLM auf
das neue Paket zieht.
**Datum:** 2026-09-08
**Quelle:** MLMManager-Session (Konsument). MLM ist seit 2026-09-08 zwischen SQL Server und
PostgreSQL umschaltbar und braucht dafür genau eine anbieter-abhängige Zeile in
`appsettings-parts.json`.
**Toolkit-Stand:** `5.0.0-PRE214` (alle Fundstellen unten daran verifiziert, nicht aus dem Gedächtnis).

## Ziel (Konsumentenwunsch)

Der `AssemblyName` eines Eintrags in `ITVenture:WebParts:Assemblies` soll die bereits vorhandene
Verweis-Notation verstehen:

```jsonc
{
  // in appsettings-parts.json — bleibt an seiner Stelle sichtbar stehen
  "AssemblyName": ":-->ProviderDefaults:SysProviderAssembly??ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.SqlServer.dll",
  "DetailConfigPaths": {
    "ContextSettings": "ITVenture:WebPartConfigurations:TenantViewTypeOptions",
    "ActivationSettings": "ITVenture:WebPartConfigurations:SecurityFeatureOptions"
  }
}
```

```jsonc
// appsettings.PostgreSql.json — per AddJsonFile(..., optional: true) angehängt
{ "ProviderDefaults": { "SysProviderAssembly": "ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.PostgreSql.dll" } }
```

Damit wählt **ein** Konfigurationswert das Anbieter-Assembly, und dieselbe Mechanik trägt später
Stage-Varianten (`appsettings.PostgreSql.Production.json`) ohne eine Zeile Code.

## Warum das heute nicht geht (verifiziert, PRE214)

Die Notation existiert und kann alles, was gebraucht wird — sie wird an dieser einen Stelle nur nicht
angewandt.

1. **Die Notation ist da.** `ITVComponents.SettingsExtensions/SettingsRefResolveExtensions.cs:80`
   behandelt `:-->Pfad:Zum:Schlüssel`, inklusive Vorgabewert über `??`
   (`SettingsRefResolveExtensions.cs:83-95`), aufgelöst gegen `configuration[expression]` — also gegen
   den zusammengeführten Konfigurationsbaum. Daneben liegt `$-->` für einen Formatierungs-Ausdruck
   (`:100`).

2. **Angewandt wird sie opt-in je WebPart**, jeweils auf dem *eigenen* Optionsmodell in
   `[LoadWebPartConfig]`:
   - `ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity/WebPartInit.cs:66`
   - `…TenantSecurity.SqlServer/WebPartInit.cs:41` und `…TenantSecurity.PostgreSql/WebPartInit.cs:41`
   - `ITVComponents.WebCoreToolkit.Net/WebPartInit.cs:29`, `…Net/OpenShiftHealth/WebPartInit.cs:31`
   - `ITVComponents.WebCoreToolkit.Saml/WebPartInit.cs:34`
   - `ITVComponents.WebCoreToolkit.InterProcessExtensions/WebPartInit.cs:22`

3. **Die Assembly-Liste des Managers geht diesen Weg nicht.**
   `ITVComponents.WebCoreToolkit/AspExtensions/WebPartManager.cs:69`:

   ```csharp
   var options = config.GetSection<WebPartOptions>("ITVenture:WebParts");
   ```

   und `ITVComponents/Settings/Native/NativeSettingsExtensions.cs:14` ist ein nacktes `cfg.Bind(retVal)`
   ohne `RefResolve`. Ein `:-->…` in `AssemblyName` bliebe also wörtlich stehen und liefe in einen
   fehlschlagenden `Assembly.Load`.

### Fazit der Lücke

Es fehlt kein Mechanismus, sondern eine Anwendung: `RefResolve` ist im Toolkit breit etabliert, und die
WebPart-Assembly-Liste ist die eine Stelle, an der eine anbieter- oder umgebungsabhängige Zeichenkette
naheliegt — und an der es nicht geht.

## Was umgesetzt werden müsste

Drei Punkte; der zweite ist der, den man leicht übersieht:

1. **`RefResolve` aufrufen** — in `WebPartManager.cs` direkt nach Zeile 69:
   ```csharp
   var options = config.GetSection<WebPartOptions>("ITVenture:WebParts");
   config.RefResolve(options);
   ```

2. **`[AutoResolveChildren]` auf `WebPartOptions.Assemblies`**
   (`ITVComponents.WebCoreToolkit/Options/WebPartOptions.cs:12`). `ResolveObjProps`
   (`SettingsRefResolveExtensions.cs:41`) steigt **nur** in Member ab, die dieses Attribut tragen —
   ohne das wird die Liste nie betreten und Punkt 1 bliebe wirkungslos. Vorbilder:
   `TenantSecurity/Shared/Options/ActivationOptions.cs:59`,
   `Saml/Configuration/SamlOptions.cs`, `InterProcessExtensions/Options/InjectableProxyOptions.cs:13`.

3. **Neue Projektreferenz** `ITVComponents.WebCoreToolkit` → `ITVComponents.SettingsExtensions`.
   Die gibt es heute nicht. **Kein Ringschluss**: `ITVComponents.SettingsExtensions.csproj` verweist nur
   auf `ITVComponents.Formatting` und `ITVComponents`.

### Nebenbefund, damit die Umsetzung vollständig ist

`AssemblyPartConfiguration.DetailConfigPaths` ist ein `Dictionary<string,string>`
(`ITVComponents.WebCoreToolkit/AspExtensions/Options/AssemblyPartConfiguration.cs`). Der
Wörterbuch-Zweig in `ResolveObjProps` (`SettingsRefResolveExtensions.cs:23`) prüft auf
`IDictionary<string,object>`; ein `<string,string>` fällt deshalb in den Eigenschafts-Zweig, findet dort
keine String-Properties und passiert unverändert. `AssemblyName` und `DetailConfigPath` (beides
`string`) würden mit Punkt 1+2 aufgelöst, die **Werte in `DetailConfigPaths` nicht**.

Für den MLM-Anwendungsfall reicht `AssemblyName`. Soll die Auflösung aber „für WebPart-Einträge" gelten
und nicht „für zwei der drei Eigenschaften", gehört der Wörterbuch-Zweig auf `IDictionary<string,string>`
erweitert (oder generisch auf `IDictionary`).

## Verträglichkeit

- **Rückwärtskompatibel.** Nur Zeichenketten, die mit `:-->` bzw. `$-->` beginnen, ändern ihre
  Bedeutung. Kein bestehender Assembly-Name fängt so an.
- **Mehrfach-Auflösung unschädlich.** Nach dem ersten Durchlauf beginnt der Wert nicht mehr mit
  `:-->`; ein zweiter Aufruf ist ein No-op.
- **Keine neue Angriffsfläche.** Der Assembly-Name, der in `Assembly.Load` geht, kommt schon heute aus
  der Konfiguration; die Auflösung verschiebt nur, aus welchem Schlüssel derselben Konfiguration.
- **Zeitpunkt passt.** Aufgelöst wird beim Binden im `WebPartManager`-Konstruktor, also nachdem der
  Host seine JSON-Dateien angehängt hat.

## Was MLM heute stattdessen macht (und was entfiele)

`MLMManager.Web/Program.cs` überschreibt die Zeile **vor** dem `WebPartManager` im Speicher: es sucht in
`ITVenture:WebParts:Assemblies` den Eintrag, dessen `AssemblyName` auf `…TenantSecurity.SqlServer.dll`
oder `…TenantSecurity.PostgreSql.dll` lautet, und setzt `{Pfad}:AssemblyName` über einen
`AddInMemoryCollection`-Layer auf den aktiven Anbieter. Der Index wird **gesucht** und nicht angenommen,
damit ein Umsortieren der JSON-Datei nichts kippt.

Das funktioniert, ist aber Wiring im Host für etwas, das Konfiguration sein sollte. Mit der Änderung
fällt der Block ersatzlos weg.

**Zwei rein konfigurative Auswege wurden vorher geprüft und verworfen** — sie sind der Grund, warum die
Notation hier der richtige Hebel ist:

- *Array-Eintrag per Index überschreiben* (`{"Assemblies": {"7": {"AssemblyName": "…"}}}` in einer
  Provider-JSON). Funktioniert, koppelt aber hart an die Position in der Hauptdatei. Wird über dem
  Eintrag ein WebPart eingefügt, zeigt die Überschreibung **lautlos** auf die falsche Zeile.
- *Eintrag aus der Hauptdatei entfernen und je Provider an einem reservierten hohen Index anhängen*
  (`{"Assemblies": {"99": {…}}}`; `WebPartOptions.Assemblies` ist eine `List<T>`, der Binder hängt
  Kinder unabhängig vom Schlüssel an, Lücken sind unschädlich). Löst die Index-Kopplung, macht aber die
  Anbieter-Zeile in der Hauptdatei unsichtbar und ersetzt sie durch eine ungeschriebene Konvention.

Beides ist schlechter als ein benannter Schlüssel mit `??`-Vorgabe: der Wert bleibt sichtbar an seiner
Stelle, es gibt keine Positions- oder Zahlen-Konvention, und ein **fehlendes** Provider-JSON fällt auf
einen ausdrücklich hingeschriebenen Vorgabewert zurück statt auf ein stilles Zufallsergebnis.

## Abnahme

1. `AssemblyName` mit `:-->Schlüssel` und gesetztem Schlüssel → das aufgelöste Assembly wird geladen.
2. Derselbe Eintrag mit `:-->Schlüssel??Fallback.dll` und **nicht** gesetztem Schlüssel → `Fallback.dll`
   wird geladen (der Fall „Provider-JSON fehlt").
3. Ein gewöhnlicher `AssemblyName` ohne Präfix verhält sich unverändert (Regressionsschutz für alle
   bestehenden Konfigurationen).
4. Optional, falls der Nebenbefund mit umgesetzt wird: ein `:-->` in einem Wert von
   `DetailConfigPaths` wird ebenfalls aufgelöst.

---

## Umsetzung (2026-09-08, Zweig `Future_10`)

Alle vier Abnahmepunkte sind umgesetzt — der Nebenbefund also mit, damit die Auflösung „für
WebPart-Einträge“ gilt und nicht für zwei von drei Eigenschaften.

**`ITVComponents.WebCoreToolkit`**

- `AspExtensions/WebPartManager.cs`: `config.RefResolve(options)` direkt nach dem Binden im Konstruktor.
- `Options/WebPartOptions.cs`: `[AutoResolveChildren]` auf `Assemblies`.
- `AspExtensions/Options/AssemblyPartConfiguration.cs`: `[AutoResolveChildren]` auf `DetailConfigPaths`.
- `ITVComponents.WebCoreToolkit.csproj`: neue Projektreferenz auf `ITVComponents.SettingsExtensions`
  (kein Ringschluss, Build bestätigt).

**`ITVComponents.SettingsExtensions/SettingsRefResolveExtensions.cs`**

- Der Wörterbuch-Zweig greift jetzt zusätzlich für das **nicht-generische** `IDictionary`, damit ein
  `Dictionary<string,string>` wie `DetailConfigPaths` betreten wird. Der Zweig für
  `IDictionary<string,object>` bleibt daneben stehen und wurde **nicht** ersetzt: ein `ExpandoObject`
  implementiert `IDictionary<string,object>`, aber nicht das nicht-generische `IDictionary` — ein
  blosses Umschreiben hätte es aus der Auflösung geworfen.
- Der Abstiegs-Filter im `[AutoResolveChildren]`-Zweig schliesst Wörterbücher jetzt ebenfalls aus. Ohne
  das wäre ein `Dictionary<string,string>` als `IEnumerable<KeyValuePair<…>>` durchlaufen und je
  **Kopie** eines Structs aufgelöst worden — wirkungslos, aber unauffällig.
- `:-->Schlüssel` ohne Treffer **und** ohne `??`-Vorgabe schreibt jetzt eine Warnung ins Log, statt
  still `null` zu liefern. Genau dieser Fall taucht sonst später als unverständlich scheiterndes
  `Assembly.Load` auf.

**Tests** — `ITVComponents.WebCoreToolkit.Tests/WebPartAssemblyRefResolveTests.cs`, fünf Tests entlang
der Abnahme: aufgelöster Schlüssel, `??`-Vorgabe bei fehlendem Provider-JSON, unveränderter
gewöhnlicher Name (Regressionsschutz), zweimaliges Auflösen als No-op, und Werte in
`DetailConfigPaths`. Lauf: 169/169 grün.

**Versionsnummern wurden nicht angefasst** — der Bump gehört zum Publish-Vorgang.
