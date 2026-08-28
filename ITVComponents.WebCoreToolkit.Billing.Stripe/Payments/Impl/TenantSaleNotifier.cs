using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl
{
    /// <summary>
    /// Calls the host's sale observers. Every observer is called even when an earlier one threw: the money has
    /// already moved, so one shop's bookkeeping failing must not stop the next one from learning about it.
    /// <para>
    /// The failure is logged, never swallowed. An observer that quietly does nothing is precisely the case where
    /// an order stays unfulfilled after a successful payment, and the only visible symptom is a customer asking
    /// where their goods are.
    /// </para>
    /// </summary>
    internal sealed class TenantSaleNotifier
    {
        private readonly IEnumerable<ITenantSaleObserver> observers;

        public TenantSaleNotifier(IEnumerable<ITenantSaleObserver> observers)
        {
            this.observers = observers;
        }

        public async Task NotifyCompletedAsync(TenantSale sale, CancellationToken cancellationToken)
        {
            foreach (var observer in observers)
            {
                try
                {
                    await observer.OnSaleCompletedAsync(sale, cancellationToken);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"Sale observer {observer.GetType().FullName} failed for the paid sale {sale.TenantSaleId} (tenant {sale.TenantId}, reference '{sale.ExternalReference}'). The payment stands; whatever this observer was to release did NOT happen: {ex.OutlineException()}",
                        LogSeverity.Error, "StripeConnect");
                }
            }
        }

        public async Task NotifyRefundedAsync(TenantSale sale, TenantSaleRefund refund, CancellationToken cancellationToken)
        {
            foreach (var observer in observers)
            {
                try
                {
                    await observer.OnSaleRefundedAsync(sale, refund, cancellationToken);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"Sale observer {observer.GetType().FullName} failed for the refund {refund.TenantSaleRefundId} of sale {sale.TenantSaleId} (tenant {sale.TenantId}). The refund stands; this observer did not react: {ex.OutlineException()}",
                        LogSeverity.Error, "StripeConnect");
                }
            }
        }
    }
}
