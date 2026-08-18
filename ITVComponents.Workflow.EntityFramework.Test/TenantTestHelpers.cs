using System;
using System.Collections.Generic;
using System.Security.Claims;
using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.WebCoreToolkit.Security;
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

    /// <summary>
    /// Ein minimaler Service-Provider fuer die Tests: er kennt genau die Dienste, die ihm mitgegeben werden,
    /// und liefert fuer alles andere null. Genau das erwartet der <see cref="WorkflowContext"/> - er fragt
    /// seine beiden Dienste mit <c>GetService</c> ab und kommt auch ohne sie zurecht.
    /// </summary>
    internal sealed class TestServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> services = new();

        public TestServiceProvider Add<TService>(TService implementation)
        {
            services[typeof(TService)] = implementation;
            return this;
        }

        public object GetService(Type serviceType)
            => services.TryGetValue(serviceType, out var service) ? service : null;
    }

    /// <summary>
    /// Ein fester Mandant als Permission-Scope. Erbt von <see cref="PermissionScopeBase"/>, weil
    /// <see cref="IPermissionScope"/> Member mit <c>protected internal</c> hat, die eine fremde Assembly
    /// gar nicht implementieren koennte - die Basisklasse bringt sie mit.
    /// </summary>
    internal sealed class FakePermissionScope : PermissionScopeBase
    {
        private string scope;

        public FakePermissionScope(string scope)
        {
            this.scope = scope;
        }

        protected override string GetPermissionScopePrefix() => scope;

        protected override void SetPermissionScopePrefix(string newScope, bool asTemporary) => scope = newScope;
    }

    /// <summary>Ein fester angemeldeter Benutzer.</summary>
    internal sealed class FakeContextUserProvider : IContextUserProvider
    {
        public FakeContextUserProvider(string userName)
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, userName) }, "test"));
        }

        public ClaimsPrincipal User { get; }

        public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>();

        public string RequestPath => "/";

        public IServiceProvider Services => null;
    }

    /// <summary>
    /// Baut den Service-Provider, den der <see cref="WorkflowContext"/> anstelle des frueheren
    /// <c>IUserAwareContext</c> bekommt: Mandant aus dem <see cref="IPermissionScope"/>, Benutzer aus dem
    /// <see cref="IContextUserProvider"/>.
    /// </summary>
    internal static class TestServices
    {
        public static IServiceProvider ForTenant(string tenant, string userName = "tester")
            => new TestServiceProvider()
                .Add<IPermissionScope>(new FakePermissionScope(tenant))
                .Add<IContextUserProvider>(new FakeContextUserProvider(userName));
    }
}
