namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public sealed class ListQuery
{
    public int Page { get; init; }
    public int PageSize { get; init; } = 25;
    public string? SortColumn { get; init; }
    public bool SortDescending { get; init; }
    public string? Search { get; init; }
    public int? TenantId { get; init; }
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
}

public sealed class AdminContext
{
    public bool IsSysAdmin { get; init; }
    public int? CurrentTenantId { get; init; }
}
