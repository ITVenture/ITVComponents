using System;
using System.Collections.Generic;
using ITVComponents.Workflow.Model;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.ViewModels
{
    /// <summary>
    /// Eine Definition, die von Hand gestartet werden kann - ein Eintrag der Auswahl im Start-Dialog.
    /// Gelistet wird immer nur die HOECHSTE Version je Id: eine aeltere Version von Hand zu starten waere
    /// keine Bedienhandlung, sondern ein Eingriff (und die Engine startet ohnehin die hoechste).
    /// </summary>
    public sealed class WorkflowStartableDefinition
    {
        /// <summary>Die fachliche Id der Definition.</summary>
        public string Id { get; init; } = "";

        /// <summary>Die Version, die beim Start zum Zug kaeme.</summary>
        public int Version { get; init; }

        /// <summary>Der Anzeigename aus der Definition, oder null.</summary>
        public string? Name { get; init; }
    }

    /// <summary>
    /// Die Start-Maske einer Definition: die Felder, die der Benutzer ausfuellt, plus der Kontext, den er
    /// dafuer braucht. Aufgeloest aus dem Start-Knoten der hoechsten Version.
    /// </summary>
    public sealed class WorkflowStartForm
    {
        /// <summary>Die Definition, zu der diese Maske gehoert.</summary>
        public string DefinitionId { get; init; } = "";

        /// <summary>Die Version, die gestartet wird.</summary>
        public int Version { get; init; }

        /// <summary>Der Anzeigename der Definition, oder null.</summary>
        public string? Name { get; init; }

        /// <summary>
        /// Die (noch unuebersetzte) Anleitung aus <see cref="StartNode.FormDescription"/> - Klartext oder
        /// Kultur-JSON. Die Uebersetzung passiert erst beim Anzeigen, damit die Kultur des Servers nicht
        /// die des Lesers bestimmt.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Die Feld-Deklaration aus <see cref="StartNode.FormFields"/>. Leer = die Definition wird ohne
        /// Eingaben gestartet (der Dialog zeigt dann nur den Korrelationsschluessel).
        /// </summary>
        public IReadOnlyList<UserTaskField> Fields { get; init; } = Array.Empty<UserTaskField>();

        /// <summary>
        /// Hat die Definition eine deklarierte Signatur (<see cref="StartNode.Inputs"/>)? Nur fuer den
        /// Hinweis im Dialog.
        /// </summary>
        public bool HasSignature { get; init; }

        /// <summary>
        /// Ist die Signatur strikt (<see cref="ActivityScopeMode.Replace"/>)? Dann ueberleben nur die in
        /// <see cref="StartNode.Inputs"/> deklarierten Parameter den Start - ein Feld, das dort fehlt, geht
        /// verloren. Der Dialog warnt deshalb.
        /// </summary>
        public bool StrictSignature { get; init; }
    }

    /// <summary>Was der Start-Dialog abschickt.</summary>
    public sealed class WorkflowStartRequest
    {
        /// <summary>Die zu startende Definition (hoechste Version).</summary>
        public string DefinitionId { get; init; } = "";

        /// <summary>Optionaler Korrelationsschluessel fuer spaetere Signale.</summary>
        public string? CorrelationKey { get; init; }

        /// <summary>
        /// Die Dringlichkeit der neuen Instanz (kleinere Zahl = wichtiger, siehe
        /// <c>WorkflowPriority</c>), oder null fuer die Vorgabe der Definition
        /// (<see cref="WorkflowDefinition.DefaultPriority"/>).
        /// </summary>
        public int? Priority { get; init; }

        /// <summary>
        /// Die uebergebenen Startvariablen (Feldname -> Wert). Sie sind der Stack, gegen den die Signatur
        /// der Definition (<see cref="StartNode.Inputs"/>) aufgeloest wird.
        /// </summary>
        public Dictionary<string, object> Variables { get; init; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Das Ergebnis eines Startversuchs. Bewusst ein Ergebnis-Objekt statt eines bool: der haeufigste
    /// Misserfolg (Definition fuer den Start gesperrt, Start-Parameter nicht aufloesbar) hat einen Grund,
    /// den der Benutzer lesen koennen muss - sonst bleibt nur "hat nicht geklappt".
    /// </summary>
    public sealed class WorkflowStartResult
    {
        /// <summary>Wurde eine Instanz angelegt?</summary>
        public bool Started { get; init; }

        /// <summary>Die Id der neuen Instanz, wenn <see cref="Started"/>.</summary>
        public string? InstanceId { get; init; }

        /// <summary>Der Grund des Scheiterns (anzeigbar), sonst null.</summary>
        public string? Error { get; init; }

        /// <summary>Erzeugt ein Erfolgs-Ergebnis.</summary>
        public static WorkflowStartResult Ok(string instanceId)
            => new WorkflowStartResult { Started = true, InstanceId = instanceId };

        /// <summary>Erzeugt ein Fehler-Ergebnis mit anzeigbarem Grund.</summary>
        public static WorkflowStartResult Failed(string error)
            => new WorkflowStartResult { Started = false, Error = error };
    }
}
