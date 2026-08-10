using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets
{
    /// <summary>
    /// A click-action from inside a widget template, together with the widget it came from.
    /// </summary>
    /// <remarks>
    /// The dashboard forwards the action instead of acting on it: what a <c>drill</c> or <c>open</c> means
    /// is the consuming application's business - it navigates, opens a dialog, or ignores it.
    /// </remarks>
    /// <param name="Widget">the widget whose template raised the action</param>
    /// <param name="Action">the action name and its optional argument</param>
    public sealed record DashboardWidgetActionArgs(DashboardWidgetDefinition Widget, WidgetAction Action);
}
