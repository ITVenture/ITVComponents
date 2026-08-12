# Diagramme mit CScript (`chart.cscript`)

Die Konfiguration ist ein Objektliteral über demselben Modell, das auch ein Scriban-Template sieht:
`Rows`, `Row`, `Count`, `Params`, `Title`. Dazu `column(…)`, `translate(…)` und der Typ `ChartType`.

```csharp
{
  type: ChartType.Pie,
  labels: column(Rows, "Topic"),
  series: [ { name: "Anzahl", data: column(Rows, "Anzahl") } ]
}
```

## Vier Eigenheiten, die man einmal wissen muss

**1. Text steht in doppelten Anführungszeichen.** Einfache bezeichnen in CScript einen **Typ**
(`'System.TimeSpan'`) — aus `'pie'` würde der Versuch, einen Typ namens *pie* zu laden.

**2. Es gibt keine Lambda-Ausdrücke.** `Rows.Select(r => r.Topic)` ist ein Syntaxfehler, und ein
Funktions-Literal nehmen die LINQ-Methoden nicht an. Dafür gibt es `column(Rows, "Spalte")`. Einzelne Werte
liest man direkt:

```csharp
Rows[0].Topic        // Eigenschaft
Rows[0]["Topic"]     // Spaltenname
Rows.Length          // Anzahl Zeilen
```

**3. Ausdruck oder Anweisungsblock** wird in den Renderer-Einstellungen **gewählt**:

- *Expression* (Vorgabe) — der Text ist das Objektliteral, wie oben.
- *Script block* — mehrere Anweisungen, die mit `return` enden:

  ```csharp
  farben = ["#2979ff", "#00acc1"];
  return { type: ChartType.Pie, labels: column(Rows, "Topic"),
           series: [ { name: "Anzahl", data: column(Rows, "Anzahl") } ],
           chartOptions: { chartPalette: farben } };
  ```

  Fehlt das `return`, ist das Ergebnis leer — und die Kachel sagt genau das.

**4. Variablen ohne `var`**: `x = 1;`, nicht `var x = 1;`.

## Wenn LINQ nötig ist

Über die native Einbettung — dort läuft echtes C#, und sie darf als Wert **mitten in der Deklaration**
stehen:

```csharp
{
  type: ChartType.Pie,
  labels: `E(Rows as Rows->DEFAULT)::@"Dictionary<string,object>[] rw =
             ((object[])Global.Rows).Cast<Dictionary<string,object>>().ToArray();
           return (from t in rw where (int)t[""Anzahl""] > 0 select (string)t[""Topic""]).ToArray();" with {},
  series: [ { name: "Anzahl", data: column(Rows, "Anzahl") } ]
}
```

Dazu vier Punkte:

- **`with { … }` ist Pflicht**, auch leer.
- **`Global.Rows` ist ein `object[]`** — darauf kann man sich verlassen. Die **Elemente** hängen an der
  Datenquelle: über eine SQL-Verbindung sind es `Dictionary<string, object>`, über einen DbContext das, was
  die Abfrage zurückgibt. Der Cast im C#-Code muss dazu passen; `column(…)` nimmt beides.
- Zwei Formen: **mit Zielobjekt** — `` `E(ziel as name->cfg)::"code" `` , Code als String-Literal (`@"…"`
  mit `""` für innere Anführungszeichen) — und **ohne**: `` `E(#cfg)::@#code# ``, Code als Block zwischen
  `@#` und `#`. Die Formen lassen sich nicht mischen.
- `System`, `System.Linq` und `System.Collections.Generic` stehen immer zur Verfügung. Weitere Namensräume
  meldet man im **Block-Modus** an: `` `U(MeineCfg)"using System.Text;"; `` — der Name der Konfiguration ist
  dabei **gross-/kleinschreibungsempfindlich** und muss bei `` `E(…->MeineCfg) `` genau so wiederkehren.

## Kommentare

`//` ist erlaubt — auch für den Block, den der Knopf **„Insert parameters"** unter die Konfiguration hängt.
