# Plan: LocalActivation — zentrale Auslöser, die ein Mandant für sich aktiviert

Aufsetzend auf `4883f0da` (Auslöser, Vertretung, Anhalten, Kommentare, Anhänge).

**Status: gebaut.** Kern, EF-Schicht, Views und Tests sind grün (94 Tests im EF-Projekt, 335 im Kern).
Migrations-SQL steht in `Migration-Future_10-MLM.md` §37. **Offen: Migration und Lauftest am Host.**
Der Flag heisst final `AllowTenantlessStart` (nicht `AllowBroadcastStart`).

## Wozu

Zentral EINEN Zeitplan-Workflow definieren — das Beispiel ist ein Zahlungslauf, immer Ende Monat — und
jeder Mandant hakt für sich an: „den will ich auch". Der Zeitplan läuft dann **in seinem Mandanten**.

Heute geht das nicht. Ein Auslöser erbt den Mandanten seiner Definition, und öffentliche Definitionen
bekommen bewusst gar keinen (`WorkflowStartTriggerFactory.FromDefinition` steigt bei `IsPublic` sofort
aus). Das war die absichtlich einfachste Variante; „je Mandant" stand von Anfang an als spätere Option
daneben.

## Der Kern des Umbaus

**Der Lauf-Zustand gehört zur Aktivierung, nicht zum Zeitplan.** `NextDueUtc`, `LastRunUtc`,
`LastInstanceId` und der Anspruch (Lease) liegen heute an `WorkflowStartTriggerRow`. Sobald zwei
Mandanten denselben zentralen Zeitplan fahren, hat aber jeder seinen eigenen Stand — die eine Zeile
kann ihn nicht mehr tragen.

- `WorkflowStartTriggers` behält nur noch die **Deklaration** (Definition, Knoten, Art, Muster, Modus),
  weiterhin beim Speichern der Definition neu aufgebaut und nie von Hand gepflegt.
- Neu `WorkflowStartTriggerActivations` trägt **Zustand und Zustimmung** je Mandant.
- Der Aufgriff läuft danach **einheitlich über die Aktivierungen** — auch für mandanteneigene
  Zeitpläne, die beim Speichern automatisch ihre eine Aktivierung bekommen. Kein Sonderweg für
  öffentliche Definitionen; Engine und Runner ändern sich in der Struktur kaum.
- **Abhaken heisst deaktivieren, nicht löschen.** Sonst geht die Historie verloren, und das
  „sofort"-Kennzeichen eines Musters (`t`) würde beim erneuten Anhaken ein zweites Mal greifen.

---

## Die Falle, die den ersten Entwurf gekippt hat

Der naheliegende Schlüssel der Aktivierung wäre `TriggerKey + TenantId`. **Das wäre falsch, und zwar
still.**

Die Auslöser-Zeilen sind abgeleitete Daten: beim Speichern einer Definition werden die alten über den
Index `(TenantId, DefinitionId)` weggeräumt und neu eingefügt. Der `TriggerKey` ist damit bei **jedem
Speichern ein anderer**. Eine Aktivierung, die darauf zeigt, hinge nach der ersten Korrektur an der
zentralen Definition im Leeren — und zwar ohne Fehler: der Join fände nichts, der Zeitplan liefe
einfach nicht mehr. Bei einem Monatslauf merkt das jemand frühestens vier Wochen später.

Die Aktivierung muss deshalb an der **fachlichen** Identität des Auslösers hängen, nicht an seiner
Zeilennummer:

```
(OwnerTenantId, DefinitionId, NodeId, Kind)  +  TenantId   →  eindeutig
```

`OwnerTenantId` ist der Mandant der **Definition** (null = öffentlich) und muss mit hinein: genau
deshalb gibt es `WorkflowDefinition.Key` überhaupt — eine öffentliche und eine mandanteneigene
Definition dürfen denselben `Id` tragen, und ohne diese Spalte aktivierte man versehentlich die
falsche. `TenantId` ist der **aktivierende** Mandant.

Technisch: eigener `ActivationKey` als Primärschlüssel (schmal für Lease und Fortschreibung), plus ein
eindeutiger Index über die fünf Spalten oben.

**Verwaiste Aktivierungen** (der Knoten wurde umbenannt oder entfernt) bleiben stehen und werden nie
aufgegriffen — der Join findet keinen Auslöser. Sie verschwinden nicht still: die Speicher-Operation,
die den Auslöser wegnimmt, protokolliert, welche Aktivierungen sie damit ins Leere schickt, und die
Ansicht zeigt sie als „Auslöser nicht mehr vorhanden". Kommt der Knoten zurück, greift die Aktivierung
wieder.

---

## Die Entscheidungen

### 1. Angeboten wird nur, was ausdrücklich dafür gedacht ist

Zwei Dinge zusammen, weil sie **zwei verschiedene Fragen** beantworten:

- **`AllowLocalActivation`** am Auslöser-Knoten beantwortet *ist das zur Übernahme gedacht?* — die
  Aussage des Modellierers.
- **`RequiredFeature` / `RequiredPermission`** an der Definition beantworten *wer darf?* — die
  Aussage des Betriebs.

Ohne das Kennzeichen taucht ein zentral gepflegter Prozess gar nicht erst in der Auswahl auf, auch
wenn niemand ein Feature gesetzt hat.

### 2. Feature und Permission an der Definition, gesetzt vom Sysadmin

Zwei neue Felder auf `WorkflowDefinition`:

```csharp
public string RequiredFeature { get; set; }      // null = keins verlangt
public string RequiredPermission { get; set; }   // null = keine verlangt
```

**Als Namen, nicht als Fremdschlüssel.** Dieselbe Begründung, aus der `WorkflowDefinition.Key` schon
`[JsonIgnore]` ist: die Definition ist exportierbar, und ein Schlüssel zeigt in der Nachbaranlage auf
etwas anderes. Präzedenzfall im eigenen Haus ist `UserActivityNode.RequiredPermission` — der Kern legt
den Namen ab, der Mantel setzt ihn durch. Die Maske bietet trotzdem eine Auswahl über die vorhandenen
Features und Permissions an, damit sich niemand vertippt.

Setzbar nur mit `ToolkitPermission.Sysadmin`, und nur an öffentlichen Definitionen sinnvoll (an einer
mandanteneigenen erlaubt, aber wirkungslos in Bezug auf Aktivierung — dort greift sie beim Start von
Hand).

Das Gate gilt **überall gleich**: Sichtbarkeit in der Liste, Anhaken, Start von Hand. Anzeige und
Guard benutzen dasselbe Prädikat — die getrennten Fassungen sind genau die, bei denen man den Knopf
sieht und beim Klick abgewiesen wird.

### 3. Wann geprüft wird — und warum das nicht dasselbe ist

Feature und Permission verhalten sich zeitlich **unterschiedlich**, und darauf steht und fällt der
Nutzen:

| | Begriff | prüfbar beim Anhaken | prüfbar beim Feuern |
|---|---|---|---|
| `RequiredPermission` | Benutzer | ja | **nein** — der Runner hat keinen Benutzer |
| `RequiredFeature` | Mandant | ja | **ja — und muss** |

Die Permission gatet also das Anhaken und den Start von Hand. Entzieht man sie jemandem, hebt das eine
früher gesetzte Aktivierung **nicht** auf; das ist vertretbar, muss aber in der Oberfläche stehen.

Das Feature wird bei **jedem Feuern** nachgeprüft. Ohne das liefe der Zahlungslauf beim Mandanten
weiter, dessen Abo vorletzte Woche ausgelaufen ist — und das ist der einzige Fall, in dem die ganze
Konstruktion wirklich weh tut.

Dafür braucht der Kern eine schmale Abstraktion, denn `ITVComponents.Workflow` ist bewusst
toolkit-neutral („Der Kern wertet den Wert nicht aus" steht so an `WorkflowDefinition.TenantId`):

```csharp
/// <summary>Ob ein Feature für einen Mandanten aktiv ist. Ohne Verdrahtung: immer ja.</summary>
public interface IWorkflowTenantFeatureGate
{
    bool IsEnabled(string tenantId, string featureName);
}
```

Standard-Implementierung sagt immer `true` (Verhalten wie heute), die Toolkit-Implementierung fragt
die Feature-Aktivierungen.

**Fehlt das Feature beim Feuern: überspringen, protokollieren, Fälligkeit trotzdem fortschreiben** —
dieselbe Regel wie bei `SkipWhilePreviousRuns`. Die Aktivierung wird *nicht* auf `Enabled=false`
gesetzt: sonst müsste nach jedem kurzen Abo-Aussetzer jemand von Hand wieder anhaken, und niemand
merkt, dass er es müsste.

### 4. Was der Mandant selbst anpassen darf — am Knoten, nicht an der Definition

Zwei Flags an `ScheduleStartTrigger`, nicht an der Definition:

```csharp
public bool AllowLocalActivation { get; set; }   // siehe 1.
public bool AllowReschedule { get; set; }        // eigenes Muster je Aktivierung
public bool AllowOwnVariables { get; set; }      // eigene Startwerte je Aktivierung
```

Der Zeitplan steht am Knoten, und eine Definition darf mehrere Einstiege haben. Ein Flag getrennt von
dem, worüber es redet, altert schlecht: bei „Monatslauf plus Tageslauf" will man typisch genau einen
davon zur Anpassung freigeben.

Die Aktivierung führt entsprechend `PatternOverride` und `VariablesJsonOverride`. Sie werden **nur
beachtet, wenn das Flag am Knoten es erlaubt** — wird die Freigabe später zurückgenommen, bleibt der
abweichende Wert stehen, wird ignoriert, und die nächste Fälligkeit rechnet wieder gegen das zentrale
Muster. Das ist auch der Moment, der protokolliert gehört: der Mandant hat sich den 25. eingestellt und
läuft ab jetzt wieder am Letzten.

### 5. Der Message-Start — die eigentliche offene Flanke

Hier liegt das Risiko, und es ist **nicht neu mit diesem Umbau**: heute kennt eine Nachricht überhaupt
keinen Mandanten. `DeliverSignal` und `BroadcastSignal` nehmen Name, Korrelationsschlüssel und
Nutzdaten — mehr nicht. `FindMessageTriggers(signalName)` sucht mandantenübergreifend, und der Start
läuft anschliessend im Mandanten der jeweiligen Definition. Haben hundert Mandanten je ihre eigene
Definition auf `OrderArrived`, startet **eine** mandantenlose Nachricht heute schon hundert Vorgänge.

Deshalb bekommt die Nachricht einen **Ursprungs-Mandanten**, und der entscheidet:

```csharp
public int DeliverSignal(string signalName, string correlationKey = null,
    IDictionary<string, object> payloadVariables = null, string originTenantId = null);
```

Auflösung, wenn nicht ausdrücklich übergeben:

1. Aus einer Instanz gesendet → der Mandant der sendenden Instanz. Der Weg steht schon: die Outbox
   führt `TenantId` bereits denormalisiert (nur reicht ihn heute niemand an die Zustellung weiter).
2. Sonst `WorkflowExecutionScope.HasTenant ? CurrentTenant : null`.
3. Aus dem Web setzt niemand den Scope — dort füllt die toolkit-seitige Fassade den Wert aus dem
   Mandanten der Anfrage. Der Kern kann das nicht selbst, er kennt die Anfrage nicht.

Die Regel beim Start:

- **Ursprungs-Mandant gesetzt** → es feuern nur die Auslöser bzw. Aktivierungen **dieses** Mandanten.
- **Kein Ursprungs-Mandant** (nicht gesetzt, oder bewusst tenant-frei) → es feuert nur, was
  `AllowBroadcastStart` am `MessageStartTrigger` ausdrücklich erlaubt. Dann darf es die hundert
  Vorgänge geben — punktuell und bewusst gewählt.

Zwei Dinge, die man dabei leicht verwechselt:

- `AllowBroadcastStart` hat **nichts** mit der Methode `BroadcastSignal` zu tun. Ein Rundruf aus der
  Instanz eines Mandanten trägt sehr wohl einen Ursprung und startet nur dort. Nur das *Fehlen* des
  Ursprungs verlangt das Flag. (Wenn dir das zu leicht verwechselbar ist: `AllowTenantlessStart`
  träfe es genauer — reine Namensfrage.)
- Die Zustellung an **wartende** Instanzen bleibt unverändert. Es geht ausschliesslich um das
  *Entstehen* neuer Vorgänge.

**Das ist eine Verhaltensänderung an frisch Committetem.** Host-Code, der heute ohne Mandanten-Kontext
sendet, startet danach nichts mehr, bis entweder der Ursprung mitgegeben oder das Flag gesetzt wird.
Damit das nicht als „auf den Namen hört halt niemand" durchgeht, wird der Fall ausdrücklich
protokolliert: gab es Auslöser für den Namen, die nur an der Mandanten-Regel gescheitert sind, steht
das mit Namen im Log.

Angeboten wird die lokale Aktivierung für `Kind=Message` vorerst **nicht** — die Tabelle trägt sie,
die Auswahl blendet sie aus. Eine eintreffende Nachricht, die in fünfzig Mandanten je einen Vorgang
eröffnet, ist eine deutlich grössere Zusage als ein Zeitplan und will einzeln entschieden werden.

---

## Datenmodell

```csharp
public class WorkflowStartTriggerActivationRow
{
    public int ActivationKey { get; set; }        // PK

    // Die fachliche Identität des Auslösers — NICHT der TriggerKey (siehe oben).
    public string OwnerTenantId { get; set; }     // Mandant der Definition, null = öffentlich
    public string DefinitionId { get; set; }
    public string NodeId { get; set; }
    public int Kind { get; set; }

    public string TenantId { get; set; }          // der aktivierende Mandant
    public bool Enabled { get; set; }

    // Abweichungen, nur wirksam bei entsprechender Freigabe am Knoten
    public string PatternOverride { get; set; }
    public string VariablesJsonOverride { get; set; }

    // Lauf-Zustand — zieht von WorkflowStartTriggerRow hierher um
    public DateTime? NextDueUtc { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public string LastInstanceId { get; set; }
    public string ClaimedBy { get; set; }
    public DateTime? ClaimedUntil { get; set; }

    // Nachvollziehbarkeit: ein aktivierter Zahlungslauf tut etwas.
    public string ActivatedBy { get; set; }
    public DateTime ActivatedUtc { get; set; }
}
```

Indizes:

- eindeutig über `(OwnerTenantId, DefinitionId, NodeId, Kind, TenantId)`
- `(Enabled, NextDueUtc)` — der Aufgriff des Runners. Message-Aktivierungen haben `NextDueUtc = null`
  und fallen dadurch von selbst heraus.

**Ohne Mandanten-Filter** konfiguriert, wie `BranchLocks` und `Outbox`: der Runner greift
mandantenübergreifend auf und muss die Zeilen aller Mandanten sehen.

Die Spalten `NextDueUtc`, `LastRunUtc`, `LastInstanceId` und der Anspruch entfallen an
`WorkflowStartTriggerRow`; der Index `(Kind, NextDueUtc)` dort ebenfalls.

## Berührte Stellen

| Datei | was |
|---|---|
| `ITVComponents.Workflow/Model/WorkflowDefinition.cs` | `RequiredFeature`, `RequiredPermission` |
| `ITVComponents.Workflow/Model/Nodes.cs` | `ScheduleStartTrigger`: `AllowLocalActivation`, `AllowReschedule`, `AllowOwnVariables`; `MessageStartTrigger`: `AllowLocalActivation`, `AllowBroadcastStart` |
| `ITVComponents.Workflow/Stores/WorkflowStartTrigger.cs` | `FromDefinition` steigt bei `IsPublic` nicht mehr aus; Lauf-Zustand fällt aus dem Typ; neuer Typ für die Aktivierung |
| `ITVComponents.Workflow/Stores/IWorkflowStore.cs` | `FindMessageTriggers(name, originTenantId)`; Aufgriff/Fortschreibung auf `ActivationKey`; Aktivierungen lesen/setzen |
| `ITVComponents.Workflow/WorkflowEngine.cs` | `RunScheduledStart` (Feature-Gate, Muster/Werte der Aktivierung, Mandant der Aktivierung), `StartFromMessageTriggers` (Ursprungs-Mandant), `DeliverSignal`/`BroadcastSignal`/Outbox-Abarbeitung reichen den Ursprung durch |
| `ITVComponents.Workflow/Abstractions/IWorkflowTenantFeatureGate.cs` | neu, plus Standard-Implementierung „immer ja" |
| `ITVComponents.Workflow.EntityFramework/WorkflowContext.cs` | neue Zeile, Indizes, alte Spalten weg |
| `ITVComponents.Workflow.EntityFramework/EfWorkflowStore.cs` | Aufgriff über Join Aktivierung↔Auslöser; Auto-Aktivierung beim Speichern mandanteneigener Definitionen; Protokoll verwaister Aktivierungen |
| `ITVComponents.Workflow/Stores/InMemoryWorkflowStore.cs` | mitziehen |
| `…WorkflowViews/Design/…` | Feature/Permission in den Definitions-Einstellungen (nur Sysadmin), die drei Flags im `StartConfigEditor` |
| `…WorkflowViews/` | neue Ansicht „Zentrale Abläufe" — Liste mit Häkchen, Muster und Startwerten, wo freigegeben |
| Toolkit-Seite | `IWorkflowTenantFeatureGate` über die Feature-Aktivierungen; Ursprungs-Mandant in der Web-Fassade |

## Migration

Schema-Änderung als **Model-Attribut plus handgeschriebenes SQL** im Migrationsleitfaden — keine
generierte Migration committen (die Snapshots driften und ziehen fremde Änderungen mit).

Reihenfolge:

1. Neue Tabelle anlegen.
2. Für jede bestehende `WorkflowStartTriggerRow` **eine** Aktivierung erzeugen: `OwnerTenantId` und
   `TenantId` beide auf die `TenantId` der Zeile, `Enabled = 1`, Lauf-Zustand 1:1 übernehmen. Damit
   läuft alles Bestehende ohne Unterbrechung weiter — insbesondere bleibt `LastRunUtc` erhalten, sonst
   griffe das „sofort"-Kennzeichen bei jedem laufenden Zeitplan noch einmal.
3. Erst danach die alten Spalten an `WorkflowStartTriggerRow` fallen lassen.

## Zwei Annahmen, die ich getroffen habe

1. **Der Ursprungs-Mandant muss exakt passen** — keine Hierarchie. Eine Nachricht aus dem
   Eltern-Mandanten startet nichts in den Kindern. Alles andere wäre eine erheblich grössere Zusage
   und gehört einzeln entschieden.
2. **Die neue Ansicht selbst hängt an `Workflow.Operate`** (sie ist eine Betriebshandlung); das
   Häkchen je Zeile zusätzlich an `RequiredFeature`/`RequiredPermission` der Definition. Eine eigene
   neue Permission gibt es nicht — so war „dito" gemeint.
