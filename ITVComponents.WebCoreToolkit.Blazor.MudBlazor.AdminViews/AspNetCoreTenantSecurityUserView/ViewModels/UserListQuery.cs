namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTenantSecurityUserView.ViewModels;

public sealed class UserListQuery
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

public sealed class UserListContext
{
    public bool IsSysAdmin { get; init; }
    public int? CurrentTenantId { get; init; }
}
