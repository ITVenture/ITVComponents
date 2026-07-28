using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ITVComponents.Formatting;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Workflow;
using ITVComponents.Workflow.Model;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks
{
    /// <summary>
    /// Der Kontext, den der Aufgaben-Mantel an die Masken-Komponente cascaded: die Daten der Aufgabe und
    /// der EINE Weg, sie abzuschliessen.
    /// </summary>
    /// <remarks>
    /// Bewusst als <c>[CascadingParameter]</c> und nicht als Parameter-Dictionary einer
    /// <c>DynamicComponent</c>: Parameternamen einer DynamicComponent werden erst zur Laufzeit geprueft
    /// (ein Tippfehler faellt beim Oeffnen des Dialogs auf, nicht beim Uebersetzen). Ausserdem laeuft
    /// dieselbe Komponente so unveraendert im Registry-Weg UND direkt aufgeklebt auf einer eigenen Seite.
    /// <para>
    /// Der Abschluss (<see cref="CompleteAsync"/>) gehoert bewusst dem Mantel: Ergebnis-Mapping,
    /// Versionskonflikt, Doppel-Klick und "war schon erledigt" gibt es genau EINMAL - nicht in jeder Maske
    /// neu.
    /// </para>
    /// </remarks>
    public sealed class WorkflowTaskContext
    {
        private readonly Func<IDictionary<string, object>?, Task<UserTaskCompletionStatus>> complete;
        private readonly Func<string, string?> translate;

        /// <summary>Erzeugt den Kontext (macht der Mantel).</summary>
        public WorkflowTaskContext(UserTaskDescriptor descriptor,
            Func<IDictionary<string, object>?, Task<UserTaskCompletionStatus>> complete,
            Func<string, string?> translate)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
            this.translate = translate ?? (s => s);
        }

        /// <summary>Die Aufgabe mit allen Rohdaten (Payload, Feld-Deklaration, Schluessel).</summary>
        public UserTaskDescriptor Descriptor { get; }

        /// <summary>Die Instanz, zu der die Aufgabe gehoert.</summary>
        public string InstanceId => Descriptor.InstanceId;

        /// <summary>Das Token - die Aufgabe selbst.</summary>
        public string TokenId => Descriptor.TokenId;

        /// <summary>Die Aufgabenart.</summary>
        public string? TaskKey => Descriptor.TaskKey;

        /// <summary>Die aufgeloesten Eingabewerte, die die Maske anzeigen soll.</summary>
        public IReadOnlyDictionary<string, object> Payload => Descriptor.Payload;

        /// <summary>Die Deklaration der generischen Maske (leer, wenn die Aufgabe nur bestaetigt wird).</summary>
        public IReadOnlyList<UserTaskField> FormFields => Descriptor.FormFields;

        /// <summary>
        /// Der uebersetzte Titel (Kultur-JSON ist hier bereits aufgeloest). Die Prototyp-Formatierung mit dem
        /// Format-Datenobjekt ist beim Titel schon BEIM PARKEN passiert (damit er auch in der Arbeitsliste
        /// fertig steht und mit dem damaligen Stand eingefroren ist) - hier wird er nur noch uebersetzt.
        /// </summary>
        public string? Title => translate(Descriptor.Title ?? string.Empty);

        /// <summary>
        /// Die uebersetzte Beschreibung. Ist am Knoten ein Formatierungs-Datenobjekt hinterlegt
        /// (<see cref="UserTaskDescriptor.FormatData"/>, aus <see cref="UserActivityNode.FormatData"/>), wird
        /// die uebersetzte Beschreibung als <b>Prototyp</b> behandelt und mit den Werten des Objekts formatiert
        /// (Platzhalter wie <c>[InvoiceNo:0000000000]</c>). Ohne Datenobjekt bleibt sie unveraendert.
        /// </summary>
        public string? Description => Format(translate(Descriptor.Description ?? string.Empty));

        /// <summary>
        /// Uebersetzt einen Wert aus der Definition (Klartext oder Kultur-JSON) in die Kultur des
        /// Lesers - fuer Beschriftungen, die eine eigene Maske selbst aus der Deklaration zieht.
        /// </summary>
        public string? Translate(string? value) => value == null ? null : translate(value);

        /// <summary>
        /// Schliesst die Aufgabe mit dem Ergebnis der Maske ab. Der Mantel kuemmert sich um Meldung,
        /// Dialogschluss und den Fall "war schon erledigt"; die Maske muss den Rueckgabewert nur dann
        /// auswerten, wenn sie selbst darauf reagieren will.
        /// </summary>
        public Task<UserTaskCompletionStatus> CompleteAsync(IDictionary<string, object>? result)
            => complete(result);

        /// <summary>
        /// Wendet die Prototyp-Formatierung auf einen (bereits uebersetzten) Text an - Titel oder
        /// Beschreibung -, wenn ein Datenobjekt vorliegt. Ohne Datenobjekt - der Normalfall - bleibt der Text
        /// unveraendert; so kann ein bestehender Text mit eckigen Klammern nicht versehentlich als Format
        /// gedeutet werden. Ein Formatierungsfehler darf die Aufgabe nicht unanzeigbar machen (dann eben
        /// unformatiert), muss aber ins Log.
        /// </summary>
        private string? Format(string? translated)
        {
            if (string.IsNullOrEmpty(translated) || Descriptor.FormatData is null)
            {
                return translated;
            }

            try
            {
                return Descriptor.FormatData.FormatText(translated, TextFormat.DefaultFormatPolicyWithPrimitives);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"WorkflowTaskContext: text formatting failed for instance '{InstanceId}' token " +
                    $"'{TokenId}': {ex.OutlineException()}", LogSeverity.Warning);
                return translated;
            }
        }
    }
}
