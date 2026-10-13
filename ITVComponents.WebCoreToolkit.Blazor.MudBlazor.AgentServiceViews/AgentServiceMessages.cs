namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AgentServiceViews
{
    /// <summary>
    /// Marker-Typ fuer die Texte des Reiters „Geraete" in der Detailzeile der ClientApps. Komponenten injizieren
    /// <c>IStringLocalizer&lt;AgentServiceMessages&gt;</c>; aufgeloest wird gegen die eingebetteten
    /// <c>Resources/AgentServiceMessages.{culture}.resx</c> (neutral = Englisch).
    /// </summary>
    /// <remarks>
    /// Lokalisiert wie die uebrigen ClientApp-Masken: dort richtet der <b>Kunde</b> seine Kassen ein. Die
    /// Beschriftungen der Geraete-Einstellungen kommen dagegen vom Geraet am Agenten und werden hier nicht
    /// uebersetzt.
    /// </remarks>
    public sealed class AgentServiceMessages
    {
    }
}
