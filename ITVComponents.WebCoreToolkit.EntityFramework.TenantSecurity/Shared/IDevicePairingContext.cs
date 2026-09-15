using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared
{
    /// <summary>
    /// Der Zugang zu den laufenden Kopplungsvorgaengen.
    /// </summary>
    /// <remarks>
    /// <b>Bewusst eine eigene, schmale Schnittstelle statt eines 46. Typparameters an
    /// <c>ISecurityContext</c>.</b> Zwei Gruende:
    /// <list type="number">
    /// <item>Niemand ausser dem Kopplungsdienst braucht diese Tabelle - weder die Rechteaufloesung noch
    /// der Konfigurations-Austausch noch die Mandanten-Vorlagen fuehren sie.</item>
    /// <item>Ein Mandantenfilter waere hier sogar <b>falsch</b>: <c>start</c> und <c>poll</c> laufen
    /// anonym und ohne Mandantenkontext - ein globaler Filter wuerde dem Geraet seinen eigenen Vorgang
    /// verbergen. Die Mandantengrenze zieht der Dienst ausdruecklich: beim Bestaetigen ueber den
    /// Mandanten des Benutzers, beim Abholen ueber den Geraetecode, der selbst das Geheimnis ist.</item>
    /// </list>
    /// </remarks>
    /// <typeparam name="TDevicePairing">die Kopplungs-Auspraegung des Modells</typeparam>
    public interface IDevicePairingContext<TDevicePairing>
        where TDevicePairing : class
    {
        DbSet<TDevicePairing> DevicePairings { get; set; }
    }
}
