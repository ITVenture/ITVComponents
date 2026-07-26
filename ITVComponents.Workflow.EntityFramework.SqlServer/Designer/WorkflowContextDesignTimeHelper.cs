using ITVComponents.Workflow.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Designer
{
    /// <summary>
    /// Design-Time-Factory fuer <c>dotnet ef</c> (Migrations-Generierung, SQL Server). Nutzt den
    /// options-only-Ctor des <see cref="WorkflowContext"/> - er liefert das schema-korrekte Modell; die
    /// tenant-abhaengigen globalen Query-Filter sind reine Laufzeit-Filter und beeinflussen das Schema
    /// nicht. Die Verbindungszeichenfolge dient AUSSCHLIESSLICH der Migrations-Erzeugung; zur Laufzeit
    /// waehlt der Host Provider und Verbindung selbst (die Migrations-Assembly wird beim
    /// <c>UseSqlServer(..., MigrationsAssembly(...))</c> des Hosts referenziert).
    /// </summary>
    public class WorkflowContextDesignTimeHelper : IDesignTimeDbContextFactory<WorkflowContext>
    {
        /// <inheritdoc/>
        public WorkflowContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<WorkflowContext>();
            optionsBuilder.UseSqlServer(
                @"Server=(localdb)\mssqllocaldb;Database=IWCWorkflow;Trusted_Connection=True;",
                so => so.MigrationsAssembly(typeof(WorkflowContextDesignTimeHelper).Assembly.FullName));
            return new WorkflowContext(optionsBuilder.Options);
        }
    }
}
