using System.Security.Claims;
using ITVComponents.EFRepo.DataSync.Models;

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

    /// <summary>
    /// Applies the reviewed configuration changes (with their per-change/per-detail <c>Apply</c> flags and the
    /// user-edited <c>NewValue</c>s) through the registered <c>IConfigurationHandler</c>. Mirrors the MVC
    /// AssemblyDiagnosticsController.ApplyChanges flow.
    /// </summary>
    /// <param name="user">the acting user (permission-gated like the other diagnostics operations)</param>
    /// <param name="changes">the changes to apply, as edited/selected in the diff editor</param>
    /// <returns>collected handler messages, or null on a clean apply</returns>
    string? ApplyConfigChanges(ClaimsPrincipal user, IEnumerable<Change> changes);
}
