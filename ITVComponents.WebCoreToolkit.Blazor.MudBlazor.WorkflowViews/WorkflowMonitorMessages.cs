namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews
{
    /// <summary>
    /// Marker-Typ fuer die Texte der <b>Betriebs</b>-Oberflaeche dieses Moduls (Vorgangs-Uebersicht,
    /// Vorgangs-Detail, Start von Hand, Wiederholen, Anhalten und die zentralen Ablaeufe). Komponenten
    /// injizieren <c>IStringLocalizer&lt;WorkflowMonitorMessages&gt;</c>; aufgeloest wird gegen die
    /// eingebetteten <c>Resources/WorkflowMonitorMessages.{culture}.resx</c> (neutral = Englisch).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Getrennt von <see cref="WorkflowTaskMessages"/>, obwohl beide Oberflaechen lokalisiert sind: die
    /// Arbeitsliste gehoert dem Sachbearbeiter, diese Ansichten gehoeren dem Betrieb. Wer eine davon in
    /// einer Anwendung nicht ausliefert, soll nicht die Texte der anderen mitschleppen - und die Begriffe
    /// unterscheiden sich (dort "Aufgabe", hier "Vorgang").
    /// </para>
    /// <para>
    /// <b>Nicht</b> fuer den Editor: der ist ein Modellierer-Werkzeug und bleibt bewusst unlokalisiert -
    /// wer Ablaeufe modelliert, arbeitet ohnehin mit den englischen Begriffen des Modells.
    /// </para>
    /// <para>
    /// Die <b>Inhalte</b> (Namen von Definitionen und Knoten, Beschriftungen aus dem Modell) sind Daten
    /// und werden nicht hier uebersetzt, sondern per Kultur-JSON - dieselbe Konvention wie bei der
    /// Arbeitsliste und den Navigations-Eintraegen.
    /// </para>
    /// </remarks>
    public sealed class WorkflowMonitorMessages
    {
    }
}
