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

/// <summary>Requests the entries surrounding a single log entry, to follow what happened around it.</summary>
public sealed class SystemLogContextQuery
{
    public const int MaxContextSize = 500;

    /// <summary>The entry to center the result on.</summary>
    public int AnchorId { get; init; }

    /// <summary>Number of entries logged before the anchor. Clamped to 0..<see cref="MaxContextSize"/>.</summary>
    public int Before { get; init; } = 20;

    /// <summary>Number of entries logged after the anchor. Clamped to 0..<see cref="MaxContextSize"/>.</summary>
    public int After { get; init; } = 20;

    /// <summary>When set, only entries of the anchor's category are considered.</summary>
    public bool SameCategoryOnly { get; init; }

    /// <summary>
    /// When non-empty, only entries whose level is in this set are considered — so noisy levels (e.g. Debug/Trace)
    /// can be switched off while the interesting ones stay. An empty or null set means "no level constraint" (all
    /// levels). The anchor itself is always included regardless of this filter.
    /// </summary>
    public IReadOnlyCollection<LogLevel>? IncludeLevels { get; init; }

    /// <summary>
    /// When set, only entries whose message, title or category contains this text are considered, so the window can
    /// be narrowed to what is actually being traced. The anchor itself is always included regardless of this filter.
    /// </summary>
    public string? Search { get; init; }
}

public sealed class SystemLogContextResult
{
    /// <summary>Anchor plus surrounding entries, oldest first. Empty when the anchor no longer exists.</summary>
    public IReadOnlyList<SystemEventViewModel> Items { get; init; } = Array.Empty<SystemEventViewModel>();

    /// <summary>The entry the context was built around.</summary>
    public SystemEventViewModel? Anchor { get; init; }

    /// <summary>True when further entries exist before the first returned one.</summary>
    public bool HasMoreBefore { get; init; }

    /// <summary>True when further entries exist after the last returned one.</summary>
    public bool HasMoreAfter { get; init; }
}
