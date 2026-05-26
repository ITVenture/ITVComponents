using System.Security.Claims;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Diagnostics;

public interface IAssemblyDiagnosticsAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    /// <summary>True when a HealthCheckService is registered in the container.</summary>
    bool HealthAvailable { get; }

    IReadOnlyList<AssemblyInfoViewModel> ListAssemblies(ClaimsPrincipal user);

    IReadOnlyList<ClaimInfoViewModel> ListClaims(ClaimsPrincipal user);

    Task<IReadOnlyList<HealthTestViewModel>> ListHealthAsync(ClaimsPrincipal user);

    Task<IReadOnlyList<HealthTestViewModel>> ListHealthDetailAsync(ClaimsPrincipal user, string checkName);
}
