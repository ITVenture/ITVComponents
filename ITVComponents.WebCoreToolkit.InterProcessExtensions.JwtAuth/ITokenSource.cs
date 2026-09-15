using System;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.JwtAuth
{
    /// <summary>
    /// Ein Bearer samt seiner Gueltigkeit.
    /// </summary>
    /// <param name="Token">der rohe Bearer, ohne das Praefix</param>
    /// <param name="ExpiresUtc">
    /// wann er ablaeuft. <b>Pflichtangabe</b> - ohne sie kann niemand rechtzeitig erneuern, und der erste
    /// Hinweis auf ein abgelaufenes Token waere eine Zurueckweisung am Hub.
    /// </param>
    public sealed record BearerToken(string Token, DateTime ExpiresUtc);

    /// <summary>
    /// Woher ein Hub-Client seinen Bearer bekommt.
    /// </summary>
    /// <remarks>
    /// <b>Der Erweiterungspunkt, nicht ein fester Fluss.</b> Nicht jeder Konsument holt sein Token
    /// gleich: der mitgelieferte Weg tauscht einen API-Schluessel gegen ein Token, ein anderer koppelt
    /// seine Geraete ueber einen eigenen Ablauf und bekommt es von dort. Ein fest verdrahteter
    /// <c>client_credentials</c>-Fluss zwaenge den zweiten Fall dazu, die ganze Klasse zu kopieren.
    /// <para>
    /// Eine eigene Umsetzung wird dem <c>JwtAuthInit</c> als Plugin in den Konstruktor gereicht - das ist
    /// der Weg, den das Plugin-System fuer so etwas ohnehin vorsieht.
    /// </para>
    /// <para>
    /// <b>Die Umsetzung muss nicht zwischenspeichern.</b> Das tut <c>JwtAuthInit</c>, und zwar
    /// nebenlaeufigkeitssicher - an einem Hub-Client haengen mehrere gleichzeitige Aufrufe, und ein
    /// Erneuerungssturm ist genau der Fehler, den man dann sucht.
    /// </para>
    /// </remarks>
    public interface ITokenSource
    {
        /// <summary>
        /// Besorgt ein frisches Token.
        /// </summary>
        /// <param name="ct">ein Abbruchtoken</param>
        /// <returns>das Token, oder null wenn keines zu bekommen war</returns>
        Task<BearerToken> AcquireAsync(CancellationToken ct = default);
    }
}
