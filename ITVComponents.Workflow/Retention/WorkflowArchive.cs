using System;
using System.Collections.Generic;
using ITVComponents.Workflow.Instances;

namespace ITVComponents.Workflow.Retention
{
    /// <summary>
    /// Eine Gruppe beendeter Vorgaenge, wie der Aufbewahrungslauf sie in die Hand nimmt: alle
    /// <b>obersten</b> Instanzen einer Definition bei einem Mandanten.
    /// </summary>
    /// <remarks>
    /// Die Frist ergibt sich erst aus Definition <i>und</i> Mandant - deshalb kann der Lauf nicht
    /// einfach „alles vor einem Stichtag" abfragen: der Stichtag ist je Gruppe ein anderer. Er fragt
    /// also zuerst, welche Gruppen es ueberhaupt gibt (das sind wenige: Definitionen mal Mandanten),
    /// rechnet je Gruppe die Frist aus und holt dann gezielt die faelligen.
    /// </remarks>
    public sealed class WorkflowRetentionGroup
    {
        /// <summary>Die Definitionszeile, mit der die Vorgaenge liefen.</summary>
        public int DefinitionKey { get; set; }

        /// <summary>Der Mandant, dem sie gehoeren.</summary>
        public string TenantId { get; set; }

        /// <summary>Wie viele beendete oberste Instanzen die Gruppe hat.</summary>
        public int Count { get; set; }

        /// <summary>
        /// Der aelteste Endzeitpunkt der Gruppe. Damit kann der Lauf eine Gruppe verwerfen, ohne eine
        /// einzige Instanz zu lesen: liegt selbst der aelteste noch nach dem Stichtag, ist nichts faellig.
        /// </summary>
        public DateTime OldestEndedUtc { get; set; }
    }

    /// <summary>
    /// Ein archivierter Vorgang - die flache Form, in der er die Aufbewahrungsfrist ueberlebt.
    /// </summary>
    /// <remarks>
    /// Felder bekommt nur, wonach gefiltert wird; alles Weitere liegt in <see cref="PayloadJson"/>.
    /// Der Name der Definition steht <b>als Text</b> dabei und nicht als Verweis: das Archiv soll
    /// lesbar bleiben, auch wenn die Definition laengst aufgeraeumt ist.
    /// </remarks>
    public sealed class WorkflowArchivedInstance
    {
        /// <summary>Die Id des Vorgangs, unveraendert.</summary>
        public string InstanceId { get; set; }

        /// <summary>Der Mandant, dem er gehoerte.</summary>
        public string TenantId { get; set; }

        /// <summary>Die Definitionszeile - nur nachrichtlich, ohne Fremdschluessel.</summary>
        public int DefinitionKey { get; set; }

        /// <summary>Die fachliche Id der Definition.</summary>
        public string DefinitionId { get; set; }

        /// <summary>Die Version der Definition.</summary>
        public int DefinitionVersion { get; set; }

        /// <summary>Der Name der Definition, wie er beim Archivieren lautete.</summary>
        public string DefinitionName { get; set; }

        /// <summary>Der Endstatus.</summary>
        public WorkflowStatus Status { get; set; }

        /// <summary>Wann der Vorgang begonnen hat (UTC).</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>Wann er geendet hat (UTC).</summary>
        public DateTime? EndedUtc { get; set; }

        /// <summary>Der Fehler-Code, oder null.</summary>
        public string FaultCode { get; set; }

        /// <summary>Die Fehlermeldung, oder null.</summary>
        public string FaultMessage { get; set; }

        /// <summary>Die oberste Instanz seines Prozessbaums.</summary>
        public string RootInstanceId { get; set; }

        /// <summary>Die aufrufende Instanz, oder null.</summary>
        public string ParentInstanceId { get; set; }

        /// <summary>Wann archiviert wurde (UTC).</summary>
        public DateTime ArchivedUtc { get; set; }

        /// <summary>
        /// Der Rest als JSON - siehe <see cref="WorkflowArchivePayload"/>. Bleibt hier bewusst ein
        /// Text: wer eine Liste zeigt, will ihn nicht laden.
        /// </summary>
        public string PayloadJson { get; set; }

        /// <summary>
        /// Wie viele Anhaenge der Vorgang hatte - als Feld, damit der Anhang-Lauf die Vorgaenge mit
        /// Anhaengen findet, ohne jede Nutzlast zu lesen.
        /// </summary>
        public int AttachmentCount { get; set; }

        /// <summary>
        /// Wann die Anhang-Inhalte weggefallen sind (UTC), oder null, solange sie noch da sind.
        /// </summary>
        public DateTime? AttachmentsPurgedUtc { get; set; }
    }

    /// <summary>
    /// Was von einem Vorgang ausser den Filter-Spalten bleibt: sein Verlauf, sein Endstand und was
    /// Menschen an ihn gehaengt haben.
    /// </summary>
    public sealed class WorkflowArchivePayload
    {
        /// <summary>
        /// Der Endstand der Variablen - <b>als eingebetteter Text</b>, nicht als Objekt.
        /// </summary>
        /// <remarks>
        /// Der Variablen-Stack laeuft ueber <c>WorkflowJson.SerializeVariables</c> und traegt je Wert
        /// seine eigene Typkennung. Als Objekt in diesem Nutzlast-Objekt liefe er durch den gewoehnlichen
        /// Serialisierer, und aus jeder Zahl wuerde beim Zurueckholen ein <c>JsonElement</c> - das Archiv
        /// haette den Endstand dann zwar noch, aber nicht mehr typtreu. Gelesen wird er mit
        /// <c>WorkflowJson.DeserializeVariables</c>.
        /// </remarks>
        public string VariablesJson { get; set; }

        /// <summary>Die Tokens in ihrem Endzustand.</summary>
        public List<Token> Tokens { get; set; } = new List<Token>();

        /// <summary>Der Verlauf.</summary>
        public List<HistoryEntry> History { get; set; } = new List<HistoryEntry>();

        /// <summary>Die Kommentare am Vorgang.</summary>
        public List<WorkflowArchivedComment> Comments { get; set; } = new List<WorkflowArchivedComment>();

        /// <summary>
        /// Die <b>Beschreibungen</b> der Anhaenge. Die Bytes bleiben, wo sie sind, und haben ihre eigene
        /// Frist; die Beschreibung bleibt in jedem Fall - ein Archiv, das nicht mehr sagen kann „hier war
        /// eine Datei", haette den Vorgang unvollstaendig festgehalten.
        /// </summary>
        public List<WorkflowArchivedAttachment> Attachments { get; set; }
            = new List<WorkflowArchivedAttachment>();
    }

    /// <summary>Ein Kommentar, wie er im Archiv steht.</summary>
    /// <remarks>
    /// Eine eigene, flache Form und nicht der Typ der Ablage: das Archiv soll ohne die Tabellen lesbar
    /// sein, aus denen es entstanden ist. Der Kern kennt Kommentare sonst nicht - ein Kommentar ist kein
    /// Prozess-Zustand -, aber ein archivierter Vorgang ohne sie waere nicht der ganze Vorgang.
    /// </remarks>
    public sealed class WorkflowArchivedComment
    {
        /// <summary>Die Aufgabe, bei der er entstand, oder null.</summary>
        public string TokenId { get; set; }

        /// <summary>Wer ihn geschrieben hat.</summary>
        public string Author { get; set; }

        /// <summary>Wann (UTC).</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>Der Text.</summary>
        public string Text { get; set; }
    }

    /// <summary>Die Beschreibung eines Anhangs, wie sie im Archiv steht.</summary>
    public sealed class WorkflowArchivedAttachment
    {
        /// <summary>Die Aufgabe, bei der er entstand, oder null.</summary>
        public string TokenId { get; set; }

        /// <summary>Der Dateiname.</summary>
        public string FileName { get; set; }

        /// <summary>Der Inhaltstyp, oder null.</summary>
        public string ContentType { get; set; }

        /// <summary>Die Groesse in Bytes.</summary>
        public long SizeBytes { get; set; }

        /// <summary>Wer ihn angehaengt hat.</summary>
        public string Author { get; set; }

        /// <summary>Wann (UTC).</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Die Kennung, unter der die Bytes liegen - solange es sie noch gibt. Sie bleibt auch danach
        /// stehen: sie sagt, WAS hier lag, und ein fremder Speicher kann sie spaeter noch brauchen.
        /// </summary>
        public string FileIdentifier { get; set; }
    }
}
