using ITVComponents.Workflow;
using ITVComponents.Workflow.Stores;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Runtime
{
    /// <summary>
    /// Baut eine <see cref="WorkflowEngine"/> ueber einen FRISCHEN, pro Operation aufgeloesten
    /// <paramref name="store"/>. Der Host registriert diese Factory und kapselt darin SEINE
    /// Engine-Konfiguration (der <c>IActivityHost</c>, die <c>hostTargets</c>, ein optionaler
    /// Ausdrucks-Auswerter) - die View-Handler besitzen die Store-/Kontext-Frische, der Host die
    /// Engine-Konfiguration.
    /// </summary>
    /// <remarks>
    /// Wichtig: Die Delegat-Implementierung MUSS den uebergebenen <paramref name="store"/> verwenden
    /// (nicht einen langlebigen Singleton-Store einfangen) - nur so laeuft jede Operation gegen einen
    /// frischen, Blazor-/tenant-sicheren Kontext. Beispiel-Registrierung:
    /// <code>
    /// services.AddSingleton&lt;WorkflowEngineFactory&gt;(sp => store =>
    ///     new WorkflowEngine(store, sp.GetRequiredService&lt;IActivityHost&gt;()));
    /// </code>
    /// </remarks>
    /// <param name="store">der frische, pro Operation gebaute Store, gegen den die Engine laufen soll</param>
    /// <returns>eine ueber <paramref name="store"/> konfigurierte Engine</returns>
    public delegate WorkflowEngine WorkflowEngineFactory(IWorkflowStore store);
}
