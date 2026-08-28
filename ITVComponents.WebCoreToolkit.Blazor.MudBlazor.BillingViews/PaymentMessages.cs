namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor
{
    /// <summary>
    /// Marker type for the payment-views string resources (axis B). Components inject
    /// <c>IStringLocalizer&lt;PaymentMessages&gt;</c>; the toolkit's localizer factory resolves the embedded resx
    /// set (<c>Resources/PaymentMessages.{culture}.resx</c>, neutral = English) plus DB overrides.
    /// <para>
    /// A set of its own next to <see cref="BillingMessages"/>, not an extension of it: the two axes are shipped
    /// independently, and a host that never runs shops should not have to translate their wording.
    /// </para>
    /// <para>
    /// The class lives in the ROOT namespace of the assembly, which differs from the assembly name. The
    /// <c>[assembly: RootNamespace]</c> attribute that makes the localizer find the resx is declared once, next
    /// to <see cref="BillingMessages"/> — a second one here would not compile.
    /// </para>
    /// </summary>
    public sealed class PaymentMessages
    {
    }
}
