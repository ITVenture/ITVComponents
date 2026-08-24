using System;
using System.Security.Claims;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Runtime
{
    /// <summary>
    /// Gemeinsame Basis der Workflow-Handler (Monitoring, Aufgaben, Entwurf).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sie traegt das, was alle drei brauchen: den Zugang zum Dienst-Anbieter, das Oeffnen einer
    /// <see cref="WorkflowOperation"/>, die Berechtigungs-Frage, den Mandanten des laufenden Kontexts -
    /// und vor allem <b>den einen Besitz-Guard</b> (<see cref="TryLoadOwnInstance"/>,
    /// <see cref="MayTouchDefinition"/>).
    /// </para>
    /// <para>
    /// <b>Warum das eine Basis sein muss und keine Sammlung von Kopien:</b> vor dieser Klasse hatte jeder
    /// Handler seine eigene Fassung von <c>BeginOperation</c>, <c>CurrentTenant</c>, <c>UserName</c> und
    /// <c>HasPermission</c> - und in der Folge auch seine eigene Vorstellung davon, wann ein Besitz-Guard
    /// noetig ist. Das Ergebnis war nicht Redundanz, sondern eine Luecke: von sechs eingreifenden
    /// Operationen im Monitoring pruefte nur die Haelfte den Mandanten, und der Entwurfs-Handler pruefte
    /// ihn gar nicht. Ein Guard, den jeder selbst tippt, wird irgendwo nicht getippt.
    /// </para>
    /// <para>
    /// <b>Permission und Mandant sind zwei Fragen.</b> Die Permission sagt, ob jemand diese ART von
    /// Eingriff machen darf; der Guard sagt, ob DIESE Zeile ihn etwas angeht. Wer nur das erste prueft,
    /// laesst jeden mit der Permission an die Vorgaenge aller Mandanten - es genuegt eine geratene Id.
    /// </para>
    /// </remarks>
    internal abstract class WorkflowHandlerBase
    {
        private readonly IFreshInjectablePlugin<WorkflowContext> freshContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowHandlerBase"/> class.
        /// </summary>
        /// <param name="services">der Dienst-Anbieter des laufenden Bereichs</param>
        /// <param name="freshContext">die Quelle fuer frische Workflow-Kontexte</param>
        protected WorkflowHandlerBase(IServiceProvider services,
            IFreshInjectablePlugin<WorkflowContext> freshContext)
        {
            Services = services;
            this.freshContext = freshContext;
        }

        /// <summary>Der Dienst-Anbieter des laufenden Bereichs.</summary>
        protected IServiceProvider Services { get; }

        /// <summary>
        /// Braucht dieser Handler eine Engine? Entwurfs-Operationen kommen ohne aus.
        /// </summary>
        /// <remarks>
        /// Die Unterscheidung ist keine Kosmetik: eine Engine-Factory, die es im Host nicht gibt, soll den
        /// Entwurf nicht aufhalten - und umgekehrt soll ein Eingriff, der eine braucht, nicht still ohne
        /// eine laufen.
        /// </remarks>
        protected virtual bool NeedsEngine => true;

        /// <summary>
        /// Oeffnet eine neue Operation mit frischem Kontext.
        /// </summary>
        /// <param name="environment">die (optionale) Workflow-Umgebung</param>
        /// <returns>die Operation</returns>
        /// <remarks>
        /// Die Engine-Factory wird optional aufgeloest - fehlt sie, wirft erst ein tatsaechlicher
        /// Engine-Zugriff mit erklaerender Meldung. Der Store richtet sich nach der gewaehlten Umgebung;
        /// ohne Umgebung/Settings bleibt es der Standard-Store.
        /// </remarks>
        protected WorkflowOperation BeginOperation(string? environment = null)
            => new WorkflowOperation(freshContext,
                NeedsEngine ? Services.GetService<WorkflowEngineFactory>() : null,
                WorkflowEnvironmentResolver.StoreDependencyName(Services, environment));

        /// <summary>
        /// Prueft die angegebenen Berechtigungen.
        /// </summary>
        /// <param name="user">der Benutzer (siehe Anmerkung)</param>
        /// <param name="permissions">die zu pruefenden Berechtigungen</param>
        /// <returns>true, wenn er sie hat</returns>
        /// <remarks>
        /// <b>Der Parameter wird nicht ausgewertet.</b> Geprueft wird ueber den ambienten Bereich, weil
        /// <c>VerifyUserPermissions</c> den Benutzer selbst aus dem Dienst-Anbieter aufloest und einen
        /// uebergebenen Principal gar nicht verarbeiten kann. Er steht hier nur, weil die Schnittstellen
        /// ihn fuehren; wer echte "pruefe fuer Benutzer X"-Semantik braucht, braucht einen anderen Weg als
        /// diesen.
        /// </remarks>
        public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
            => Services.VerifyUserPermissions(permissions);

        /// <summary>
        /// Der Mandant des laufenden Kontexts, in derselben Schreibweise wie
        /// <c>WorkflowContext.CurrentTenant</c>.
        /// </summary>
        /// <returns>der Mandant, oder null im Ein-Mandanten-Betrieb</returns>
        /// <remarks>
        /// Ueber <see cref="WorkflowTenant.Normalize"/>: frueher stand an einer Stelle <c>ToLower()</c>, im
        /// Kontext dagegen der rohe <c>PermissionPrefix</c> - was der eine Weg schrieb, verglich der andere
        /// anders. Unter SQL Server deckte die Collation das zu.
        /// </remarks>
        protected string? CurrentTenant()
            => WorkflowTenant.Normalize(Services.GetService<IPermissionScope>()?.PermissionPrefix);

        /// <summary>Der Anmeldename des Benutzers, oder null.</summary>
        /// <param name="user">der Benutzer</param>
        /// <returns>sein Name</returns>
        protected static string? UserName(ClaimsPrincipal user) => user?.Identity?.Name;

        /// <summary>
        /// Gehoert eine Zeile mit diesem Mandanten dem laufenden Kontext?
        /// </summary>
        /// <param name="tenantId">der Mandant der Zeile (null = mandantenlos/oeffentlich)</param>
        /// <returns>true, wenn der laufende Kontext sie anfassen darf</returns>
        /// <remarks>
        /// <b>Die eine Mandanten-Entscheidung fuer eingreifende Wege.</b> Ohne laufenden Mandanten ist der
        /// Host im Ein-Mandanten-Betrieb: dann gibt es keine Trennung, die verletzt werden koennte.
        /// <para>
        /// Ausdruecklich geprueft und nicht dem Query-Filter ueberlassen: ob der greift, entscheidet die
        /// Registrierung des Kontexts im Host, und der Weg ueber die DbContext-Factory ist bewusst
        /// filterfrei. Ein Eingriff darf davon nicht abhaengen.
        /// </para>
        /// <para>
        /// <b>Achtung, nicht dasselbe wie der Listen-Filter der Aufgaben:</b> <c>OpenTasks</c> uebersetzt
        /// "kein Mandant" in <c>TenantId IS NULL</c>, diese Frage hier in "alles erlaubt". Das ist eine
        /// offene Produktentscheidung und bewusst NICHT hier vereinheitlicht - ein Listen-Filter und ein
        /// Eingriffs-Guard duerfen unterschiedlich streng sein, sie sollten es nur absichtlich tun.
        /// </para>
        /// </remarks>
        protected bool OwnsTenant(string? tenantId)
        {
            string? tenant = CurrentTenant();
            if (string.IsNullOrEmpty(tenant))
            {
                return true;
            }

            return string.Equals(tenantId, tenant, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Laedt eine Instanz und stellt sicher, dass sie dem laufenden Mandanten gehoert.
        /// </summary>
        /// <param name="op">die laufende Operation</param>
        /// <param name="instanceId">die Kennung der Instanz</param>
        /// <param name="operation">der Name des Eingriffs - er steht so im Log</param>
        /// <param name="instance">die geladene Instanz, wenn der Guard traegt</param>
        /// <returns>true, wenn der Eingriff weitergehen darf</returns>
        /// <remarks>
        /// Beide Ablehnungsgruende bekommen ihre eigene Meldung: "gibt es nicht" und "gehoert einem anderen
        /// Mandanten" sind fuer die Fehlersuche voellig verschiedene Aussagen, und ein gemeinsames stilles
        /// <c>false</c> macht aus einem Angriffsversuch einen Tippfehler.
        /// </remarks>
        protected bool TryLoadOwnInstance(WorkflowOperation op, string instanceId, string operation,
            out WorkflowInstance instance)
        {
            instance = op.Store.GetInstance(instanceId);
            if (instance == null)
            {
                LogEnvironment.LogEvent(
                    $"{operation} der Instanz '{instanceId}' abgelehnt: nicht vorhanden.",
                    LogSeverity.Warning);
                return false;
            }

            if (!OwnsTenant(instance.TenantId))
            {
                LogEnvironment.LogEvent(
                    $"{operation} der Instanz '{instanceId}' abgelehnt: sie gehoert dem Mandanten "
                    + $"'{instance.TenantId}', der laufende Kontext ist '{CurrentTenant()}'.",
                    LogSeverity.Warning);
                instance = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Darf der laufende Mandant diese bestehende Definition aendern?
        /// </summary>
        /// <param name="stored">die bereits gespeicherte Fassung (null = neu)</param>
        /// <param name="operation">der Name des Eingriffs - er steht so im Log</param>
        /// <returns>true, wenn der Eingriff weitergehen darf</returns>
        /// <remarks>
        /// <para>
        /// Eine neue Definition hat keinen Besitzer, den man verletzen koennte - <c>null</c> ist also
        /// erlaubt. Eine oeffentliche gehoert allen; ob jemand sie anfassen darf, beantwortet die
        /// Berechtigung <c>DesignPublic</c> und nicht diese Frage hier.
        /// </para>
        /// <para>
        /// <b>Warum es diesen Guard ueberhaupt braucht:</b> der Store laedt eine Definition ueber ihren
        /// technischen Schluessel bewusst OHNE Query-Filter - eine laufende Instanz eines oeffentlichen
        /// Workflows muss ihre Definition auch dann laden koennen, wenn gerade ein anderer Mandant aktiv
        /// ist. Der Store sagt in seinem eigenen Kommentar, dass die Zugriffsentscheidung damit beim
        /// Aufrufer liegt. Genau der ist hier gemeint.
        /// </para>
        /// </remarks>
        protected bool MayTouchDefinition(WorkflowDefinition stored, string operation)
        {
            if (stored == null || stored.IsPublic || OwnsTenant(stored.TenantId))
            {
                return true;
            }

            LogEnvironment.LogEvent(
                $"{operation} der Definition '{stored.Id}' v{stored.Version} abgelehnt: sie gehoert dem "
                + $"Mandanten '{stored.TenantId}', der laufende Kontext ist '{CurrentTenant()}'.",
                LogSeverity.Warning);
            return false;
        }
    }
}
