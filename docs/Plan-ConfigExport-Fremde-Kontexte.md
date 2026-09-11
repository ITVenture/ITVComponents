# System-Config-Austausch aus einem fremden DbContext

**Stand:** gebaut, vom Anwender noch nicht getestet (2026-09-11, Zweig Future_10).

**Anlass:** Bestellung aus MLM (`MLMManager/docs/Designstudie-ConfigExport-Topic-Kontexte.md`).
`IConfigExtension` konnte eine Sektion **beschreiben**, aber nicht **einspielen**, wenn ihre Entitaeten
nicht im Modell des Host-Kontexts liegen. Das trifft jedes Fachmodul mit eigenem Topic-Context - die
mitgelieferten Sektionen (`billing`, `help`) merkten es nie, weil ihre Tabellen im Host-Modell sitzen.

---

## Was das Problem war

`SysConfigurationHandler.ApplyChanges` schob **alle** Changes durch `DbContext.ApplyData(...)`:
`change.EntityName` wurde als Ausdruck gegen den Host-Kontext ausgewertet, das Set ueber dessen
`db.Set` geholt, gespeichert ueber dessen `SaveChanges`. Eine Entitaet aus einem anderen Modell ist
dort nicht aufloesbar - der Applyer faengt das je Change ab und ueberspringt ihn. Das Ergebnis waere
ein Import gewesen, der durchlaeuft und nichts tut.

Dazu die Fremdschluessel: `MakeLinqAssign<TContext>` backt `typeof(TContext).Name` als
Variablendeklaration **in den Skripttext** (`MlmCloudDbContext db = Global.Db;`). Fuer eine fremde
Sektion muss dort der fremde Typ stehen.

## Was gebaut wurde

### 1. `Change.SectionKey`

Nullable, gestempelt **zentral** in `CompareExtensions` auf jeden Change, den eine Extension liefert.
Keine Extension aendert dafuer eine Zeile. `null` = Host-Sektion, also genau das bisherige Verhalten.

Eine Eigenschaft am Change und keine Gruppierung daneben, weil der MVC-Weg
(`AssemblyDiagnosticsController.ApplyChanges`) die Changes als JSON zum Client und zurueck reicht -
eine Zuordnung, die nur im Speicher lebt, ueberlebt das nicht.

### 2. `IConfigExtensionContext` (optional, neben `IConfigExtension`)

```csharp
public interface IConfigExtensionContext
{
    /// Erzeugt den Kontext fuer EINEN Vorgang. Der Aufrufer schliesst ihn.
    DbContext CreateContext(IServiceProvider services);
}
```

Am **Handler** und nicht an der Registrierung: der Handler wird ohnehin aus einem Bereich gebaut, und
ein Topic-Context haengt nicht immer an einer DI-Registrierung, die sich ueber den Typ aufloesen
liesse (bei MLM am Fresh-Plugin-Weg). Eine Typangabe in der Registrierung koennte das nicht.

`Create…` und nicht `Resolve…`, weil der Name die Eigentumsfrage beantworten muss: erwartet wird eine
**frische Instanz je Vorgang**, das Toolkit schliesst sie in einem `finally`. Unter Blazor Server lebt
ein Bereichs-Kontext so lange wie der Circuit des Benutzers - ein Import wuerde sonst in einen Kontext
schreiben, den die Oberflaeche nebenher weiterbenutzt.

### 3. Routing beim Einspielen

`ApplyChanges` teilt die Changes auf:

| Change | laeuft in |
|---|---|
| ohne `SectionKey` | Host-Kontext (wie bisher, inkl. `FullSecurityAccessHelper`) |
| Sektion **ohne** eigenen Kontext | Host-Kontext |
| Sektion **mit** eigenem Kontext | deren Kontext, eigener Bereich, eigenes `SaveChanges` |
| unbekannte Sektion | uebersprungen, mit Meldung und Log-Eintrag |

Der Host laeuft zuerst; die uebrigen Sektionen danach in stabiler Reihenfolge. Ob eine Sektion einen
eigenen Kontext hat, ist am **Handler-Typ** ablesbar (`IsAssignableFrom`) - dafuer muss keiner gebaut
werden.

### 4. Beschreiben und Vergleichen bekommen denselben Kontext

Nennt eine Extension einen eigenen Kontext, bekommt sie ihn auch in `Describe` und `Compare` gereicht
statt des Host-Kontexts. Sonst bliebe die Asymmetrie „beschreiben muss sich selbst behelfen,
einspielen wird bedient". Ausserdem laeuft jede Sektion jetzt in ihrem **eigenen Bereich** - eine
scheiternde nimmt nichts mit in die naechste.

### 5. Die Change-Helfer werden an den Ziel-Kontext gebunden

`ConfigurationHandlerBase` hat neben `MakeLinqAssign<TContext>` / `MakeLinqQuery<TContext>` jetzt
Fassungen mit `Type`-Parameter; die generischen delegieren dorthin. `CreateChangeContext(Type)` gibt
einer Extension die Helfer gebunden an **ihren** Kontext-Typ. Das Interface `IConfigChangeContext`
bleibt unveraendert.

### 6. Der schreibende Weg bekommt eine eigene Lade-Scope

Aufgefallen beim Nachfragen, nicht bestellt - aber dieselbe Baustelle:

Der Config-Handler ist kein Dienst, sondern ein **Plugin**, und er nimmt seinen Kontext im Konstruktor
(`SysConfigurationHandler(TContext db, …)`) und haelt ihn, solange die Plugin-Instanz lebt. Die haengt
in der von `WebPluginHelper` gepufferten Factory - und `WebPluginHelper` ist `AddScoped`, unter Blazor
also **circuit-lang**.

Der Mechanismus dagegen gibt es laengst (`CreateOperationScope` + `AddDependency(…,
disposeWithScope: true)`, bzw. `IFreshInjectablePlugin<T>.Lease()`), er war nur ungleich angewandt:

| Weg | Aufloesung | Kontext |
|---|---|---|
| Hochladen/Vergleichen (`sysCfg`) | `DefaultFileServiceHandler` → `CreateOperationScope()` | frisch je Vorgang |
| Einspielen (Blazor) | `IInjectablePlugin<IConfigurationHandler>` - ambient | der des Circuits |
| Einspielen (MVC) | ambient | der des Requests - dort in Ordnung |

Der Vergleich lief also schon frisch; ausgerechnet der **schreibende** Weg nicht.
`AssemblyDiagnosticsAdminHandler.ApplyConfigChanges` nimmt jetzt
`IFreshInjectablePlugin<IConfigurationHandler>.Lease()`. Zwei Dinge dazu:

- **Der Wirt entscheidet mit.** Eine frische Lade-Scope liefert nur dann einen frischen DbContext, wenn
  die Abhaengigkeit als scope-besessen verdrahtet ist (`disposeWithScope: true`, MLM-Leitfaden §8.1).
  Ist sie es nicht, reicht die Scope die ambiente Instanz durch - dann ist es so gut wie vorher, nicht
  schlechter.
- **Kein zweiter Versuch nach dem Einspielen.** Das Kennzeichen `applied` wird gesetzt, sobald die Lease
  steht - scheitert danach das Schliessen der Scope, wird das gemeldet, aber nicht in den
  Rueckfall-Weg gelaufen: der spielte die Changes ein zweites Mal ein.

Ein direkt registrierter Handler (ohne Plugin-System) geht weiter den bisherigen Weg; fuer den gibt es
keine Lade-Scope.

---

## Der Punkt, an dem es weniger Arbeit war als gedacht

Die naheliegende vierte Baustelle - „die Skript-Umgebung kennt den fremden Typ nicht" - gibt es nicht.
`ConfigurationHandlerBase.RunInit` setzt `NativeScriptHelper.SetAutoReferences("SysQry…", true)`, und
der Ausfuehrungsweg fuer `` `E(Db as Db->SysQry…) `` ist die Roslyn-Ueberladung **mit Ziel-Objekt**:
die ruft `ApplyAutoRef(cfg, new[]{ target }, true)` - Assembly **und Namensraum** des tatsaechlich
uebergebenen Kontexts landen von selbst in der Skript-Konfiguration. Wer den richtigen Kontext als
`Db` hereinreicht, bekommt den Typnamen geschenkt. Kein `AddUsing`, keine Registrierung, keine
Bestellung an dieser Stelle.

## Vier Entscheidungen, die man kennen sollte

**Keine uebergreifende Transaktion.** Je Kontext ein `SaveChanges`. Zwei Kontexte koennen zwei
Datenbanken sein; eine gemeinsame Transaktion waere eine Annahme, die nur dann stimmt, wenn sie es
zufaellig tut. Der Applyer arbeitet ohnehin best-effort - die Meldungen sagen jetzt je Sektion, was
durchging (`--- Section 'xyz' ---`).

**Kein Ersatz-Kontext.** Liefert eine Extension keinen Kontext (null oder Ausnahme), wird ihre Sektion
**ausgelassen** - nicht ersatzweise im Host-Kontext bearbeitet. Dort waeren ihre Entitaeten nicht
aufloesbar, und ein stiller Fehlschlag je Datensatz ist das schlechtere Ergebnis als eine ausgelassene
Sektion mit Eintrag im Log und einer Warnung im Diff (`RegisterBrokenSection`).

**Fremdschluessel ueber die Kontextgrenze gibt es nicht.** `MakeLinqAssign` loest innerhalb des
Kontexts der Sektion auf. Wer aus einer fremden Sektion auf eine Host-Entitaet zeigen will (Mandant,
Benutzer), kann das nicht - und sollte es auch nicht: das waere Betriebsdatum in einer
Konfigurationsdatei.

**Filter sind Sache der Umsetzung.** Den Sicherheits-Context macht das Toolkit fuer die Dauer des
Einspielens mandanten-blind (`FullSecurityAccessHelper`); einen fremden Kontext kann es nicht kennen.
Wer dort globale Filter fuehrt, schaltet sie in `CreateContext` aus - sonst sieht der Vergleich
weniger Zeilen, als da sind, und der Import legt an, was er nur nicht sehen konnte.

## Fuer den Konsumenten

```csharp
public class CustomerCareConfigExtension : IConfigExtension, IConfigExtensionContext
{
    public string SectionKey => "customercare";

    public DbContext CreateContext(IServiceProvider services)
        => /* frische Instanz, z.B. ueber IDbContextFactory<T> oder den Fresh-Plugin-Weg */;

    public ConfigExtensionMarkup Describe(DbContext db) { /* db IST der eigene Kontext */ }

    public IEnumerable<Change> Compare(DbContext db, ConfigExtensionMarkup current,
        ConfigExtensionMarkup uploaded, IConfigChangeContext changes) { /* dito */ }
}
```

Registriert wird wie bisher ueber `AddSystemConfigExtension<TMarkup>()`; am Attribut
`[SystemConfigHandler]` aendert sich nichts.

## Was offen bleibt

- **Vom Anwender getestet** ist nichts davon; auf Toolkit-Seite gibt es dafuer keinen Konsumenten -
  `billing` und `help` gehen weiterhin den Host-Weg, und genau das ist die Ruecksicht, die die
  Aenderung nehmen musste. Der erste echte Lauf ist MLMs `customercare`-Sektion.
- **Die Wirts-Verdrahtung.** Damit der schreibende Weg wirklich einen frischen Kontext bekommt, muss
  die Kontext-Abhaengigkeit des Handler-Plugins als scope-besessen registriert sein (§8.1). Ohne das
  bleibt es beim geteilten Kontext - ohne Fehler, aber auch ohne Gewinn.
- **Der MVC-Weg** bleibt ambient und damit request-gebunden. Das ist dort richtig und wurde bewusst
  nicht angefasst.
