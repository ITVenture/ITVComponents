using System;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks.Configuration;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.Handlers;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Options;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks.Handlers;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks.Handlers.Impl;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Extensions
{
    /// <summary>
    /// DI-Registrierung des Workflow-View-Moduls.
    /// </summary>
    public static class DependencyInjectionExtensions
    {
        /// <summary>
        /// Registriert die Routing-Assembly und die Handler.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Die Handler ziehen ihren <c>WorkflowContext</c> pro Operation frisch ueber
        /// <c>IFreshInjectablePlugin&lt;WorkflowContext&gt;</c> (Blazor-/tenant-sicher). Der Host muss daher:
        /// </para>
        /// <list type="number">
        ///   <item><c>UseInjectablePlugins(...)</c> aufrufen (registriert
        ///   <c>IFreshInjectablePlugin&lt;&gt;</c>).</item>
        ///   <item><c>WorkflowContext</c> als scope-owned Dependency registrieren
        ///   (<c>FactoryOptions.AddDependency(name, delegate, disposeWithScope: true)</c>) - global (Delegate
        ///   liefert <c>IDbContextFactory&lt;WorkflowContext&gt;.CreateDbContext()</c>) ODER tenant-faehig
        ///   (Toolkit-Konvention). WOHER der Kontext kommt, ist reine Host-Registrierung; die Views kennen
        ///   nur den einen Seam.</item>
        ///   <item>Fuer Signal-/Abbruch-Operationen zusaetzlich eine <c>WorkflowEngineFactory</c>
        ///   registrieren (baut eine Engine ueber den frischen Store; kapselt die Engine-Konfiguration).</item>
        /// </list>
        /// <para>
        /// Ueber <paramref name="options"/> laesst sich die Signal-Zustellung waehlen (inline vs.
        /// store-only/Runner - siehe <see cref="WorkflowViewsOptions.SignalDelivery"/>). Inline-Ausfuehrung
        /// setzt den globalen Ein-Kontext-Betrieb voraus; fuer Multi-Tenant
        /// <see cref="WorkflowSignalDelivery.Runner"/> + einen tenant-uebergreifenden Runner nutzen.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddWorkflowViews(this IServiceCollection services,
            AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior, WorkflowViewsOptions? options = null)
        {
            partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions
            {
                DefaultBehavior = TypeRegisterBehavior.Use
            };

            services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly, partTypeLoadBehavior);

            if (partTypeLoadBehavior.ShouldLoadType(typeof(WorkflowMonitorHandler)))
            {
                // Signal-Zustellung nach Konfiguration: inline (Web-Only, Standard) vs. store-only, dann
                // treibt ein (Backend-)Runner voran (getrennte Deployments / Multi-Tenant).
                if (options?.SignalDelivery == WorkflowSignalDelivery.Runner)
                {
                    services.AddScoped<IWorkflowMonitorHandler, SplitWorkflowMonitorHandler>();
                }
                else
                {
                    services.AddScoped<IWorkflowMonitorHandler, WorkflowMonitorHandler>();
                }
            }

            if (partTypeLoadBehavior.ShouldLoadType(typeof(WorkflowDesignHandler)))
            {
                services.AddScoped<IWorkflowDesignHandler, WorkflowDesignHandler>();
            }

            if (partTypeLoadBehavior.ShouldLoadType(typeof(WorkflowTaskHandler)))
            {
                // Dieselbe Unterscheidung wie bei der Signal-Zustellung: der Abschluss einer Aufgabe ist
                // immer store-only, nur das Weiterlaufen des Zweigs passiert inline oder im Runner.
                if (options?.SignalDelivery == WorkflowSignalDelivery.Runner)
                {
                    services.AddScoped<IWorkflowTaskHandler, SplitWorkflowTaskHandler>();
                }
                else
                {
                    services.AddScoped<IWorkflowTaskHandler, WorkflowTaskHandler>();
                }
            }

            return services;
        }

        /// <summary>
        /// Registriert die Masken eigener Aufgaben-Arten: <c>ViewKey</c> bzw. <c>TaskKey</c> aus der
        /// Definition auf eine Blazor-Komponente. Ohne Registrierung baut die Oberflaeche die generische
        /// Maske aus der Feld-Deklaration des Knotens - der einfache Fall braucht also gar nichts.
        /// </summary>
        /// <example>
        /// <code>
        /// services.ConfigureWorkflowTaskViews(cfg => cfg.RegisterTaskView&lt;ApproveInvoice&gt;("ApproveInvoice"));
        /// </code>
        /// Die Komponente liest ihren Zustand ueber
        /// <c>[CascadingParameter] WorkflowTaskContext TaskContext</c> und schliesst mit
        /// <c>TaskContext.CompleteAsync(...)</c> ab.
        /// </example>
        public static IServiceCollection ConfigureWorkflowTaskViews(this IServiceCollection services,
            Action<WorkflowTaskViewConfiguration> configure)
        {
            return services.Configure(configure);
        }
    }
}
