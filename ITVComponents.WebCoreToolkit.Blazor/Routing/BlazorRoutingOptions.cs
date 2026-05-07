using System.Reflection;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;

namespace ITVComponents.WebCoreToolkit.Blazor.Routing;

/// <summary>
/// Aggregates assemblies that should be discoverable by the Blazor &lt;Router&gt; for
/// routable components. Each WebCoreToolkit-Blazor library that exposes pages adds
/// its own assembly here at startup; the host reads <see cref="AdditionalAssemblies"/>
/// in its Routes/App component.
/// Per-type exclusions are supported via <see cref="TypeFilters"/>: when the host
/// renders a routed page through a <c>FilteredRouteView</c>, the resolved page type
/// is checked against the filter and short-circuited to NotFound if marked Ignore.
/// </summary>
public class BlazorRoutingOptions
{
    public List<Assembly> AdditionalAssemblies { get; } = new();

    /// <summary>
    /// Per-assembly type-loading behavior. Mirrors the MVC <c>AssemblyPartWithGenerics</c>
    /// blacklist mechanism so the same JSON config can be reused.
    /// </summary>
    public Dictionary<Assembly, AssemblyPartTypeLoadBehaviorOptions> TypeFilters { get; } = new();

    public void AddAssembly(Assembly assembly)
    {
        if (!AdditionalAssemblies.Contains(assembly))
        {
            AdditionalAssemblies.Add(assembly);
        }
    }

    public void AddAssembly(Assembly assembly, AssemblyPartTypeLoadBehaviorOptions? filter)
    {
        AddAssembly(assembly);
        if (filter is not null)
        {
            TypeFilters[assembly] = filter;
        }
    }

    /// <summary>
    /// Returns true if the given component type may be rendered. The default behavior
    /// when no filter is registered for the type's assembly is to allow rendering.
    /// </summary>
    public bool ShouldRenderType(Type? type)
    {
        if (type is null) return true;
        var asm = type.Assembly;
        if (!TypeFilters.TryGetValue(asm, out var filter) || filter is null) return true;

        var custom = filter.CustomLoadings?.FirstOrDefault(c => c.Type == type);
        var behavior = custom is not null && custom.LoadBehavior != TypeRegisterBehavior.Default
            ? custom.LoadBehavior
            : filter.DefaultBehavior;
        return behavior != TypeRegisterBehavior.Ignore;
    }
}
