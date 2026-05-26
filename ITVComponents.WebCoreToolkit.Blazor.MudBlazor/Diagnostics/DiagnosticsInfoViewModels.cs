namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Diagnostics;

public sealed class AssemblyInfoViewModel
{
    public string? FullName { get; set; }
    public string? AssemblyVersion { get; set; }
    public string? Location { get; set; }
    public string? LoadContext { get; set; }
    public string? RuntimeVersion { get; set; }
    public bool IsDynamic { get; set; }
    public bool IsCollectible { get; set; }
}

public sealed class ClaimInfoViewModel
{
    public string? Type { get; set; }
    public string? Value { get; set; }
    public string? ValueType { get; set; }
    public string? Issuer { get; set; }
    public string? OriginalIssuer { get; set; }
}

public sealed class HealthTestViewModel
{
    public string? Name { get; set; }
    public string? Result { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public string? Message { get; set; }
    public bool HasDetails { get; set; }
}
