using ITVComponents.InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Client;
using System;
using System.Threading;
using Grpc.Core;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.InterProcessExtensions.JwtAuth.Config;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.JwtAuth
{
    /// <summary>
    /// Haengt an jeden Hub-Aufruf einen Bearer und haelt ihn aktuell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bis PRE239 war das eine Attrappe:</b> <c>GetCurrentBearer</c> gab eine leere Zeichenkette
    /// zurueck, <c>Initialize</c> setzte nur ein Flag. Jeder Aufruf ging mit
    /// <c>Authorization: Bearer </c> hinaus, und am Hub endete das als Zurueckweisung, die von
    /// <b>fehlenden Rechten</b> sprach statt von einem fehlenden Token. Die Klasse war vollstaendig
    /// genug, dass man sie fuer fertig hielt - der Header wurde ja gesetzt.
    /// </para>
    /// <para>
    /// <b>Woher das Token kommt, entscheidet eine <see cref="ITokenSource"/>.</b> Ohne eigene kommt der
    /// mitgelieferte Weg zum Zug (API-Schluessel gegen Token, siehe <see cref="ApiKeyTokenSource"/>); wer
    /// sein Token anders bekommt, reicht dem Konstruktor eine eigene herein - das Plugin-System sieht
    /// genau das vor.
    /// </para>
    /// </remarks>
    public class JwtAuthInit: CollectableClientInit, IDeferredInit
    {
        private readonly JwtAuthConfig configuration;
        private readonly ITokenSource tokenSource;
        private readonly bool ownsTokenSource;

        // Ein Hub-Client wird von mehreren Aufrufen gleichzeitig benutzt. Ohne diese Sperre erneuern
        // beim Ablauf ALLE zugleich - genau der Sturm, den man hinterher sucht.
        private readonly SemaphoreSlim gate = new(1, 1);
        private BearerToken current;

        public JwtAuthInit(string configName)
            : this(configName, null)
        {
        }

        /// <summary>
        /// Mit eigener Token-Quelle.
        /// </summary>
        /// <param name="configName">der Name der Konfiguration</param>
        /// <param name="tokenSource">
        /// woher das Token kommt. <c>null</c> waehlt den mitgelieferten Weg ueber den konfigurierten
        /// Token-Endpunkt.
        /// </param>
        public JwtAuthInit(string configName, ITokenSource tokenSource)
        {
            configuration = JwtAuthenticationSection.Helper.JwtAuthSchemes[configName];
            if (configuration == null)
            {
                throw new InvalidOperationException(
                    $"There is no jwt-authentication configuration called '{configName}'.");
            }

            ownsTokenSource = tokenSource == null;
            this.tokenSource = tokenSource ?? new ApiKeyTokenSource(configuration);
        }

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization { get; } = true;

        /// <summary>
        /// Configures the options used for the next call
        /// </summary>
        /// <param name="optionsRaw">the current state of call-options value</param>
        /// <returns>the modified call-options for the next call</returns>
        public override CallOptions ConfigureCallOptions(CallOptions optionsRaw)
        {
            var retVal = base.ConfigureCallOptions(optionsRaw);
            var ent = new Metadata.Entry("Authorization", $"Bearer {GetCurrentBearer()}");
            if (retVal.Headers == null)
            {
                retVal = retVal.WithHeaders(new Metadata { ent });
            }
            else
            {
                retVal.Headers.Add(ent);
            }

            return retVal;
        }

        /// <summary>
        /// Returns the current bearer-token for this connection
        /// </summary>
        /// <remarks>
        /// Erneuert, sobald das Token innerhalb von <see cref="JwtAuthConfig.RenewBeforeSeconds"/>
        /// ablaeuft. <b>Wirft, wenn keines zu bekommen ist</b>, statt eine leere Zeichenkette
        /// zurueckzugeben: ein leerer Bearer faellt erst am Hub auf, und dort als Rechteproblem.
        /// </remarks>
        /// <returns>the current bearer-token that enables this instance to access the remote hub service</returns>
        private string GetCurrentBearer()
        {
            var token = EnsureToken();
            if (token == null)
            {
                throw new InvalidOperationException(
                    $"No bearer token could be obtained for the jwt-configuration '{configuration.Name}'. See the log for the reason.");
            }

            return token.Token;
        }

        private BearerToken EnsureToken()
        {
            var renewBefore = TimeSpan.FromSeconds(Math.Max(0, configuration.RenewBeforeSeconds));
            var snapshot = current;
            if (snapshot != null && snapshot.ExpiresUtc - renewBefore > DateTime.UtcNow)
            {
                return snapshot;
            }

            gate.Wait();
            try
            {
                // Zweite Pruefung innerhalb der Sperre: waehrend des Wartens hat sehr wahrscheinlich
                // jemand anderes bereits erneuert.
                snapshot = current;
                if (snapshot != null && snapshot.ExpiresUtc - renewBefore > DateTime.UtcNow)
                {
                    return snapshot;
                }

                var fresh = tokenSource.AcquireAsync().GetAwaiter().GetResult();
                if (fresh != null)
                {
                    current = fresh;
                    LogEnvironment.LogEvent(
                        $"A bearer token for '{configuration.Name}' was obtained; it expires {fresh.ExpiresUtc:u}.",
                        LogSeverity.Report);
                }
                else if (snapshot != null)
                {
                    // Das alte Token gilt vielleicht noch ein paar Sekunden - es wegzuwerfen machte aus
                    // einer voruebergehenden Stoerung sofort einen Ausfall.
                    LogEnvironment.LogEvent(
                        $"Renewing the bearer token for '{configuration.Name}' failed; the previous one is kept until it expires {snapshot.ExpiresUtc:u}.",
                        LogSeverity.Warning);
                    return snapshot;
                }

                return current;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        /// <remarks>
        /// Holt das erste Token gleich hier. <b>Scheitert das, scheitert die Initialisierung</b> - ein
        /// Client, der sich nicht ausweisen kann, soll beim Hochfahren auffallen und nicht beim ersten
        /// fachlichen Aufruf.
        /// </remarks>
        public void Initialize()
        {
            if (Initialized)
            {
                return;
            }

            if (EnsureToken() == null)
            {
                throw new InvalidOperationException(
                    $"The jwt-configuration '{configuration.Name}' could not obtain an initial bearer token. See the log for the reason.");
            }

            Initialized = true;
        }

        /// <summary>
        /// Gibt die eigene Token-Quelle und die Sperre frei.
        /// </summary>
        /// <remarks>
        /// <b>Ueberschreibt <c>Dispose(bool)</c> und verdeckt nicht <c>Dispose()</c>.</b> Die Basisklasse
        /// fuehrt ein eigenes Aufraeumen und loest danach ihr <c>Disposed</c>-Ereignis aus - ein eigenes
        /// <c>Dispose()</c> haette beides unterschlagen, und zwar lautlos.
        /// <para>
        /// Fremde Token-Quellen werden NICHT freigegeben: sie gehoeren dem, der sie hereingereicht hat,
        /// und der benutzt sie moeglicherweise noch woanders.
        /// </para>
        /// </remarks>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (ownsTokenSource && tokenSource is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                gate.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
