using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public sealed class SystemEventViewModel
{
    public int SystemEventId { get; set; }
    public LogLevel LogLevel { get; set; }
    public string? Category { get; set; }
    public string? Title { get; set; }
    public string? Message { get; set; }
    public DateTime EventTime { get; set; }
}

public sealed class SystemLogQuery
{
    public int Page { get; init; }
    public int PageSize { get; init; } = 50;
    public string? Search { get; init; }
    public LogLevel? MinimumLevel { get; init; }
    public LogLevel? MaximumLevel { get; init; }
    public string? Category { get; init; }
    public string? Title { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}
