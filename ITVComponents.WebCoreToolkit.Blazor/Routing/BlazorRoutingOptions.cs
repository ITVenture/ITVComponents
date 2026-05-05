using System.Reflection;

namespace ITVComponents.WebCoreToolkit.Blazor.Routing;

/// <summary>
/// Aggregates assemblies that should be discoverable by the Blazor &lt;Router&gt; for
/// routable components. Each WebCoreToolkit-Blazor library that exposes pages adds
/// its own assembly here at startup; the host reads <see cref="AdditionalAssemblies"/>
/// in its Routes/App component.
/// </summary>
public class BlazorRoutingOptions
{
    public List<Assembly> AdditionalAssemblies { get; } = new();

    public void AddAssembly(Assembly assembly)
    {
        if (!AdditionalAssemblies.Contains(assembly))
        {
            AdditionalAssemblies.Add(assembly);
        }
    }
}
