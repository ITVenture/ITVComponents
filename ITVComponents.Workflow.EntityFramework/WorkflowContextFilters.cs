using ITVComponents.EFRepo.DbContextConfig.Expressions;
using ITVComponents.EFRepo.Options;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.Workflow.EntityFramework
{
    /// <summary>
    /// Konfiguriert die tenant-abhaengigen globalen Query-Filter des <see cref="WorkflowContext"/>.
    /// </summary>
    public static class WorkflowContextFilters
    {
        /// <summary>
        /// Registriert die Filter. Die <c>CurrentTenant</c>-Referenz ist ein Platzhalter, den der
        /// EFRepo-Visitor zur Model-Build-Zeit durch die gleichnamige Property des laufenden Kontexts
        /// ersetzt (siehe <see cref="DbContextModelBuilderOptions{TContext}.ConfigureExpressionProperty"/>).
        /// </summary>
        public static void ConfigureFilters<TContext>(DbContextModelBuilderOptions<TContext> options)
            where TContext : DbContext
        {
            // Definitionen: eigener Tenant ODER oeffentlich (null). Oeffentliche Workflows sind fuer
            // alle Tenants sichtbar und im Kontext eines beliebigen Tenants startbar.
            options.ConfigureGlobalFilter<WorkflowDefinitionRow>(
                r => r.TenantId == CurrentTenant || r.TenantId == null);

            // Instanzen: strikt der eigene Tenant. Eine laufende Instanz gehoert genau einem Tenant.
            options.ConfigureGlobalFilter<WorkflowInstanceRow>(
                r => r.TenantId == CurrentTenant);

            // Tokens: derselbe strikte Filter. Der Tenant steht denormalisiert an der Token-Zeile und
            // kommt beim Speichern IMMER von der Instanz (EfWorkflowStore.SaveInstance), es gibt also
            // keine zweite Wahrheit.
            //
            // Fuer die Suchlaeufe des Stores ist das kein Gewinn - sie sammeln nur Kandidaten-Ids und
            // laufen danach durch LoadInstances, und das liest die (gefilterten) Instanzen. Der Filter
            // hier deckt die direkten Zugriffe: Aufgaben-Ansichten, Diagnose-Abfragen, Konsumenten-Code.
            // Wer ueber db.Tokens einsteigt, sah bisher die Zeilen aller Mandanten.
            //
            // ACHTUNG bei Aenderungen an den Lesewegen: die Fehlerart ist hier eine andere als bei den
            // Instanzen. Ein Token, das der Filter verschluckt, liefert kein falsches Ergebnis, sondern
            // einen Vorgang, der stehen bleibt - ohne Meldung. Deshalb laeuft der Runner filterfrei
            // (sein Suchlauf geht jedem WorkflowExecutionScope voraus), und die beiden schreibenden
            // Sammel-Zugriffe im Store setzen ausdruecklich IgnoreQueryFilters().
            options.ConfigureGlobalFilter<TokenRow>(
                r => r.TenantId == CurrentTenant);
        }

        // Platzhalter: der ExpressionFixVisitor ersetzt jede Referenz hierauf durch WorkflowContext.CurrentTenant.
        [ExpressionPropertyRedirect("CurrentTenant")]
        private static string CurrentTenant => "";
    }
}
