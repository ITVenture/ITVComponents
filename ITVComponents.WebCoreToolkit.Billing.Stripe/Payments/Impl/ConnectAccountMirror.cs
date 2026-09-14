using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
// Siehe TenantPaymentAccountService: eine using-Direktive importiert die TYPEN eines Namespace, nicht seine
// verschachtelten - "V2.Core.X" braucht deshalb den Alias.
using V2 = Stripe.V2;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl
{
    /// <summary>
    /// Uebersetzt, was der Anbieter ueber ein verbundenes Konto sagt, in den lokalen Spiegel.
    /// <para>
    /// Eigene Klasse und nicht Teil des Dienstes: die Abbildung braucht weder Kontext noch Client noch
    /// Einstellungen - sie ist eine reine Funktion vom v2-Konto auf die gespiegelte Zeile. Als statisches
    /// Mitglied einer generischen Klasse war sie nur ueber einen Typparameter erreichbar, der sie nichts
    /// angeht, und aus demselben Grund nicht fuer sich pruefbar. Und geprueft gehoert sie: an
    /// <see cref="TenantPaymentAccount.ChargesEnabled"/> haengt, ob ein Laden Geld annehmen darf.
    /// </para>
    /// </summary>
    internal static class ConnectAccountMirror
    {
        /// <summary>
        /// Copies the provider's view of the account onto the local mirror.
        /// <para>
        /// The two booleans are the reason this method matters: they decide whether a shop may take money, and
        /// v2 no longer answers that with a flag. A capability reads <c>active</c>, <c>pending</c>,
        /// <c>restricted</c> or <c>unsupported</c>, and only the first is a yes - the other three are all "not
        /// yet", however differently they read to a human. The raw status is mirrored alongside so the view can
        /// tell "we are checking" from "we need something from you"; the boolean must not try to.
        /// </para>
        /// </summary>
        internal static void Apply(TenantPaymentAccount account, V2.Core.Account remote)
        {
            var merchant = remote.Configuration?.Merchant;
            var recipient = remote.Configuration?.Recipient;

            account.CardPaymentsStatus = merchant?.Capabilities?.CardPayments?.Status;
            account.PayoutsStatus = recipient?.Capabilities?.StripeBalance?.Payouts?.Status;

            // A deactivated configuration is a no regardless of what its capability last said.
            account.ChargesEnabled = merchant?.Applied == true && IsActive(account.CardPaymentsStatus);
            account.PayoutsEnabled = recipient?.Applied == true && IsActive(account.PayoutsStatus);

            var entries = remote.Requirements?.Entries ?? new List<V2.Core.AccountRequirementsEntry>();

            // v2 has no "details submitted". The honest equivalent is that the provider is not waiting on the
            // TENANT for anything - what it is still checking itself does not belong to the tenant any more.
            account.DetailsSubmitted = !entries.Any(e => IsAwaitingUser(e));
            // The deadline is a pair, not a date: the moment plus the strictest status of everything outstanding.
            // Only the moment is mirrored; the status of an individual requirement already rides along per entry.
            account.RequirementsDeadline = remote.Requirements?.Summary?.MinimumDeadline?.Time;

            // Nor a "disabled reason". v2 reports per capability why it is not active, and the one that blocks
            // selling is the one worth showing - a payout problem reads very differently from a card one.
            account.DisabledReason = FirstBlockingReason(merchant, recipient);

            if (!string.IsNullOrEmpty(remote.Identity?.Country))
            {
                account.Country = remote.Identity.Country.ToUpperInvariant();
            }

            if (!string.IsNullOrEmpty(remote.Defaults?.Currency))
            {
                account.DefaultCurrency = remote.Defaults.Currency.ToUpperInvariant();
            }

            if (!string.IsNullOrEmpty(remote.Dashboard))
            {
                account.DashboardType = remote.Dashboard;
            }

            account.RequirementsJson = SerializeRequirements(entries, account.RequirementsDeadline);
            account.Updated = DateTime.UtcNow;
        }

        /// <summary>Only <c>active</c> is a yes. Everything else - including null - is not yet.</summary>
        internal static bool IsActive(string? status)
            => string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether this requirement is one the TENANT has to act on, as opposed to the provider.</summary>
        internal static bool IsAwaitingUser(V2.Core.AccountRequirementsEntry entry)
            => string.Equals(entry.AwaitingActionFrom, "user", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The code of the capability that is standing in the way, card payments first: without those there is no
        /// sale at all, and a payout problem on top of that is the smaller of the two worries.
        /// </summary>
        internal static string? FirstBlockingReason(V2.Core.AccountConfigurationMerchant? merchant,
            V2.Core.AccountConfigurationRecipient? recipient)
        {
            var card = merchant?.Capabilities?.CardPayments;
            if (card != null && !IsActive(card.Status))
            {
                return card.StatusDetails?.FirstOrDefault()?.Code ?? card.Status;
            }

            var payouts = recipient?.Capabilities?.StripeBalance?.Payouts;
            if (payouts != null && !IsActive(payouts.Status))
            {
                return payouts.StatusDetails?.FirstOrDefault()?.Code ?? payouts.Status;
            }

            return null;
        }

        /// <summary>
        /// The parts of the account the mirror lives on. v2 returns the bare identity unless they are asked for,
        /// so this has to accompany every read AND the creation - otherwise the first mirror is empty and the
        /// account looks unusable until something refreshes it.
        /// </summary>
        internal static readonly List<string> MirroredSections = new()
        {
            "configuration.merchant", "configuration.recipient", "defaults", "identity", "requirements"
        };

        /// <summary>
        /// Mirrors the requirement lists as raw JSON. Deliberately not modelled: the shape belongs to the
        /// provider and changes without notice, and everything we do with it is show it.
        /// </summary>
        internal static string? SerializeRequirements(List<V2.Core.AccountRequirementsEntry> entries,
            DateTime? deadline)
        {
            if (entries.Count == 0)
            {
                return null;
            }

            // The stored shape stays what it was, so the views keep reading it unchanged. v2 delivers one flat
            // list instead of four; the buckets come back from each entry's deadline status, which carries
            // exactly the three v1 names, and "pending verification" is what the provider is checking itself.
            return JsonSerializer.Serialize(new
            {
                currentlyDue = Named(entries, "currently_due"),
                pastDue = Named(entries, "past_due"),
                eventuallyDue = Named(entries, "eventually_due"),
                pendingVerification = entries.Where(e => !IsAwaitingUser(e)).Select(Describe).ToList(),
                // The date is NOT on the entry - an entry only says which bucket its impact falls into. The
                // soonest actual deadline is reported once, for the account as a whole.
                currentDeadline = deadline
            });
        }

        /// <summary>The requirements whose impact currently falls into the named bucket.</summary>
        internal static List<string> Named(List<V2.Core.AccountRequirementsEntry> entries, string deadlineStatus)
            => entries
                .Where(e => IsAwaitingUser(e)
                            && string.Equals(e.MinimumDeadline?.Status, deadlineStatus, StringComparison.OrdinalIgnoreCase))
                .Select(Describe)
                .ToList();

        /// <summary>
        /// The machine-readable name of a requirement. Deliberately that and not a sentence: the view translates
        /// these, and a provider sentence would arrive in whatever language the provider felt like.
        /// </summary>
        internal static string Describe(V2.Core.AccountRequirementsEntry entry)
            => entry.Description ?? entry.AwaitingActionFrom ?? "unknown";
    }
}
