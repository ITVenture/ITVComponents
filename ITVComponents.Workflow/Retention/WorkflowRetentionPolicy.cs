using System;
using ITVComponents.Workflow.Model;

namespace ITVComponents.Workflow.Retention
{
    /// <summary>
    /// Der Widerspruch eines Mandanten gegen die Fristen einer Definition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Haengt an der FACHLICHEN Identitaet</b> (Besitzer + Definitions-Id + widersprechender
    /// Mandant), nicht am technischen Schluessel der Definitionszeile. Der wird bei jeder neuen Version
    /// neu vergeben - ein Widerspruch, der daran haengt, waere beim naechsten Veroeffentlichen still
    /// weg, und es gaelte wieder die Vorgabe. Dieselbe Lehre wie bei den Ausloeser-Uebernahmen, die aus
    /// genau diesem Grund nicht am <c>TriggerKey</c> haengen.
    /// </para>
    /// <para>
    /// <b>Der Besitzer gehoert dazu, nicht nur die Id.</b> Legt ein Mandant eine eigene Definition
    /// gleichen Namens neben der oeffentlichen an, waeren das ohne ihn dieselbe Zeile - ein Widerspruch
    /// gaelte still fuer beide, obwohl die zwei Definitionen verschiedene Rahmen setzen und die eine ihn
    /// vielleicht gar nicht erlaubt. Dieselbe Form wie bei <c>WorkflowStartTriggerActivation</c>.
    /// </para>
    /// <para>
    /// <b>Eine Ruecknahme loescht die Zeile nicht</b>: beide Fristen auf null heisst „nichts gesagt", die
    /// Vorgabe greift wieder - aber <see cref="SetBy"/> und <see cref="SetUtc"/> halten fest, wer sie
    /// wann zurueckgenommen hat. Bei einer Einstellung, an der das Loeschen von Daten haengt, ist das
    /// die Spur, die spaeter gesucht wird.
    /// </para>
    /// </remarks>
    public sealed class WorkflowRetentionOverride
    {
        /// <summary>
        /// Der Mandant der DEFINITION, null = die oeffentliche. Teil der Identitaet - nicht der
        /// Widersprechende, das ist <see cref="TenantId"/>.
        /// </summary>
        public string OwnerTenantId { get; set; }

        /// <summary>Die fachliche Id der Definition. Teil der Identitaet.</summary>
        public string DefinitionId { get; set; }

        /// <summary>Der Mandant, der widerspricht. Teil der Identitaet.</summary>
        public string TenantId { get; set; }

        /// <summary>Seine Frist bis zum Archivieren, oder null (dann gilt die Vorgabe).</summary>
        public int? RetentionDays { get; set; }

        /// <summary>Seine Frist fuer die Anhang-Inhalte, oder null (dann gilt die Vorgabe).</summary>
        public int? AttachmentRetentionDays { get; set; }

        /// <summary>Wer den Widerspruch eingelegt hat - fuer die Nachvollziehbarkeit.</summary>
        public string SetBy { get; set; }

        /// <summary>Wann - von der Ablage gesetzt, wenn der Aufrufer nichts mitbringt.</summary>
        public DateTime SetUtc { get; set; }
    }

    /// <summary>Die globalen Vorgaben, wenn weder Definition noch Mandant etwas sagen.</summary>
    public sealed class WorkflowRetentionDefaults
    {
        /// <summary>Tage bis zum Archivieren, oder null = nie archivieren.</summary>
        public int? RetentionDays { get; init; }

        /// <summary>Tage bis zum Wegfallen der Anhang-Inhalte, oder null = nie.</summary>
        public int? AttachmentRetentionDays { get; init; }
    }

    /// <summary>Woher eine geltende Frist stammt - fuer die Anzeige und fuer das Protokoll.</summary>
    public enum RetentionSource
    {
        /// <summary>Niemand hat etwas gesagt: es wird nicht aufgeraeumt.</summary>
        None,

        /// <summary>Die globale Vorgabe.</summary>
        Global,

        /// <summary>Die Vorgabe der Definition.</summary>
        Definition,

        /// <summary>Der Widerspruch des Mandanten.</summary>
        Tenant
    }

    /// <summary>Eine geltende Frist samt ihrer Herkunft.</summary>
    /// <param name="Days">die Tage, oder null = kein Aufraeumen</param>
    /// <param name="Source">woher der Wert stammt</param>
    /// <param name="RequestedDays">
    /// was der Mandant wollte, falls der Rahmen es begrenzt hat - sonst null
    /// </param>
    public readonly record struct EffectiveRetention(int? Days, RetentionSource Source,
        int? RequestedDays = null)
    {
        /// <summary>Wird ueberhaupt aufgeraeumt?</summary>
        public bool Applies => Days.HasValue;

        /// <summary>
        /// Wurde der Wunsch des Mandanten vom Rahmen begrenzt? Dann steht in
        /// <see cref="RequestedDays"/>, was er wollte.
        /// </summary>
        /// <remarks>
        /// <b>Begrenzt und nicht verworfen</b> - aber es wird gesagt. Still zu begrenzen hiesse, dass ein
        /// Mandant zehn Tage einstellt, dreissig bekommt und es nirgends erfaehrt. Bei einer Einstellung,
        /// an der das Loeschen von Daten haengt, ist das die schlechteste aller Auskuenfte.
        /// </remarks>
        public bool WasLimited => RequestedDays.HasValue;

        /// <summary>
        /// Der Stichtag: alles, was VOR diesem Zeitpunkt geendet hat, ist faellig. Null, wenn nicht
        /// aufgeraeumt wird.
        /// </summary>
        /// <param name="nowUtc">der aktuelle Zeitpunkt</param>
        public DateTime? DueBefore(DateTime nowUtc)
            => Days.HasValue ? nowUtc.AddDays(-Days.Value) : null;
    }

    /// <summary>
    /// Entscheidet, welche Aufbewahrungsfrist fuer einen Vorgang gilt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die Kette: <b>Widerspruch des Mandanten -&gt; Vorgabe der Definition -&gt; globale Vorgabe</b>.
    /// Jede Stufe null heisst „erben"; sagt niemand etwas, wird nicht aufgeraeumt. Das ist die sichere
    /// Richtung - wer nichts einstellt, verliert nichts.
    /// </para>
    /// <para>
    /// <b>Der Widerspruch gilt nur, wenn die Definition ihn zulaesst</b>
    /// (<see cref="WorkflowDefinition.AllowTenantRetentionOverride"/>). Ein Widerspruch gegen eine
    /// Definition, die das nicht erlaubt, wird nicht etwa abgelehnt - er bleibt stehen und wirkt einfach
    /// nicht, so wie eine Muster-Uebersteuerung ohne <c>AllowReschedule</c>. Der Grund ist derselbe:
    /// erlaubt die Definition es spaeter doch, soll der Wunsch des Mandanten noch da sein.
    /// </para>
    /// <para>
    /// Reine Rechnung, absichtlich ohne Ablage und ohne Uhr: der Aufrufer bringt beides mit. So ist die
    /// Regel gegen beide Speicher-Fassungen und ohne Datenbank pruefbar - und die Frage „warum wurde das
    /// weggeraeumt?" laesst sich an einem Tisch beantworten statt an einem Datensatz.
    /// </para></remarks>
    public static class WorkflowRetentionPolicy
    {
        /// <summary>Die Frist bis zum Archivieren.</summary>
        /// <param name="definition">die Definition des Vorgangs</param>
        /// <param name="tenantOverride">der Widerspruch des Mandanten, oder null</param>
        /// <param name="defaults">die globalen Vorgaben, oder null</param>
        public static EffectiveRetention Archive(WorkflowDefinition definition,
            WorkflowRetentionOverride tenantOverride, WorkflowRetentionDefaults defaults)
            => Resolve(
                MayOverride(definition) ? tenantOverride?.RetentionDays : null,
                definition?.RetentionDays,
                defaults?.RetentionDays,
                definition?.MinTenantRetentionDays,
                definition?.MaxTenantRetentionDays);

        /// <summary>Die Frist fuer die Anhang-Inhalte.</summary>
        /// <param name="definition">die Definition des Vorgangs</param>
        /// <param name="tenantOverride">der Widerspruch des Mandanten, oder null</param>
        /// <param name="defaults">die globalen Vorgaben, oder null</param>
        public static EffectiveRetention Attachments(WorkflowDefinition definition,
            WorkflowRetentionOverride tenantOverride, WorkflowRetentionDefaults defaults)
            => Resolve(
                MayOverride(definition) ? tenantOverride?.AttachmentRetentionDays : null,
                definition?.AttachmentRetentionDays,
                defaults?.AttachmentRetentionDays,
                definition?.MinTenantAttachmentRetentionDays,
                definition?.MaxTenantAttachmentRetentionDays);

        /// <summary>
        /// Wirkt ein Widerspruch dieses Mandanten ueberhaupt? Ohne Definition nein - dann ist nicht zu
        /// entscheiden, ob sie ihn zulaesst, und „im Zweifel wirkt er" waere bei Fristen die falsche
        /// Richtung.
        /// </summary>
        public static bool MayOverride(WorkflowDefinition definition)
            => definition is { AllowTenantRetentionOverride: true };

        private static EffectiveRetention Resolve(int? tenant, int? definition, int? global,
            int? min, int? max)
        {
            if (Valid(tenant))
            {
                int granted = Clamp(tenant.Value, min, max);
                return granted == tenant.Value
                    ? new EffectiveRetention(tenant, RetentionSource.Tenant)
                    : new EffectiveRetention(granted, RetentionSource.Tenant, tenant);
            }

            if (Valid(definition))
            {
                return new EffectiveRetention(definition, RetentionSource.Definition);
            }

            return Valid(global)
                ? new EffectiveRetention(global, RetentionSource.Global)
                : new EffectiveRetention(null, RetentionSource.None);
        }

        /// <summary>
        /// Eine Frist zaehlt nur, wenn sie nicht negativ ist. <b>Null Tage sind erlaubt</b> und heissen
        /// „sofort nach dem Ende" - eine sinnvolle Ansage. Eine negative Zahl ist dagegen keine kuerzere
        /// Frist, sondern ein Fehler, und der darf nicht zu einem Stichtag in der Zukunft fuehren: der
        /// raeumte Vorgaenge weg, die noch gar nicht geendet haben.
        /// </summary>
        private static bool Valid(int? days) => days is >= 0;

        /// <summary>
        /// Haelt den Wunsch des Mandanten im Rahmen der Definition.
        /// </summary>
        /// <remarks>
        /// <b>Ein widerspruechlicher Rahmen (Untergrenze groesser als Obergrenze) wird ganz ignoriert</b>
        /// statt in einer der beiden Richtungen aufgeloest. Er ist ein Fehler des Autors, und welche der
        /// beiden Grenzen "gewinnt", waere geraten - beide Antworten liessen sich begruenden, und genau
        /// deshalb darf sie hier nicht fallen. Der Validator der Definition ist die Stelle, die das
        /// bemaengelt; bis dahin gilt der Wunsch des Mandanten unveraendert.
        /// </remarks>
        private static int Clamp(int days, int? min, int? max)
        {
            if (Valid(min) && Valid(max) && min.Value > max.Value)
            {
                return days;
            }

            if (Valid(min) && days < min.Value)
            {
                return min.Value;
            }

            return Valid(max) && days > max.Value ? max.Value : days;
        }
    }
}
