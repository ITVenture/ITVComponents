using System.Globalization;
using System.Resources;
using ITVComponents.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity.Resources
{
    /// <summary>
    /// The texts of <see cref="TenantPayoutProfileModule{TContext}"/>, in the reader's language.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain <see cref="ResourceManager"/> rather than an <c>IStringLocalizer</c>: the module is created as a
    /// plugin, whose constructor the host declares, and a localizer from DI would change that declaration.
    /// The resource manager follows <see cref="CultureInfo.CurrentUICulture"/> with the same fallback
    /// (<c>fr-CH</c> → <c>fr</c> → neutral, which is English), and it does not depend on whether the host
    /// configured a <c>ResourcesPath</c>.
    /// </para>
    /// <para>
    /// The texts are handed to the onboarding renderer as plain text, which its JSON translation passes through
    /// unchanged. They are read per call, in the circuit of the user, so the culture is that user's.
    /// </para>
    /// </remarks>
    internal static class PayoutProfileMessages
    {
        private static readonly ResourceManager Manager = new(typeof(PayoutProfileMessages));

        /// <summary>The text for <paramref name="key"/> in the current UI culture.</summary>
        /// <param name="key">the resource key</param>
        /// <returns>the text; the key itself when the resource is missing (and that is logged)</returns>
        public static string Text(string key)
        {
            var retVal = Manager.GetString(key, CultureInfo.CurrentUICulture);
            if (retVal == null)
            {
                LogEnvironment.LogEvent($"The payout profile text '{key}' is missing from the resources; the key is shown instead.",
                    LogSeverity.Error, "TenantPayments");
                return key;
            }

            return retVal;
        }
    }
}
