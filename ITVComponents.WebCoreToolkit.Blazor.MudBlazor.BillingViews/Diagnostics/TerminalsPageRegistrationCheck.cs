using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Diagnostics
{
    /// <summary>
    /// Meldet beim Start, wenn die Terminal-Verwaltung geroutet ist, ihr Handler aber fehlt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die Seite liegt im selben Assembly wie die Abo- und Zahlungs-Ansichten. Wer eine davon registriert,
    /// macht auch <c>/Account/Manage/Payments/Terminals</c> erreichbar — ohne
    /// <c>AddMudBlazorTerminalViews</c> stürzt sie dann erst beim Öffnen ab.
    /// </para>
    /// <para>
    /// Geprüft wird erst am fertigen Container und nicht im WebPart: ein Host darf die Terminal-Ansichten
    /// auch im eigenen Code registrieren, und in welcher Reihenfolge das gegenüber den WebParts geschieht,
    /// weiss das WebPart nicht.
    /// </para>
    /// </remarks>
    internal sealed class TerminalsPageRegistrationCheck : IHostedService
    {
        private readonly IServiceProviderIsService isService;
        private readonly ILogger<TerminalsPageRegistrationCheck> logger;

        public TerminalsPageRegistrationCheck(IServiceProviderIsService isService, ILogger<TerminalsPageRegistrationCheck> logger)
        {
            this.isService = isService;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!isService.IsService(typeof(ITerminalsHandler)))
            {
                logger.LogWarning(
                    "The billing views are registered, which also routes /Account/Manage/Payments/Terminals, but no {Handler} is registered - the page fails when opened. Set PaymentViews:ActivateTerminalViews on the BillingViews web part or call AddMudBlazorTerminalViews in code.",
                    nameof(ITerminalsHandler));
            }

            if (!isService.IsService(typeof(IOwnAccessHandler)))
            {
                logger.LogWarning(
                    "The billing views are registered, which also routes /Account/Manage/Payments/OwnAccess, but no {Handler} is registered - the page fails when opened. It comes with AddMudBlazorPaymentViews or AddMudBlazorTerminalViews.",
                    nameof(IOwnAccessHandler));
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
