using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers.Impl;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Diagnostics;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Extensions
{
    public static class DependencyInjectionExtensions
    {
        /// <summary>
        /// Registers the MudBlazor billing views (routing assembly + <see cref="IBillingHandler"/>). The host
        /// must also wire the provider service layer (<c>AddStripeBilling&lt;TContext&gt;(config)</c>) and the
        /// feature provisioner (<c>AddBillingFeatureProvisioner&lt;...&gt;()</c>); the handler depends on both.
        /// </summary>
        public static IServiceCollection AddMudBlazorBillingViews<TContext>(this IServiceCollection services, AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior = null)
            where TContext : DbContext, IBillingContext, ITenantScopeContext
        {
            partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions { DefaultBehavior = TypeRegisterBehavior.Use };
            services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly, partTypeLoadBehavior);
            services.AddScoped<IBillingHandler, BillingHandler<TContext>>();
            services.AddTerminalsPageRegistrationCheck();
            return services;
        }

        /// <summary>
        /// Registers the MudBlazor payment views (axis B: the tenant's own end customers). A call of its OWN next
        /// to <see cref="AddMudBlazorBillingViews{TContext}"/>, so a host can use the payments services without
        /// these pages — precisely the "shop with its own front end" case.
        /// <para>
        /// The host must also wire the connect service layer (<c>AddStripePayments&lt;TContext&gt;()</c>) and the
        /// payment feature gate (<c>AddPaymentFeatureGate&lt;...&gt;()</c>); the handler depends on both.
        /// </para>
        /// </summary>
        /// <typeparam name="TContext">The host context carrying the payments tables and the active tenant.</typeparam>
        /// <typeparam name="TTenant">
        /// The tenant model AS MAPPED by the host — <c>Tenant</c> on flat hosts, <c>HierarchyTenant</c> on
        /// hierarchical ones. The platform overview reads tenant names through it; asking for the base type on a
        /// hierarchical host would throw, because only the derived type is in that model. The WebPart fills this
        /// automatically: <c>MethodHelper</c> binds type parameters BY NAME out of the security context, and
        /// <c>TTenant</c> is one of its own names.
        /// </typeparam>
        public static IServiceCollection AddMudBlazorPaymentViews<TContext, TTenant>(this IServiceCollection services, AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior = null)
            where TContext : DbContext, IPaymentsContext, ITenantScopeContext
            where TTenant : Tenant
        {
            partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions { DefaultBehavior = TypeRegisterBehavior.Use };
            // Same assembly as the billing views; the routing registration is idempotent for a host that wires
            // both.
            services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly, partTypeLoadBehavior);
            services.AddScoped<IPaymentsHandler, PaymentsHandler<TContext, TTenant>>();
            // Eigene Anbieter-Zugaenge gehoeren zum Online-Verkauf UND zu den Terminals - darum hier wie dort.
            services.TryAddScoped<IOwnAccessHandler, OwnAccessHandler<TContext>>();
            services.AddTerminalsPageRegistrationCheck();
            return services;
        }

        /// <summary>
        /// Registriert die Geräte-Verwaltung (Achse C): Liste, Assistent „Gerät hinzufügen" und die
        /// Auswahlliste der Client-Anwendungen.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Ein eigener Aufruf neben den Zahlungs-Ansichten, weil ein Betrieb Online-Verkäufe führen kann,
        /// ohne je ein Terminal zu besitzen — und umgekehrt.
        /// </para>
        /// <para>
        /// Der Host muss die Dienstschicht verdrahten (<c>AddStripeTerminals</c>,
        /// <c>AddPayrexxTerminals</c>, <c>AddWalleeTerminals</c> bzw. <c>AddAgentTerminals</c>) — ohne
        /// sie steht im Assistenten keine Auswahl.
        /// </para>
        /// <para>
        /// Die Auswahl <c>TerminalChoiceSources.RemoteObjects</c> („Geräte-Objekt auf dieser Kasse") füllt der
        /// mitgelieferte <see cref="Handlers.Impl.RemoteObjectTerminalChoiceProvider"/> über den
        /// <c>IRemoteObjectCatalog</c> des AgentServices-Webpakets. Ohne dieses Paket wird daraus ein
        /// Textfeld. <b>Ein eigener Anbieter des Hosts für diese Quelle gehört entfernt</b> — der zuerst
        /// registrierte gewinnt, und das ist dieser.
        /// </para>
        /// </remarks>
        /// <typeparam name="TContext">der Kontext mit den Zahlungstabellen und dem aktiven Mandanten</typeparam>
        /// <typeparam name="TClientApp">
        /// die Client-Anwendung, WIE DER HOST sie abgebildet hat. Das WebPart füllt den Parameter von
        /// selbst, sobald <c>PaymentViews:ActivateTerminalViews</c> gesetzt ist: es bindet Typparameter
        /// nach NAMEN aus dem Sicherheitskontext, und <c>TClientApp</c> ist einer von dessen eigenen
        /// Namen — dieselbe Mechanik wie bei <c>TTenant</c> oben.
        /// </typeparam>
        public static IServiceCollection AddMudBlazorTerminalViews<TContext, TClientApp>(
            this IServiceCollection services, AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior = null)
            where TContext : DbContext, IPaymentsContext, ITenantScopeContext
            where TClientApp : class
        {
            partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions { DefaultBehavior = TypeRegisterBehavior.Use };
            services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly, partTypeLoadBehavior);
            services.AddScoped<ITerminalsHandler, TerminalsHandler<TContext>>();
            services.TryAddScoped<IOwnAccessHandler, OwnAccessHandler<TContext>>();
            services.AddScoped<ITerminalChoiceProvider, ClientAppTerminalChoiceProvider<TContext, TClientApp>>();
            services.AddScoped<ITerminalChoiceProvider, RemoteObjectTerminalChoiceProvider>();
            return services;
        }

        /// <summary>
        /// Die Terminal-Seite liegt im selben Assembly wie die übrigen Ansichten und wird mit ihnen
        /// geroutet — fehlt ihr Handler, soll das beim Start im Log stehen und nicht erst beim Öffnen.
        /// </summary>
        private static void AddTerminalsPageRegistrationCheck(this IServiceCollection services)
            => services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TerminalsPageRegistrationCheck>());
    }
}
