using System;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Refusal of a payments operation, carrying a resource KEY rather than a finished sentence.
    /// <para>
    /// The tenant reads these in the user interface, in its own language — so the service names the reason and
    /// the view translates it. A German string thrown from the service layer would be untranslatable by the time
    /// it reaches the page.
    /// </para>
    /// Every precondition gets its OWN code. A single "not possible" would be worthless in support, which is
    /// exactly the situation these codes exist to avoid.
    /// </summary>
    public class TenantPaymentException : Exception
    {
        public TenantPaymentException(string code, string message) : base(message)
        {
            Code = code;
        }

        public TenantPaymentException(string code, string message, Exception innerException) : base(message, innerException)
        {
            Code = code;
        }

        /// <summary>Resource key of the reason; see <see cref="PaymentErrorCodes"/>.</summary>
        public string Code { get; }
    }

    /// <summary>Reasons a payments operation can be refused. Each is a resource key in the payment views.</summary>
    public static class PaymentErrorCodes
    {
        /// <summary>The module is switched off for the whole deployment.</summary>
        public const string Disabled = "Payments_Error_Disabled";

        /// <summary>The tenant does not hold the payments feature.</summary>
        public const string FeatureMissing = "Payments_Error_FeatureMissing";

        /// <summary>No connected account exists for the tenant yet.</summary>
        public const string NoAccount = "Payments_Error_NoAccount";

        /// <summary>
        /// The tenant has not supplied what the provider needs before an account can be created at all - country,
        /// contact address, legal form. Its own code because it is the only one of these the tenant can fix
        /// alone, and the view sends them to the right tab instead of showing a provider message about an API.
        /// </summary>
        public const string ProfileIncomplete = "Payments_Error_ProfileIncomplete";

        /// <summary>The connected account may not accept payments (yet).</summary>
        public const string ChargesDisabled = "Payments_Error_ChargesDisabled";

        /// <summary>Payouts are not enabled and the configuration insists on them before selling.</summary>
        public const string PayoutsDisabled = "Payments_Error_PayoutsDisabled";

        /// <summary>The tenant severed the connection between platform and account.</summary>
        public const string Disconnected = "Payments_Error_Disconnected";

        /// <summary>Amount is zero or negative, or the currency is missing.</summary>
        public const string InvalidAmount = "Payments_Error_InvalidAmount";

        /// <summary>No such sale.</summary>
        public const string SaleNotFound = "Payments_Error_SaleNotFound";

        /// <summary>The sale was never paid, so there is nothing to refund.</summary>
        public const string SaleNotRefundable = "Payments_Error_SaleNotRefundable";

        /// <summary>The requested refund exceeds what is left of the sale.</summary>
        public const string RefundExceedsAmount = "Payments_Error_RefundExceedsAmount";

        /// <summary>The sale has no charge on record — the payment never reached that stage.</summary>
        public const string MissingCharge = "Payments_Error_MissingCharge";

        /// <summary>The provider refused the call. The provider's own message travels in the exception message.</summary>
        public const string ProviderError = "Payments_Error_ProviderError";

        /// <summary>A configured charge type other than direct charges, which is not implemented.</summary>
        public const string UnsupportedChargeType = "Payments_Error_UnsupportedChargeType";

        /// <summary>
        /// The configured combination of dashboard and liability is one the provider only serves on a preview
        /// API version, which this SDK cannot be asked for. Its own code because the remedy is a configuration
        /// decision with money attached - not something a retry or a support ticket fixes.
        /// </summary>
        public const string UnsupportedAccountConfiguration = "Payments_Error_UnsupportedAccountConfiguration";
    }
}
