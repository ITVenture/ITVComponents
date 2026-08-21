# Prüfstand für die hierarchische Mandanten-Sicherheit

Zwei Dinge werden hier geprüft, auf **beiden** Providern und gegen den **echten** Code:

1. **Gleichheit** — liefern SQL Server und PostgreSQL Zeile für Zeile dasselbe?
2. **Laufzeit** — was kostet der Baum bei 10 000 Mandanten und Tiefe 100?

Der Prüfstand ist bewusst kein Testprojekt: SQLite oder In-Memory beweisen hier nichts, weil die
Provider-Unterschiede genau der Gegenstand sind. Es braucht eine echte PostgreSQL-Instanz und einen
echten SQL Server.

## Was ihn von einer Abschrift unterscheidet

`pgcheck` und `tsqlcheck` sind winzige Konsolenprogramme, die die Datenbank-Anweisungen aus
`ConfigureViews` des jeweiligen Providers **selbst herausziehen** und als `objects.sql` schreiben.
Damit wird gegen den ausgelieferten Code geprüft und nicht gegen eine Kopie davon, die irgendwann
auseinanderläuft.

```
dotnet run --project pgcheck\pgcheck.csproj   -- pgcheck\objects.sql
dotnet run --project tsqlcheck\tsqlcheck.csproj -- tsqlcheck\objects.sql
```

`objects.sql` steht bewusst nicht im Repo — sie entsteht bei jedem Lauf neu.

Falls der PostBuild eines referenzierten Projekts scheitert (MSB3073), das Kommando mit
`-p:SolutionDir="…\ITVComponents-Public\"` wiederholen.

## Umgebung

**PostgreSQL** — ein Wegwerf-Container reicht:

```
docker run -d --name itv-pgcheck -p 55432:5432 -e POSTGRES_PASSWORD=pgcheck postgres:17
docker exec -i itv-pgcheck psql -U postgres -c "CREATE DATABASE treecheck"
docker exec -i itv-pgcheck psql -U postgres -c "CREATE DATABASE loadcheck"
docker exec -i itv-pgcheck psql -U postgres -c "CREATE DATABASE depthcheck"
```

Skripte laufen über `Get-Content datei.sql | docker exec -i itv-pgcheck psql -U postgres -d <db>`.

**SQL Server** — LocalDB genügt (`(localdb)\MSSQLLocalDB`), aufgerufen mit
`sqlcmd -S "(localdb)\MSSQLLocalDB" -d <db> -b -i datei.sql`.

## Teil 1 — Gleichheit (Datenbank `treecheck`)

Reihenfolge je Seite: `fixture.sql` → `fixture2.sql` → `objects.sql` → `dump.sql`.

`dump.sql` schreibt jede Zeile als `KUERZEL|feld|feld|…`, Nullwerte als `-` und `bit`/`boolean` als
`1`/`0` — beide Seiten in **demselben** Textformat, damit ein reiner Textvergleich genügt. Ergebnis
sind 87 vergleichbare Zeilen; sie müssen Zeichen für Zeichen übereinstimmen.

Die fünf `CT…`-Abfragen (Kind-Mandanten mit Berechtigung) fehlen im T-SQL-Abzug und stehen dort
stattdessen in `procs.sql`: **`GetChildTenantsWithPermsProc` lässt sich auf SQL Server nicht per
`INSERT … EXEC` abgreifen**, weil die Prozedur intern selbst eins macht und T-SQL die Schachtelung
verbietet. Das trifft nur den Prüfstand — EF ruft über `FromSql`. Auf PostgreSQL ist es eine Funktion
und damit frei zusammensetzbar.

**Warum die Hierarchie so aussieht, wie sie aussieht.** Die erste Fassung (`T1 → T2 → T3`, ein
Benutzer an der Wurzel) prüfte die Verrohrung, nicht die Semantik — die Rekursion in
`GetEffectiveTenantUserRoles` lief dabei kein einziges Mal. `fixture2.sql` ergänzt genau die Fälle,
an denen eine Übersetzung auseinanderlaufen kann:

| Fall | Warum er zählt |
|---|---|
| Rolle **innerhalb** desselben Mandanten weitergegeben | Erst dadurch läuft die Rekursion überhaupt |
| Berechtigung nur über diese Weitergabe erreichbar | Prüft die „diskrete Weitergabe" |
| Zwei Wege zum selben Blatt auf **derselben** Ebene | Dort steht `RANK`, nicht `ROW_NUMBER` — mit `ROW_NUMBER` verschwände eine Zeile |
| Benutzer in der **Mitte** statt an der Wurzel | Der Baum muss von dort rechnen |
| Verzweigung statt Kette | Nachbarzweige dürfen sich nicht gegenseitig einsammeln |

Dazu auf der PostgreSQL-Seite `checks.sql` (32 inhaltliche Prüfungen, Erwartungswerte von Hand aus
der T-SQL-Vorlage hergeleitet) und `columns.sql` (48 Spaltennamen — **die richten sich nach dem
Modell, nicht nach der T-SQL-Vorlage**, und auf PostgreSQL ist das der Unterschied zwischen „EF
findet die Spalte" und „EF findet sie nicht").

## Teil 2 — Grenze und Zyklen (Datenbank `depthcheck`)

`fixture.sql` → `objects.sql` → `depth.sql` → `cycle.sql`.

`depth.sql` baut Ketten wachsender Länge und meldet, wo jede Seite tatsächlich abbricht — gemessen,
nicht hergeleitet. Erwartet auf **beiden** Datenbanken: 101 trägt, 102 bricht ab. **Gezählt werden
Rekursionsschritte, nicht Ebenen** — der Anker liefert Ebene 1, ohne einen Schritt gebraucht zu
haben. Wer die Zahl direkt gegen die Ebene prüft, bricht eine Ebene zu früh ab; genau so lag die
erste PostgreSQL-Fassung daneben, und der Gleichheitstest zeigte es nicht, weil seine Hierarchie
drei Ebenen tief ist. **Ein Test beweist nur, was er anfasst.**

`cycle.sql` legt einen Mandanten-Ring an. Erwartet: `SQLSTATE 54001` auf allen drei Wegen — über die
Sicht, über die Funktion und über den gefilterten Zugriff. Bewusst ein Fehler und **kein** stilles
Teilergebnis: bei einer Rechte-Abfrage wäre still die schlechtere Sorte Fehler.

## Teil 3 — Last (Datenbank `loadcheck`)

`load_schema.sql` → `objects.sql` → `load_data.sql` → `load_measure.sql`.

10 000 Mandanten, zweiteilig: **1..100 eine Kette** (Tiefe 1 bis 100, also exakt an der
Rekursionsgrenze, nicht in ihrer Nähe) und **101..10000 ein breiter Baum**, dessen Eltern reihum die
Kettenglieder 1..99 sind — damit gibt es Geschwister auf jeder Ebene und die tiefste Ebene ist 100.
`UpwardsTenantTree` hat dadurch 509 950 Zeilen. Dazu 100 Rollen mit 100 Stufen Vererbung, alice an
der Wurzel, bob in T50, und je 5 000 Rausch-Zeilen bei Plugins, Diagnose-Abfragen und Kacheln.

**Mit den echten Indizes und mit frischen Statistiken** (`ANALYZE` / `sp_updatestats`) — ohne beides
wären die Zeiten wertlos.

Zehn Messpunkte M0–M9, jeder mit seiner Zeilenzahl. **Die Zeilenzahlen gehören zum Ergebnis:** nur
wenn sie auf beiden Seiten übereinstimmen, wurde dieselbe Arbeit gemessen.

Vorbehalte, die zu jeder Auswertung gehören: je ein Lauf, und die beiden Datenbanken stehen in
verschiedenen Umgebungen (Container gegen LocalDB). Ein Faktor 2 ist damit Rauschen — ein Faktor 300
nicht.

## Was der Prüfstand bisher gefunden hat

- **Der Zyklen-Wächter brach eine Ebene zu früh ab** (Verwechslung von Ebenen und Schritten). Der
  Gleichheitstest zeigte es nicht — seine Hierarchie ist drei Ebenen tief.
- **PostgreSQL schiebt einen Filter nicht in eine rekursive Sicht.** `UpwardsTenantTree` wurde bei
  jedem Zugriff komplett gebaut und danach gefiltert: 270 ms für den Weg, den jede Anfrage in einem
  Kind-Mandanten geht. Behoben, indem die Rekursion in eine Funktion mit dem Blatt als Parameter
  wanderte und die Sicht ein flacher `LATERAL`-Aufruf darauf wurde — 270 ms → 1 ms.

Auswertung und Zahlen: `docs/Audit-PostgreSQL-Luecken.md`, Abschnitt „Phase 4".
