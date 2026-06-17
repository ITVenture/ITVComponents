using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection
{
    /// <summary>
    /// Yields a fresh, per-operation <see cref="ICoreSystemContext"/> (backed by the scoped
    /// <see cref="IDbContextFactory{TContext}"/> → correct tenant/user state, collision-free vs. the shared
    /// circuit-scoped context). Consumers (Blazor handlers) use it via <see cref="CoreSystemContextFactoryExtensions"/>
    /// so the context lifetime (create/dispose) is managed centrally and never leaks.
    /// </summary>
    public interface ICoreSystemContextFactory
    {
        /// <summary>Creates a fresh context. The caller owns it (dispose via the Use*-helpers).</summary>
        ICoreSystemContext CreateContext();
    }

    internal sealed class CoreSystemContextFactory<TContext> : ICoreSystemContextFactory
        where TContext : DbContext, ICoreSystemContext
    {
        private readonly IDbContextFactory<TContext> inner;

        public CoreSystemContextFactory(IDbContextFactory<TContext> inner)
        {
            this.inner = inner;
        }

        public ICoreSystemContext CreateContext() => inner.CreateDbContext();
    }

    /// <summary>
    /// Central helpers for running a unit of work on a fresh, per-operation <see cref="ICoreSystemContext"/>.
    /// They create the context, optionally elevate (ShowAllTenants) for admin reads/writes, run the body and
    /// dispose the context afterwards — so handlers never hold a long-lived/circuit-shared context (Blazor-safe)
    /// and never deal with disposal themselves. Because the admin handlers load+modify+save within a single call,
    /// no detached Attach/Update is needed: load and save happen on the same per-operation instance.
    /// </summary>
    public static class CoreSystemContextFactoryExtensions
    {
        public static async Task<T> UseAsync<T>(this ICoreSystemContextFactory factory,
            Func<ICoreSystemContext, Task<T>> body, bool showAllTenants = true)
        {
            var db = factory.CreateContext();
            try
            {
                if (showAllTenants)
                {
                    db.ShowAllTenants = true;
                }

                return await body(db).ConfigureAwait(false);
            }
            finally
            {
                (db as IDisposable)?.Dispose();
            }
        }

        public static async Task UseAsync(this ICoreSystemContextFactory factory,
            Func<ICoreSystemContext, Task> body, bool showAllTenants = true)
        {
            var db = factory.CreateContext();
            try
            {
                if (showAllTenants)
                {
                    db.ShowAllTenants = true;
                }

                await body(db).ConfigureAwait(false);
            }
            finally
            {
                (db as IDisposable)?.Dispose();
            }
        }
    }
}
