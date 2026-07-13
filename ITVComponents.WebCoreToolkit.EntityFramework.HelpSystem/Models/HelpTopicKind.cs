namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models
{
    /// <summary>Classifies a <see cref="HelpTopic"/> in the help tree.</summary>
    public enum HelpTopicKind
    {
        /// <summary>A grouping node without effective content — only holds child topics.</summary>
        Container = 0,

        /// <summary>A leaf (or nested) node that carries localized Markdown content.</summary>
        ContentPage = 1
    }
}
