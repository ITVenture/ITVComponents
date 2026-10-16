using ITVComponents.WebCoreToolkit.Billing.SumUp.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;

namespace ITVComponents.WebCoreToolkit.Billing.SumUp.Extensions
{
    /// <summary>
    /// Verdrahtet SumUp.
    /// </summary>
    public static class SumUpDependencyInjectionExtensions
    {
        /// <summary>Der Name, unter dem dieser Anbieter angesprochen wird.</summary>
        public const string ProviderKey = SumUpAccess.Key;

        /// <summary>
        /// Registriert SumUp für Achse C (Kassieren an einem Solo über die Cloud API).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Kommt auch bei der Weiche der Online-Verkäufe an, weil Erstattungen dort laufen - mit
        /// <c>SupportsOnlineSales = false</c>, damit SumUp nie von selbst für eine Zahlungsseite gewählt wird.
        /// </para>
        /// <para>
        /// Der Host muss weiterhin beisteuern:
        /// </para>
        /// <list type="bullet">
        /// <item>einen <c>IProviderCredentialStore</c> (<c>AddFlatProviderCredentialStore</c> bzw.
        /// <c>AddHierarchyProviderCredentialStore</c>), denn SumUp läuft nur über den eigenen Zugang des Mandanten;</item>
        /// <item>das GlobalSetting <c>SumUpPayments</c> mit dem Affiliate Key der Plattform;</item>
        /// <item>den Endpunkt aus <see cref="SumUpEndpointExtensions.MapSumUpWebhook{TContext}"/> und dessen öffentliche
        /// Adresse als <c>SumUpPayments:WebhookUrl</c>.</item>
        /// </list>
        /// </remarks>
        public static IServiceCollection AddSumUpTerminals<TContext>(this IServiceCollection services)
            where TContext : DbContext, IPaymentsContext
        {
            services.AddHttpClient<SumUpApiClient>();
            services.AddScoped<SumUpTerminalPaymentService<TContext>>();
            services.AddScoped<ITerminalProviderAdapter>(sp => new TerminalProviderAdapter(
                SumUpTerminalPaymentService<TContext>.Key,
                sp.GetRequiredService<SumUpTerminalPaymentService<TContext>>()));
            services.AddScoped<SumUpWebhookHandler<TContext>>();
            // Der Assistent fragt bei SumUp nach dem Code, den der Solo anzeigt, statt nach einer Kennung.
            services.AddScoped<ITerminalPairing, SumUpTerminalPairing<TContext>>();
            services.TryAddEnumerable(ServiceDescriptor.Scoped<IOwnAccessProvider, SumUpOwnAccessProvider>());
            services.AddProviderAccessAdministration<TContext>();

            services.AddScoped<SumUpSaleService<TContext>>();
            services.AddScoped<IPaymentProviderAdapter>(sp => new PaymentProviderAdapter(ProviderKey,
                sp.GetRequiredService<SumUpSaleService<TContext>>(),
                new UnsupportedTenantPaymentAccountService(ProviderKey,
                    "SumUp has no platform account: every tenant connects its own SumUp account (own provider access)."),
                supportsOnlineSales: false));

            services.AddTenantPaymentRouting<TContext>();
            return services.AddTenantTerminalRouting<TContext>();
        }
    }
}
