using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>Eine Options-Quelle, die den geteilten In-Memory-SQLite-Kontext verdrahtet.</summary>
    internal sealed class SqliteTestOptionsLoader : ContextOptionsLoader<WorkflowContext>
    {
        private readonly SqliteConnection connection;

        public SqliteTestOptionsLoader(SqliteConnection connection)
        {
            this.connection = connection;
        }

        protected override void ConfigureOptionsBuilder(DbContextOptionsBuilder<WorkflowContext> builder)
        {
            builder.UseSqlite(connection);
        }
    }

    /// <summary>Ein fester Tenant/Benutzer als Ersatz fuer den injizierten Security-Context.</summary>
    internal sealed class FakeUserContext : IUserAwareContext
    {
        public string CurrentUserName => "tester";

        public string CurrentTenant { get; init; }
    }
}
