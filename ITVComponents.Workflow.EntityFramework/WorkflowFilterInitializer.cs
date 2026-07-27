using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.EFRepo.Options;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.Workflow.EntityFramework
{
    /// <summary>
    /// Liefert die Model-Optionen (tenant-abhaengige Query-Filter) fuer den
    /// <see cref="WorkflowContext"/> und ist zugleich dessen
    /// <c>IOptions&lt;DbContextModelBuilderOptions&lt;WorkflowContext&gt;&gt;</c>. Wird ueber das
    /// Plugin-System instanziiert und in den Plugin-Ctor des Kontexts eingespeist.
    /// </summary>
    /// <typeparam name="TContext">der zu konfigurierende Kontext-Typ (i.d.R. <see cref="WorkflowContext"/>)</typeparam>
    public class WorkflowFilterInitializer<TContext> : DbModelBuilderOptionsProvider<TContext>
        where TContext : DbContext
    {
        /// <summary>Initialisiert einen eigenstaendigen Initializer.</summary>
        public WorkflowFilterInitializer() : base()
        {
        }

        /// <summary>Verkettet diesen Initializer mit einem uebergeordneten Options-Provider.</summary>
        public WorkflowFilterInitializer(DbModelBuilderOptionsProvider<TContext> parent) : base(parent)
        {
        }

        /// <inheritdoc/>
        protected override void Configure(DbContextModelBuilderOptions<TContext> options)
        {
            WorkflowContextFilters.ConfigureFilters(options);
        }
    }
}
