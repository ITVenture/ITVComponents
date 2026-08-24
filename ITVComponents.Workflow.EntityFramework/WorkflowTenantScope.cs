using System;
using System.Linq;
using System.Linq.Expressions;
using ITVComponents.Workflow.Runtime;

namespace ITVComponents.Workflow.EntityFramework
{
    /// <summary>
    /// Die <b>eine</b> Mandanten-Entscheidung fuer die Laufzeit-Zeilen einer Workflow-Ablage: welche
    /// Instanzen, Tokens, Kommentare und Anhaenge gehen den laufenden Kontext etwas an.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sie wird nicht konfiguriert, sondern abgelesen</b> - am <see cref="WorkflowContext"/>, den der
    /// Aufrufer ohnehin geleast hat. Das ist der Kern: derselbe <c>ctx.CurrentTenant</c>, den diese Klasse
    /// liest, SCHREIBT auch die Zeilen (<c>EfWorkflowStore.SaveInstance</c>:
    /// <c>instance.TenantId ?? ctx.CurrentTenant</c>). Lese- und Schreibseite koennen deshalb nicht
    /// auseinanderlaufen. Eine zweite Angabe - etwa ein Schalter an der Umgebungs-Konfiguration - koennte
    /// dem Kontext widersprechen, und der Widerspruch waere still: Zeilen mandantenlos geschrieben, Ansicht
    /// mandantengebunden gesucht, niemand sieht etwas, niemand erfaehrt warum.
    /// </para>
    /// <para>
    /// <b>Und sie gilt pro Umgebung</b>, ohne dass jemand das erklaeren muesste: jede Umgebung leaset ihr
    /// eigenes Store-Plugin, also ihren eigenen Kontext. Eine Anlage, in der jeder Mandant seine EIGENE
    /// Workflow-Datenbank hat, betreibt diese Ablagen mandantenlos (<see cref="UsesTenants"/> false) -
    /// eine geteilte Ablage betreibt sie mandantengebunden. Beides nebeneinander im selben Prozess ist
    /// erlaubt.
    /// </para>
    /// <para>
    /// <b>Nicht fuer Definitionen.</b> Dort ist <c>TenantId == null</c> nicht "gehoert niemandem", sondern
    /// <i>oeffentlich</i> - fuer alle Mandanten sichtbar und startbar (siehe der Query-Filter in
    /// <c>WorkflowContextFilters</c>: eigener Mandant ODER null). Wer diese Regel hier auf Definitionen
    /// anwendet, laesst jeden oeffentlichen Ablauf aus dem Designer verschwinden.
    /// </para>
    /// <para>
    /// <b>Schreibweise:</b> verglichen wird ueber <see cref="WorkflowTenant.Normalize"/> auf beiden Seiten.
    /// Altbestand, der vor der Vereinheitlichung mit abweichender Schreibweise geschrieben wurde, kann
    /// deshalb in der Liste (SQL-Collation) und im Guard (hier) unterschiedlich ausfallen - er gehoert
    /// einmalig per SQL vereinheitlicht, nicht hier abgefangen.
    /// </para>
    /// </remarks>
    public sealed class WorkflowTenantScope
    {
        private WorkflowTenantScope(bool usesTenants, string tenantId)
        {
            UsesTenants = usesTenants;
            TenantId = tenantId;
        }

        /// <summary>
        /// Liest die Entscheidung am geleasten Kontext ab.
        /// </summary>
        /// <param name="ctx">der Kontext dieser Umgebung</param>
        /// <returns>die Mandanten-Entscheidung fuer seine Laufzeit-Zeilen</returns>
        public static WorkflowTenantScope For(WorkflowContext ctx)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            return new WorkflowTenantScope(ctx.UseTenantFilter, WorkflowTenant.Normalize(ctx.CurrentTenant));
        }

        /// <summary>
        /// Ob diese Ablage ueberhaupt mandantengebunden betrieben wird.
        /// </summary>
        public bool UsesTenants { get; }

        /// <summary>
        /// Der Mandant des laufenden Kontexts (normalisiert), oder null - im mandantenlosen Betrieb IMMER
        /// null.
        /// </summary>
        public string TenantId { get; }

        /// <summary>
        /// Mandantenbetrieb, aber kein Mandant ermittelbar.
        /// </summary>
        /// <remarks>
        /// <b>Ein Verdrahtungsfehler, kein Betriebszustand.</b> Die Ablage traegt Zeilen mit Mandant, aber
        /// der laufende Kontext weiss keinen - dann ist weder "alles" noch "die mandantenlosen" die
        /// richtige Antwort, sondern "nichts, und zwar hoerbar". Wer hier still alles durchliesse, gaebe
        /// jedem mit der passenden Berechtigung Zugriff auf die Vorgaenge aller Mandanten.
        /// </remarks>
        public bool IsUnresolved => UsesTenants && string.IsNullOrEmpty(TenantId);

        /// <summary>
        /// Gehoert eine Zeile mit diesem Mandanten in diese Sicht?
        /// </summary>
        /// <param name="rowTenantId">der Mandant der Zeile</param>
        /// <returns>true, wenn sie dazugehoert</returns>
        /// <remarks>
        /// Die In-Memory-Form von <see cref="Restrict{T}"/> - <b>dieselbe Frage, dieselbe Antwort</b>. Das
        /// ist der Zweck dieser Klasse: was eine Liste nicht zeigt, darf ein Eingriff nicht anfassen. Zwei
        /// getrennt getippte Praedikate haben genau diese Zusicherung nicht.
        /// </remarks>
        public bool Owns(string rowTenantId)
        {
            if (IsUnresolved)
            {
                return false;
            }

            return string.Equals(WorkflowTenant.Normalize(rowTenantId), TenantId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Schraenkt eine Abfrage auf die Zeilen ein, die in diese Sicht gehoeren.
        /// </summary>
        /// <typeparam name="T">der Zeilen-Typ</typeparam>
        /// <param name="source">die Abfrage</param>
        /// <param name="tenantSelector">der Zugriff auf die Mandanten-Spalte dieser Zeile</param>
        /// <returns>die eingeschraenkte Abfrage</returns>
        /// <remarks>
        /// Ausdruecklich gefiltert und nicht dem globalen Query-Filter ueberlassen: <c>TokenRow</c> und die
        /// Kommentar-/Anhangs-Zeilen haengen an Kontexten, deren Filter je nach Registrierung im Host
        /// greift oder nicht (der Weg ueber die DbContext-Factory ist bewusst filterfrei). Eine
        /// Arbeitsliste darf davon nicht abhaengen.
        /// </remarks>
        public IQueryable<T> Restrict<T>(IQueryable<T> source, Expression<Func<T, string>> tenantSelector)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (tenantSelector == null)
            {
                throw new ArgumentNullException(nameof(tenantSelector));
            }

            if (IsUnresolved)
            {
                // Kein Mandant im Mandantenbetrieb: nichts. Als Ausdruck und nicht als leere Liste, damit
                // der Aufrufer weiter komponieren kann (Zaehlen, Sortieren, Blaettern laufen unveraendert).
                return source.Where(Expression.Lambda<Func<T, bool>>(
                    Expression.Constant(false), tenantSelector.Parameters));
            }

            // t => t.TenantId == <mandant>   bzw.   t => t.TenantId == null
            BinaryExpression comparison = Expression.Equal(
                tenantSelector.Body, Expression.Constant(TenantId, typeof(string)));
            return source.Where(Expression.Lambda<Func<T, bool>>(comparison, tenantSelector.Parameters));
        }
    }
}
