using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions
{
    /// <summary>
    /// The way back to the host's shop. Without it nobody ever learns that a sale was paid — the payment happens
    /// on the provider's hosted page and only the webhook sees the outcome.
    /// <para>
    /// Registered as <c>IEnumerable&lt;ITenantSaleObserver&gt;</c>; every registered observer is called. Called
    /// ONLY on a real status change (<c>Pending -&gt; Paid</c>): the provider delivers at-least-once, and an
    /// order must not be released twice. If an observer throws, the exception is logged and the next observer
    /// still runs — the sale stays paid either way, because the money has moved.
    /// </para>
    /// </summary>
    public interface ITenantSaleObserver
    {
        /// <summary>The end customer paid. The sale row is already persisted as <c>Paid</c>.</summary>
        Task OnSaleCompletedAsync(TenantSale sale, CancellationToken cancellationToken = default);

        /// <summary>A refund was booked against the sale. The sale's status already reflects the refund total.</summary>
        Task OnSaleRefundedAsync(TenantSale sale, TenantSaleRefund refund, CancellationToken cancellationToken = default);
    }
}
