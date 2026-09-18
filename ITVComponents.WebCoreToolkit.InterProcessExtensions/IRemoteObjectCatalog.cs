using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions
{
    /// <summary>
    /// Was auf der Gegenseite eines Dienstes an ansprechbaren Objekten bereitsteht.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Das Gegenstück zu <see cref="IInjectableProxy{T}"/>: jener sagt, <b>wie</b> man hinkommt, dieser,
    /// <b>was</b> es dort gibt. Gebraucht wird er überall, wo eine Maske eine Auswahl anbieten soll,
    /// statt jemanden einen Objektnamen abtippen zu lassen — ein Tippfehler dort fällt sonst erst auf,
    /// wenn der Aufruf ins Leere geht.
    /// </para>
    /// <para>
    /// <b>Nur der Vertrag, keine Umsetzung.</b> Woher die Antwort kommt, weiss allein die Anwendung:
    /// aus der Tabelle, in der sie die Plugins ihrer Dienste führt, aus einer Rückfrage beim Dienst
    /// selbst, oder aus einer festen Liste. Das Toolkit stellt die Frage.
    /// </para>
    /// <para>
    /// <b>Der Dienstname ist der <c>ClientKey</c> der Anwendung</b>, die den Dienst bereitstellt. Er
    /// trägt einen systemweit eindeutigen Index und ist damit als Name über Mandantengrenzen hinweg
    /// kollisionsfrei — anders als ein Anzeigename, der zweimal „Kasse 1" lauten darf.
    /// </para>
    /// </remarks>
    public interface IRemoteObjectCatalog
    {
        /// <summary>
        /// Die Objekte eines Dienstes, die einen bestimmten Vertrag erfüllen.
        /// </summary>
        /// <param name="serviceName">der Dienst — der <c>ClientKey</c> der bereitstellenden Anwendung</param>
        /// <param name="contract">
        /// der Vertrag, um den es geht. <b>Ohne ihn wäre die Frage falsch gestellt:</b> auf einem
        /// Kassenrechner hängen auch Belegdrucker und Schubladensteuerung, und eine Liste, die alles
        /// zeigt, führt zu einem Druckertreiber in der Terminal-Auswahl.
        /// </param>
        /// <returns>
        /// die Objekte, oder eine leere Liste. <b>Leer heisst nicht „Fehler"</b> — ein Dienst, der
        /// nichts dieser Art anbietet, ist ein normaler Fall.
        /// </returns>
        Task<IReadOnlyList<RemoteObjectInfo>> GetObjectsAsync(string serviceName, Type contract,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Ein ansprechbares Objekt auf einem Dienst.</summary>
    /// <param name="Name">
    /// der Name, unter dem ein Proxy darauf entsteht — der Wert, der gespeichert wird
    /// </param>
    /// <param name="DisplayName">
    /// wie es in einer Auswahl heissen soll; leer = <paramref name="Name"/>
    /// </param>
    public readonly record struct RemoteObjectInfo(string Name, string? DisplayName = null);
}
