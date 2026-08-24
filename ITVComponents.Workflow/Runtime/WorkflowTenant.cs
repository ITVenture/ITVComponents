using System;

namespace ITVComponents.Workflow.Runtime
{
    /// <summary>
    /// Die eine Schreibweise, in der ein Mandantenname in der Workflow-Ablage steht.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Mandant kommt im Web-Betrieb aus dem <c>IPermissionScope</c>, und der holt ihn aus der Route
    /// bzw. der Konfiguration - also in der Schreibweise, die dort zufaellig steht. Ohne eine gemeinsame
    /// Normalisierung entstehen zwei Wahrheiten in DERSELBEN Spalte: der Start ueber die Monitor-Ansicht
    /// schrieb klein (die Handler riefen <c>ToLower()</c>), der gewoehnliche Web-Weg in der
    /// Originalschreibweise. Unter SQL Server deckt die Collation das zu, unter PostgreSQL nicht - dort
    /// sieht ein Mandant seine eigenen Vorgaenge dann nicht mehr, und ein Vorgang, den der Filter
    /// verschluckt, bleibt schlicht stehen.
    /// </para>
    /// <para>
    /// <b>Bewusst <see cref="string.ToLowerInvariant"/> und nicht <c>ToLower()</c>:</b> unter
    /// tuerkischer Kultur macht letzteres aus "I" ein "ı", und ein Mandantenname haenge dann davon ab,
    /// welche Kultur der Prozess gerade traegt.
    /// </para>
    /// <para>
    /// Angewendet wird das dort, wo der Mandant aus dem <b>Web-Kontext</b> eintritt - NICHT auf Werte,
    /// die aus der Ablage kommen (<c>instance.TenantId</c> und Ihresgleichen). Die sind, was sie sind:
    /// wuerde man sie beim Lesen normalisieren, faende der Filter eine alte, gemischt geschriebene Zeile
    /// nicht mehr wieder.
    /// </para>
    /// </remarks>
    public static class WorkflowTenant
    {
        /// <summary>
        /// Bringt einen aus dem Web-Kontext stammenden Mandantennamen in die Ablage-Schreibweise.
        /// </summary>
        /// <param name="tenantId">der Name, oder null</param>
        /// <returns>
        /// der klein geschriebene Name; null bei null oder leer - "kein Mandant" und "der Mandant mit dem
        /// leeren Namen" duerfen nicht zwei verschiedene Dinge sein.
        /// </returns>
        public static string Normalize(string tenantId)
            => string.IsNullOrEmpty(tenantId) ? null : tenantId.ToLowerInvariant();
    }
}
