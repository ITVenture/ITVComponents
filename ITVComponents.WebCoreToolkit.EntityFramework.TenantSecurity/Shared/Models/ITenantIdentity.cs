namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    /// <summary>
    /// Das Minimum, das eine Mandanten-Entitaet ausmacht: ein Name und die Id dahinter.
    /// </summary>
    /// <remarks>
    /// Gebraucht von allem, was einen Mandanten nur <b>nachschlagen</b> muss, ohne seine Ausbaustufe zu
    /// kennen - allen voran der <c>SetCurrentTenantInterceptor</c>. Die vier Ausbaustufen (flach und
    /// hierarchisch, jeweils als vollstaendiges Modell und als schlanke Binder-Fassung) unterscheiden
    /// sich fuer diesen Zweck in nichts.
    /// <para>
    /// Bewusst nur lesend deklariert: wer diesen Vertrag benutzt, schlaegt nach - er legt keine
    /// Mandanten an. Die Entitaeten selbst haben ihre Setter weiterhin.
    /// </para>
    /// </remarks>
    public interface ITenantIdentity
    {
        /// <summary>Der Primaerschluessel des Mandanten.</summary>
        int TenantId { get; }

        /// <summary>Der eindeutige (technische) Name des Mandanten.</summary>
        string TenantName { get; }
    }
}
