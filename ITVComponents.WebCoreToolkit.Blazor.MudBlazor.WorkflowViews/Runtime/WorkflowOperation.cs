using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Stores;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Runtime
{
    /// <summary>
    /// Kapselt eine EINZELNE View-Handler-Operation Blazor-/tenant-sicher. Jeder Store- und
    /// EF-Abfrage-Zugriff dieser Operation least ueber <see cref="IFreshInjectablePlugin{WorkflowContext}"/>
    /// einen FRISCHEN <see cref="WorkflowContext"/> in einem eigenen Operations-Scope. So teilen sich
    /// nebenlaeufige Blazor-Renders NIE einen <c>DbContext</c>, und der Kontext wird pro Operation frisch
    /// aufgeloest statt circuit-lang fixiert.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ein Seam, zwei Registrierungen (DI-vs-Plugin-Dualitaet):</b> WOHER der frische Kontext kommt,
    /// entscheidet der Host bei der Registrierung des scope-owned <c>WorkflowContext</c> (ueber
    /// <c>FactoryOptions.AddDependency(name, delegate, disposeWithScope: true)</c>):
    /// <list type="bullet">
    ///   <item><b>global</b> - das Delegate liefert <c>IDbContextFactory&lt;WorkflowContext&gt;.CreateDbContext()</c>
    ///   (filterfrei, alle Zeilen).</item>
    ///   <item><b>per-Tenant</b> - das Delegate baut einen tenant-faehigen Kontext (Toolkit-Konvention).</item>
    /// </list>
    /// Die Operation selbst ist in beiden Faellen identisch - sie kennt nur <see cref="IFreshInjectablePlugin{T}"/>.
    /// </para>
    /// <para>
    /// Der <see cref="Store"/> ist ein <see cref="EfWorkflowStore"/>, der je Aufruf einen frischen Kontext
    /// leaset (Unit of Work je Aufruf) und ihn selbst wieder disposed. Die Operation sammelt die Leases und
    /// schliesst sie am Ende (<see cref="Dispose"/>) - damit fallen die Operations-Scopes samt scope-owned
    /// Dependencies (das erneute Dispose des bereits vom Store geschlossenen Kontexts ist idempotent).
    /// </para>
    /// <para>
    /// Die <see cref="Engine"/> wird ueber die vom Host gelieferte <see cref="WorkflowEngineFactory"/>
    /// gebaut (mit dem frischen <see cref="Store"/>). Fuer reine Lese-/Design-Operationen wird keine Engine
    /// gebraucht - dann darf die Factory fehlen (der <see cref="Engine"/>-Zugriff wirft erst bei
    /// tatsaechlichem Bedarf mit erklaerender Meldung).
    /// </para>
    /// </remarks>
    internal sealed class WorkflowOperation : IDisposable
    {
        // Der WorkflowContext wird als scope-owned Dependency unter diesem Namen frisch in den
        // Operations-Scope gehaengt (BuildOperationScope). Die DEFAULT-Namensaufloesung der Lease liefe ueber
        // den IWebPluginsSelector und faende die Dependency NICHT -> darum explizit ueber den Namen aus dem
        // ScopedDependency-Attribut leasen (trifft die frisch registrierte Dependency via scope[name,true]).
        private static readonly string ContextPluginName =
            (Attribute.GetCustomAttribute(typeof(WorkflowContext), typeof(ScopedDependencyAttribute))
                as ScopedDependencyAttribute)?.FriendlyName ?? typeof(WorkflowContext).Name;

        private readonly IFreshInjectablePlugin<WorkflowContext> freshContext;
        private readonly WorkflowEngineFactory? engineFactory;
        private readonly string? storeDependencyName;
        /// <summary>
        /// Die Modelle, fuer die die Filter-Pruefung schon gelaufen ist. Als schwache Tabelle, damit die
        /// Pruefung kein Modell am Leben haelt, das die Anwendung sonst freigeben wuerde.
        /// </summary>
        private static readonly ConditionalWeakTable<IModel, object> ReportedModels = new();

        /// <summary>Der Platzhalter-Wert der Tabelle - gebraucht wird nur der Schluessel.</summary>
        private static readonly object Reported = new();

        private readonly List<IDisposable> leases = new();
        private EfWorkflowStore? store;
        private WorkflowEngine? engine;
        private WorkflowTenantScope? tenantScope;
        private bool disposed;

        /// <summary>
        /// Erzeugt eine Operation. <paramref name="storeDependencyName"/> waehlt - fuer den
        /// Mehr-Umgebungen-Betrieb - den Namen der scope-owned <c>WorkflowContext</c>-Dependency, die
        /// geleast wird (der Store-Plugin-Name der gewaehlten Umgebung). Null = der Standard-Name aus dem
        /// <see cref="ScopedDependencyAttribute"/> des <c>WorkflowContext</c> - also die einzelne, per DI
        /// registrierte Umgebung (bisheriges Verhalten).
        /// </summary>
        public WorkflowOperation(IFreshInjectablePlugin<WorkflowContext> freshContext,
            WorkflowEngineFactory? engineFactory = null, string? storeDependencyName = null)
        {
            this.freshContext = freshContext ?? throw new ArgumentNullException(nameof(freshContext));
            this.engineFactory = engineFactory;
            this.storeDependencyName = string.IsNullOrWhiteSpace(storeDependencyName) ? null : storeDependencyName;
        }

        /// <summary>
        /// Least einen frischen Kontext (eigener Operations-Scope) fuer eine direkte EF-Abfrage. Der Scope
        /// wird erst beim <see cref="Dispose"/> dieser Operation geschlossen - der Kontext bleibt fuer die
        /// Dauer der Abfrage gueltig.
        /// </summary>
        public WorkflowContext LeaseContext()
        {
            EnsureNotDisposed();
            // Standard-Name (eine Umgebung) ODER der Store-Plugin-Name der gewaehlten Umgebung. Beide
            // treffen eine per Namen registrierte scope-owned WorkflowContext-Dependency (scope[name,true]).
            IPluginLease<WorkflowContext> lease = freshContext.Lease(storeDependencyName ?? ContextPluginName);
            leases.Add(lease);
            WarnIfUnfiltered(lease.Value, storeDependencyName ?? ContextPluginName);
            return lease.Value;
        }

        /// <summary>
        /// Meldet, wenn dieser Kontext <b>gar keinen</b> Mandantenfilter mitbringt.
        /// </summary>
        /// <param name="ctx">der geleaste Kontext</param>
        /// <param name="dependencyName">unter welchem Namen er geholt wurde - das ist der Hinweis fuer die Suche</param>
        /// <remarks>
        /// <para>
        /// Die Lese-Wege der Ansichten ziehen ihre Mandantengrenze <b>aus dem Query-Filter</b> und nicht
        /// aus einer eigenen Pruefung - <c>ListDefinitionsAsync</c> und <c>GetDefinitionAsync</c> fragen
        /// schlicht ungefiltert ab und verlassen sich darauf, dass das Modell filtert. Das ist so gewollt:
        /// die Grenze zweimal zu ziehen hiesse, zwei Fassungen zu haben, die auseinanderlaufen koennen.
        /// </para>
        /// <para>
        /// <b>Aber es darf keine stille Annahme sein.</b> Es gibt in diesem Haus ausdruecklich einen
        /// filterfreien Weg - der Runner braucht ihn, weil sein Suchlauf jedem Mandanten-Scope vorausgeht,
        /// und wirklich filterfrei ist allein die options-only-Registrierung ueber
        /// <c>IDbContextFactory&lt;WorkflowContext&gt;</c>, auf der gar keine Model-Optionen gesetzt werden.
        /// Haengt eine ANSICHT versehentlich an dieser Registrierung, sieht sie die Zeilen aller Mandanten
        /// - ohne Fehler, ohne Meldung, die Liste ist nur laenger. Genau das sagt diese Meldung.
        /// </para>
        /// <para>
        /// Nicht zu verwechseln mit <c>useTenantFilter: false</c>: das ist NICHT filterfrei, sondern
        /// wertet den aktiven Mandanten als <c>null</c> aus - dann sind nur noch die mandantenlosen
        /// (bei Definitionen: die oeffentlichen) Zeilen sichtbar. Das faellt von selbst auf, weil man zu
        /// WENIG sieht; der Fall hier ist der umgekehrte und deshalb der teurere.
        /// </para>
        /// <para>
        /// Einmal je Modell gemeldet, nicht je Abfrage: das Modell ist ein langlebiges Singleton je
        /// Konfiguration, und eine Meldung pro Rasterseite waere keine Warnung mehr, sondern Rauschen.
        /// </para></remarks>
        private static void WarnIfUnfiltered(WorkflowContext ctx, string dependencyName)
        {
            IModel model = ctx.Model;
            if (ReportedModels.TryGetValue(model, out _))
            {
                return;
            }

            ReportedModels.AddOrUpdate(model, Reported);
            // GetDeclaredQueryFilters und nicht das veraltete GetQueryFilter: seit EF Core 10 kann eine
            // Entitaet MEHRERE benannte Filter tragen, und die Einzahl-Form ist deshalb abgekuendigt.
            // Geprueft wird nur, OB ueberhaupt einer da ist - welcher, ist hier nicht die Frage.
            if (model.FindEntityType(typeof(WorkflowDefinitionRow))?.GetDeclaredQueryFilters().Any() == true)
            {
                return;
            }

            LogEnvironment.LogEvent(
                $"The workflow context leased as '{dependencyName}' carries NO tenant query filter at all "
                + "(the model has none on WorkflowDefinitionRow). The view and design paths take their "
                + "tenant boundary from exactly that filter - without it this view lists the rows of EVERY "
                + "tenant, and nothing about that looks like an error. This is the filter-free "
                + "registration meant for the runner (options-only, IDbContextFactory<WorkflowContext>); "
                + "the views belong on the named plugin registration that sets the model options.",
                LogSeverity.Error);
        }

        /// <summary>
        /// Die Mandanten-Entscheidung dieser Operation - einmal am Kontext abgelesen, dann festgehalten.
        /// </summary>
        /// <remarks>
        /// Einmal, weil sie sich innerhalb einer Operation nicht aendern kann und jeder Aufruf sonst eine
        /// weitere Lease zoege. Abgelesen und nicht konfiguriert: derselbe <c>ctx.CurrentTenant</c>
        /// entscheidet hier ueber die Sicht und schreibt im Store den Mandanten der Zeilen - Lese- und
        /// Schreibseite koennen damit nicht auseinanderlaufen.
        /// </remarks>
        public WorkflowTenantScope TenantScope
        {
            get
            {
                EnsureNotDisposed();
                return tenantScope ??= WorkflowTenantScope.For(LeaseContext());
            }
        }

        /// <summary>
        /// Der Store dieser Operation. Leaset je Store-Aufruf einen frischen Kontext ueber
        /// <see cref="LeaseContext"/>; die Operations-Scopes werden am Ende der Operation geschlossen.
        /// </summary>
        public IWorkflowStore Store
        {
            get
            {
                EnsureNotDisposed();
                return store ??= new EfWorkflowStore(LeaseContext);
            }
        }

        /// <summary>
        /// Die Engine dieser Operation, gebaut ueber die Host-<see cref="WorkflowEngineFactory"/> mit dem
        /// frischen <see cref="Store"/>. Wirft, wenn keine Factory registriert ist.
        /// </summary>
        public WorkflowEngine Engine
        {
            get
            {
                EnsureNotDisposed();
                if (engineFactory == null)
                {
                    throw new InvalidOperationException(
                        "Diese Operation benoetigt eine WorkflowEngine, aber es ist keine " +
                        $"'{nameof(WorkflowEngineFactory)}' im DI-Container registriert. Der Host muss fuer " +
                        "Signal-/Abbruch-Operationen eine Engine-Factory registrieren (siehe AddWorkflowViews).");
                }

                return engine ??= engineFactory(Store);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            // In umgekehrter Reihenfolge freigeben (verschachtelte Scopes zuerst schliessen).
            for (int i = leases.Count - 1; i >= 0; i--)
            {
                try
                {
                    leases[i].Dispose();
                }
                catch (Exception ex)
                {
                    // Eine fehlgeschlagene Freigabe darf die uebrigen nicht mitreissen; der Grund muss aber
                    // nachvollziehbar im Log stehen (globale Regel: kein stiller catch).
                    LogEnvironment.LogEvent(
                        $"Konnte den Operations-Scope eines Workflow-Kontexts nicht freigeben: {ex.OutlineException()}",
                        LogSeverity.Error);
                }
            }

            leases.Clear();
        }

        private void EnsureNotDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(WorkflowOperation));
            }
        }
    }
}
