using System;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.ViewModels
{
    /// <summary>
    /// Ein <b>zentraler Ablauf</b>, wie ihn die Uebersicht zeigt: was er ist, ob dieser Mandant ihn
    /// uebernommen hat und wie weit sein Lauf ist.
    /// </summary>
    /// <remarks>
    /// Traegt bewusst beides zusammen - die Deklaration (die allen gehoert) und den Stand (der nur
    /// diesem Mandanten gehoert). Erst zusammen ergibt es eine Zeile, die jemand lesen kann.
    /// </remarks>
    public class CentralWorkflowItem
    {
        /// <summary>Die fachliche Id der Definition - Teil der Kennung dieser Zeile.</summary>
        public string DefinitionId { get; set; } = string.Empty;

        /// <summary>Der Start-Knoten - Teil der Kennung dieser Zeile.</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>Der Anzeigename der Definition.</summary>
        public string? Name { get; set; }

        /// <summary>Das <b>wirksame</b> Muster: das eigene, wo erlaubt und gesetzt, sonst das zentrale.</summary>
        public string? Pattern { get; set; }

        /// <summary>Das zentrale Muster - fuer die Anzeige „statt &lt;zentral&gt;".</summary>
        public string? CentralPattern { get; set; }

        /// <summary>Ob dieser Mandant den Ablauf uebernommen hat.</summary>
        public bool Enabled { get; set; }

        /// <summary>Ob er ein eigenes Muster setzen darf.</summary>
        public bool MayReschedule { get; set; }

        /// <summary>Sein eigenes Muster, oder null.</summary>
        public string? PatternOverride { get; set; }

        /// <summary>Die naechste Faelligkeit (UTC), oder null.</summary>
        public DateTime? NextDueUtc { get; set; }

        /// <summary>Wann zuletzt gelaufen (UTC), oder null - noch nie.</summary>
        public DateTime? LastRunUtc { get; set; }

        /// <summary>Die zuletzt gestartete Instanz, oder null.</summary>
        public string? LastInstanceId { get; set; }

        /// <summary>
        /// Ob der Ausloeser dazu weggefallen ist (Knoten umbenannt oder entfernt). Dann laeuft die
        /// Uebernahme ins Leere und wird als solche gezeigt - stillschweigend verschwinden darf sie nicht.
        /// </summary>
        public bool Orphaned { get; set; }
    }

    /// <summary>Das Anhaken bzw. Abhaken eines zentralen Ablaufs.</summary>
    public class CentralWorkflowActivationRequest
    {
        /// <summary>Die fachliche Id der Definition.</summary>
        public string DefinitionId { get; set; } = string.Empty;

        /// <summary>Der Start-Knoten.</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>Ob der Ablauf ab jetzt fuer diesen Mandanten laufen soll.</summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Ein eigenes Muster, oder null fuer das zentrale. Wird nur beachtet, wenn der Start-Knoten es
        /// erlaubt - sonst stillschweigend verworfen und protokolliert.
        /// </summary>
        public string? PatternOverride { get; set; }
    }
}
