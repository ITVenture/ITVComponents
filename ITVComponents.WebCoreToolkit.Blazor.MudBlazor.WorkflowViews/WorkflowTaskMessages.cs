namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews
{
    /// <summary>
    /// Marker-Typ fuer die Texte der <b>Benutzer</b>-Oberflaeche dieses Moduls (Arbeitsliste und
    /// Aufgaben-Dialog). Komponenten injizieren <c>IStringLocalizer&lt;WorkflowTaskMessages&gt;</c>;
    /// aufgeloest wird gegen die eingebetteten <c>Resources/WorkflowTaskMessages.{culture}.resx</c>
    /// (neutral = Englisch).
    /// </summary>
    /// <remarks>
    /// Bewusst nur fuer die Aufgaben-Seite: Monitoring und Editor sind Betreiber-/Modellierer-Werkzeuge
    /// und heute unlokalisiert. Die Arbeitsliste ist die einzige Ansicht dieses Moduls, die ein normaler
    /// Benutzer taeglich sieht - sie darf die harten Texte der uebrigen Views nicht erben.
    /// <para>
    /// Die <b>Inhalte</b> (Aufgabentitel, Feldbeschriftungen) sind dagegen Daten aus der Definition und
    /// werden nicht hier, sondern per Kultur-JSON uebersetzt (<c>StringExtensions.Translate</c> - dieselbe
    /// Konvention wie bei den Navigations-Eintraegen).
    /// </para>
    /// </remarks>
    public sealed class WorkflowTaskMessages
    {
    }
}
