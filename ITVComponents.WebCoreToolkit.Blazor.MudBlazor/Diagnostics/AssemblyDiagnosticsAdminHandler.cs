using System.Reflection;
using System.Runtime.Loader;
using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Diagnostics;

public class AssemblyDiagnosticsAdminHandler : IAssemblyDiagnosticsAdminHandler
{
    private const string ViewPermission = "AssemblyDiagnostics.View";

    private readonly IServiceProvider services;
    private readonly HealthCheckService? health;

    public AssemblyDiagnosticsAdminHandler(IServiceProvider services)
    {
        this.services = services;
        health = services.GetService<HealthCheckService>();
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public bool HealthAvailable => health != null;

    public IReadOnlyList<AssemblyInfoViewModel> ListAssemblies(ClaimsPrincipal user)
    {
        if (!HasPermission(user, ViewPermission)) return Array.Empty<AssemblyInfoViewModel>();

        return AssemblyLoadContext.All
            .SelectMany(ctx => ctx.Assemblies.Select(a => new { Context = ctx.Name, Assembly = a }))
            .Select(n => new AssemblyInfoViewModel
            {
                AssemblyVersion = n.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? n.Assembly.GetCustomAttribute<AssemblyVersionAttribute>()?.Version
                    ?? "--UNKNOWN--",
                FullName = n.Assembly.FullName,
                IsDynamic = n.Assembly.IsDynamic,
                Location = !n.Assembly.IsDynamic ? n.Assembly.Location : "--DYNAMIC--",
                LoadContext = n.Context,
                RuntimeVersion = n.Assembly.ImageRuntimeVersion,
                IsCollectible = n.Assembly.IsCollectible
            })
            .OrderBy(a => a.LoadContext)
            .ThenBy(a => a.FullName)
            .ToList();
    }

    public IReadOnlyList<ClaimInfoViewModel> ListClaims(ClaimsPrincipal user)
    {
        if (!HasPermission(user, ViewPermission)) return Array.Empty<ClaimInfoViewModel>();
        if (user.Identity is not ClaimsIdentity) return Array.Empty<ClaimInfoViewModel>();

        return user.Claims.Select(c => new ClaimInfoViewModel
        {
            Type = c.Type,
            Value = c.Value,
            ValueType = c.ValueType,
            Issuer = c.Issuer,
            OriginalIssuer = c.OriginalIssuer
        }).ToList();
    }

    public async Task<IReadOnlyList<HealthTestViewModel>> ListHealthAsync(ClaimsPrincipal user)
    {
        if (!HasPermission(user, ViewPermission) || health == null) return Array.Empty<HealthTestViewModel>();

        var report = await health.CheckHealthAsync();
        return report.Entries.Select(e => new HealthTestViewModel
        {
            Name = e.Key,
            Result = e.Value.Status.ToString(),
            Description = e.Value.Description,
            Tags = string.Join(", ", e.Value.Tags),
            Message = e.Value.Exception?.Message ?? "OK",
            HasDetails = e.Value.Data.Any(d => d.Value is IHealthDetailResult)
        }).OrderBy(t => t.Name).ToList();
    }

    public async Task<IReadOnlyList<HealthTestViewModel>> ListHealthDetailAsync(ClaimsPrincipal user, string checkName)
    {
        if (!HasPermission(user, ViewPermission) || health == null) return Array.Empty<HealthTestViewModel>();

        var report = await health.CheckHealthAsync(p => p.Name == checkName);
        if (!report.Entries.TryGetValue(checkName, out var entry)) return Array.Empty<HealthTestViewModel>();

        return entry.Data
            .Where(d => d.Value is IHealthDetailResult)
            .Select(d =>
            {
                var m = (IHealthDetailResult)d.Value;
                return new HealthTestViewModel
                {
                    Name = d.Key,
                    Result = m.Status.ToString(),
                    Message = m.StatusText
                };
            }).ToList();
    }
}
