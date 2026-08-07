using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents
{
    /// <summary>
    /// Die Art eines deklarierten Feldes - sie bestimmt, welches Eingabe-Element
    /// <c>DeclaredFieldsForm</c> rendert.
    /// </summary>
    public enum DeclaredFieldKind
    {
        /// <summary>Einzeiliger Text (Vorgabe).</summary>
        Text,

        /// <summary>Mehrzeiliger Text - nimmt die volle Breite ein.</summary>
        MultilineText,

        /// <summary>Zahl.</summary>
        Number,

        /// <summary>Ja/Nein-Schalter.</summary>
        Boolean,

        /// <summary>Datum.</summary>
        Date,

        /// <summary>Auswahl aus <see cref="DeclaredField.Choices"/>.</summary>
        Choice
    }

    /// <summary>
    /// Ein deklariertes Eingabefeld - die neutrale Beschreibung, gegen die <c>DeclaredFieldsForm</c>
    /// rendert.
    /// </summary>
    /// <remarks>
    /// Bewusst ein eigenes Modell und kein Interface, das die aufrufenden Modelle implementieren: die
    /// Feld-Deklarationen der Konsumenten liegen in Paketen, die dieses hier NICHT kennen duerfen
    /// (<c>UserTaskField</c> etwa in der Workflow-Engine, die weder das WebCoreToolkit noch Blazor
    /// referenziert). Ein gemeinsames Interface haette die Abhaengigkeit in die falsche Richtung gezogen.
    /// Stattdessen bildet jeder Konsument sein Modell an seiner eigenen Kante auf dieses hier ab - eine
    /// Projektion von rund zwanzig Zeilen dort, wo die Abhaengigkeit ohnehin schon besteht.
    /// </remarks>
    public class DeclaredField
    {
        /// <summary>
        /// Der Name des Feldes. Er ist der Schluessel im Ergebnis der Maske.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Die Beschriftung. Uebersetzt wird sie nicht hier, sondern ueber
        /// <c>DeclaredFieldsForm.TranslateText</c> - leer heisst: <see cref="Name"/> wird angezeigt.
        /// </summary>
        public string? Label { get; set; }

        /// <summary>Die Art des Feldes (Vorgabe: einzeiliger Text).</summary>
        public DeclaredFieldKind Kind { get; set; } = DeclaredFieldKind.Text;

        /// <summary>Muss das Feld ausgefuellt sein, damit die Maske ein Ergebnis liefert?</summary>
        public bool Required { get; set; }

        /// <summary>
        /// Nur anzeigen, nicht erfassen: das Feld zeigt den Wert aus der Vorbelegung und geht nicht ins
        /// Ergebnis ein.
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>Optionaler Hinweistext unter dem Feld.</summary>
        public string? HelpText { get; set; }

        /// <summary>
        /// Optionaler Name in der Vorbelegung, aus dem das Feld gefuellt wird. Leer = <see cref="Name"/>.
        /// </summary>
        public string? PrefillName { get; set; }

        /// <summary>Die Auswahlmoeglichkeiten - nur bei <see cref="DeclaredFieldKind.Choice"/>.</summary>
        public IReadOnlyList<DeclaredChoice>? Choices { get; set; }
    }

    /// <summary>Eine Auswahlmoeglichkeit eines <see cref="DeclaredFieldKind.Choice"/>-Feldes.</summary>
    public class DeclaredChoice
    {
        /// <summary>Der Wert, der ins Ergebnis geht.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>Die Beschriftung; leer = <see cref="Value"/>.</summary>
        public string? Label { get; set; }
    }
}
