namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets;

/// <summary>
/// Click-action invoked by a <see cref="WidgetRenderer"/> when a template element with
/// <c>data-widget-action</c> is clicked. The action name and optional argument are taken from
/// the template's <c>data-widget-action</c> / <c>data-widget-arg</c> attributes.
/// </summary>
public sealed record WidgetAction(string Action, string? Arg);
