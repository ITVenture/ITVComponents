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
        }

        // Platzhalter: der ExpressionFixVisitor ersetzt jede Referenz hierauf durch WorkflowContext.CurrentTenant.
        [ExpressionPropertyRedirect("CurrentTenant")]
        private static string CurrentTenant => "";
    }
}
