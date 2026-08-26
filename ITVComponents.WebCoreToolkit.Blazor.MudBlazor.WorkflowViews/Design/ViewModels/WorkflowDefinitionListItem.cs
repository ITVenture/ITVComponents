namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.ViewModels
{
    /// <summary>Eine Zeile der Definition-Uebersicht.</summary>
    public sealed class WorkflowDefinitionListItem
    {
        /// <summary>
        /// Der Primaerschluessel der Definitionszeile - der Aufhaenger jeder Aktion (oeffnen, bearbeiten)
        /// und der Wert, den man in Einstellungen hinterlegt. Eindeutig ohne Namens- oder
        /// Mandanten-Aufloesung.
        /// </summary>
        public int Key { get; init; }

        /// <summary>
        /// Der sprechende Name der Definition. Zum Suchen und Wiedererkennen - nicht als Aufhaenger
        /// einer Aktion, weil derselbe Name je Mandant und Version mehrfach vorkommt.
        /// </summary>
        public string TechnicalName { get; init; } = "";

        /// <summary>Version.</summary>
        public int Version { get; init; }

        /// <summary>Anzeigename, oder null.</summary>
        public string? Name { get; init; }

        /// <summary>Anzahl Knoten (0, falls die Definition nicht gelesen werden konnte).</summary>
        public int NodeCount { get; init; }

        /// <summary>Anzahl Kanten.</summary>
        public int FlowCount { get; init; }

        /// <summary>
        /// Ob die Definition <b>oeffentlich</b> ist (fuer alle Mandanten). In der Liste sichtbar, weil
        /// derselbe Name je Mandant einmal vorkommen darf - ohne die Angabe waeren zwei Zeilen mit
        /// gleichem Namen nicht auseinanderzuhalten.
        /// </summary>
        public bool IsPublic { get; init; }

        /// <summary>Der Mandant, dem sie gehoert, oder null bei einer oeffentlichen.</summary>
        public string? TenantId { get; init; }
    }
}
