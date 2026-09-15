namespace ITVComponents.WebCoreToolkit.Security.ClientApps
{
    /// <summary>
    /// Die Angaben zu einem gueltigen Zugang, so weit der Anmelde-Rand sie braucht.
    /// </summary>
    /// <param name="ClientAppAccessId">technischer Schluessel des Zugangs</param>
    /// <param name="Label">
    /// der systemweit eindeutige Bezeichner. <b>Er wird als <c>ClaimTypes.Name</c> gesetzt</b> und reist
    /// von dort als <c>##APPUSER##&lt;Label&gt;#</c> in die Rechteaufloesung - er ist also nicht bloss
    /// eine Anzeige, sondern die Identitaet.
    /// </param>
    /// <param name="SecretHash">
    /// der gespeicherte Hash. Der Aufrufer prueft dagegen; die Datenbankschicht bekommt den Klartext nie
    /// zu sehen.
    /// </param>
    /// <param name="TenantName">
    /// der Mandant der Anwendung. Er wird als <c>ClaimTypes.FixedUserScope</c> gesetzt - <b>ohne ihn
    /// laeuft das angemeldete Geraet mandantenlos</b>, und genau das war der Zustand des API-Key-Pfads,
    /// bevor es diese Abstraktion gab.
    /// </param>
    /// <param name="IsMachine">
    /// true, wenn der Zugang an keinem Benutzer haengt. Nur zur Nachvollziehbarkeit im Protokoll - die
    /// Rechte entscheidet ohnehin die Aufloesung.
    /// </param>
    public sealed record ClientAppAccessInfo(
        int ClientAppAccessId,
        string Label,
        string SecretHash,
        string TenantName,
        bool IsMachine);
}
