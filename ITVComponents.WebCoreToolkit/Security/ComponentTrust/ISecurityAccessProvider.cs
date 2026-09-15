using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.ComponentTrust
{
    public interface ISecurityAccessProvider
    {
        IFullSecurityAccessHelper<TTrustConfig> CreateForCaller<TTrustConfig, T>(T trustingObject, TTrustConfig desiredTrust = null)
            where T : ITrustfulComponent<TTrustConfig> where TTrustConfig : class, ITrustConfig<TTrustConfig>, new();

        /// <summary>
        /// Loest jeden hinterlegten Vertrauens-Eintrag gegen die geladenen Assemblies auf und meldet die,
        /// die zur Laufzeit nie treffen werden.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Wofuer:</b> die Eintraege enthalten assembly-qualifizierte Typnamen, und darin steckt bei
        /// generischen Typen die <b>Stelligkeit</b>. Nimmt oder gibt eine neue Toolkit-Version dem
        /// Sicherheitskontext eine Entitaet, verschiebt sich diese Zahl bei jedem Typ, der den Kontext
        /// generisch fuehrt - und ein einmal geseedeter Eintrag passt nicht mehr. Der Bruch faellt
        /// <b>nirgends dort auf, wo man ihn erwartet</b>: der Build ist gruen (die Zahl steht in einer
        /// Datenbankzeile, nicht im Code), die Migration laeuft durch (sie prueft die Zeile nicht gegen die
        /// Assembly), und die Anmeldung gelingt - erst danach ist der Mandant leer.
        /// </para>
        /// <para>
        /// Deshalb gibt es diese Pruefung zum <b>bewussten Aufrufen</b>: aus einem Start-Check oder der
        /// Diagnose-Maske. Sie ist nicht billig (sie durchsucht im Fehlerfall geladene Assemblies) und
        /// gehoert darum nicht in einen Anfrage-Pfad.
        /// </para>
        /// <para>
        /// Die Vorbelegung meldet <see cref="TrustEntryValidationResult.NotSupported"/> - ein Provider, der
        /// seine Eintraege nicht aus einer Ablage bezieht, hat nichts zu pruefen.
        /// </para>
        /// </remarks>
        TrustEntryValidationResult ValidateTrustEntries() => TrustEntryValidationResult.NotSupported;
    }
}
