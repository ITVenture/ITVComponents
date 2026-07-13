namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Handlers
{
    /// <summary>
    /// Permission names that gate the help-system admin surface. Per area a <c>.View</c> (read) and
    /// <c>.Write</c> (read + write) under the <c>Help.Admin.*</c> prefix; <c>.Write</c> implies <c>.View</c>.
    /// The admin pages additionally require the <c>ITVAdminViews</c> feature (via SecureView). The public
    /// viewer is anonymous and ungated.
    /// </summary>
    public static class HelpPermissions
    {
        public const string TopicsView = "Help.Admin.Topics.View";
        public const string TopicsWrite = "Help.Admin.Topics.Write";
        public const string ResourcesView = "Help.Admin.Resources.View";
        public const string ResourcesWrite = "Help.Admin.Resources.Write";

        /// <summary>Read access to the topics surface (View or Write).</summary>
        public static readonly string[] TopicsRead = { TopicsView, TopicsWrite };

        /// <summary>Write access to topics.</summary>
        public static readonly string[] TopicsWriteAny = { TopicsWrite };

        /// <summary>Read access to the resources surface (View or Write).</summary>
        public static readonly string[] ResourcesRead = { ResourcesView, ResourcesWrite };

        /// <summary>Write access to resources.</summary>
        public static readonly string[] ResourcesWriteAny = { ResourcesWrite };
    }
}
