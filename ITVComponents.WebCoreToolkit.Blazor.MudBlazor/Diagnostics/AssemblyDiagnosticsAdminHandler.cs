using System.Reflection;
using System.Runtime.Loader;
using System.Security.Claims;
using System.Text;
using ITVComponents.EFRepo.DataSync;
using ITVComponents.EFRepo.DataSync.Models;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Health;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
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

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public bool HealthAvailable => health != null;

    public IReadOnlyList<AssemblyInfoViewModel> ListAssemblies(ClaimsPrincipal user)
    {
        if (!HasPermission(ViewPermission)) return Array.Empty<AssemblyInfoViewModel>();

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
        if (!HasPermission(ViewPermission)) return Array.Empty<ClaimInfoViewModel>();
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
        if (!HasPermission(ViewPermission) || health == null) return Array.Empty<HealthTestViewModel>();

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
        if (!HasPermission(ViewPermission) || health == null) return Array.Empty<HealthTestViewModel>();

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

    /// <summary>
    /// Applies the reviewed changes.
    /// </summary>
    /// <remarks>
    /// Das Einspielen SCHREIBT - es laeuft deshalb in einer EIGENEN Lade-Scope
    /// (<see cref="IFreshInjectablePlugin{T}"/>), die dem Handler einen frischen, scope-eigenen DbContext gibt
    /// statt des Kontexts, der unter Blazor am Circuit haengt. Der Vergleich geht diesen Weg laengst: er kommt
    /// ueber den Datei-Dispatch (<c>DefaultFileServiceHandler</c>), und der oeffnet seit jeher eine
    /// Operations-Scope. Ohne diese Zeile bliebe genau der schreibende Weg der einzige, der es nicht taete.
    ///
    /// Ob dabei wirklich ein frischer Context herauskommt, entscheidet der Wirt: die Abhaengigkeit des
    /// Handlers muss als scope-besessen verdrahtet sein (<c>AddDependency(..., disposeWithScope: true)</c>).
    /// Ist sie es nicht, reicht die Scope die ambiente Instanz durch - dann ist es so gut wie vorher, nicht
    /// schlechter.
    /// </remarks>
    public string? ApplyConfigChanges(ClaimsPrincipal user, IEnumerable<Change> changes)
    {
        if (!HasPermission(ViewPermission)) return "Not authorized to apply configuration changes.";

        var messages = new StringBuilder();
        var applied = false;

        // Nur wenn der Handler ueberhaupt aus dem Plugin-System kommt - ein direkt registrierter waere
        // ueber eine Lade-Scope nicht zu holen.
        if (services.GetService<IInjectablePlugin<IConfigurationHandler>>() != null
            && services.GetService<IFreshInjectablePlugin<IConfigurationHandler>>() is { } fresh)
        {
            try
            {
                using var lease = fresh.Lease(ConfigHandlerName);
                // Ab hier gilt der Vorgang als gelaufen: scheitert spaeter das Schliessen der Scope, darf
                // das NICHT in den zweiten Weg fallen und die Changes ein zweites Mal einspielen.
                applied = true;
                RunApply(lease.Value, changes, messages);
            }
            catch (Exception ex)
            {
                if (applied)
                {
                    // Das Einspielen selbst ist durch (RunApply faengt seine Fehler). Hier kann nur das
                    // Schliessen der Lade-Scope gescheitert sein - gemeldet, aber kein zweiter Versuch:
                    // der wuerde die Changes ein zweites Mal einspielen.
                    LogEnvironment.LogEvent(
                        $"The loading-scope of the configuration handler could not be closed after applying: "
                        + $"{ex.OutlineException()}", LogSeverity.Warning);
                }
                else
                {
                    // Der frische Weg ist der richtige, aber er darf das Einspielen nicht verhindern.
                    LogEnvironment.LogEvent(
                        $"The configuration handler could not be leased in its own scope; falling back to the "
                        + $"ambient instance (which shares the caller's database-context): {ex.OutlineException()}",
                        LogSeverity.Warning);
                }
            }
        }

        if (!applied)
        {
            var handler = ResolveConfigurationHandler();
            if (handler == null) return "No configuration handler is registered on this host.";

            RunApply(handler, changes, messages);
        }

        return messages.Length != 0 ? messages.ToString() : null;
    }

    private static void RunApply(IConfigurationHandler handler, IEnumerable<Change> changes, StringBuilder messages)
    {
        try
        {
            handler.ApplyChanges(changes, messages);
        }
        catch (Exception ex)
        {
            // A failed apply must not tear down the circuit — surface the reason to the reviewer.
            messages.AppendLine(ex.Message);
            LogEnvironment.LogEvent($"Applying a system-configuration failed: {ex.OutlineException()}",
                LogSeverity.Error);
        }
    }

    /// <summary>Der explizit konfigurierte Handler-Name, oder null fuer den Standard.</summary>
    private string? ConfigHandlerName
    {
        get
        {
            var name = services.GetService<IHierarchySettings<AssemblyDiagnosticsOptions>>()?.Value.ConfigHandlerName;
            return string.IsNullOrEmpty(name) ? null : name;
        }
    }

    /// <summary>
    /// Resolves the IConfigurationHandler the same way the MVC controller does: prefer the plugin-injection
    /// wrapper (optionally a named instance via <c>ConfigHandlerName</c>), fall back to a directly registered
    /// handler (the one ConfigFileHandler itself consumes).
    /// </summary>
    /// <remarks>
    /// Der LESENDE Weg. Wer schreibt, nimmt die frische Lade-Scope - siehe <see cref="ApplyConfigChanges"/>.
    /// </remarks>
    private IConfigurationHandler? ResolveConfigurationHandler()
    {
        var name = ConfigHandlerName;
        var plugin = services.GetService<IInjectablePlugin<IConfigurationHandler>>();
        if (plugin != null)
        {
            return name == null ? plugin.Instance : plugin.GetInstance(name);
        }

        return services.GetService<IConfigurationHandler>();
    }
}
