namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.ViewModels
{
    /// <summary>Eine Zeile der Definition-Uebersicht.</summary>
    public sealed class WorkflowDefinitionListItem
    {
        /// <summary>Fachliche Id der Definition.</summary>
        public string Id { get; init; } = "";

        /// <summary>Version.</summary>
        public int Version { get; init; }

        /// <summary>Anzeigename, oder null.</summary>
        public string? Name { get; init; }

        /// <summary>Anzahl Knoten (0, falls die Definition nicht gelesen werden konnte).</summary>
        public int NodeCount { get; init; }

        /// <summary>Anzahl Kanten.</summary>
        public int FlowCount { get; init; }
    }
}
