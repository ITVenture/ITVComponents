using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection
{
    /// <summary>
    /// Generic per-operation factory for the security/system DbContext, exposed through ANY of the DbSet-bearing
    /// abstraction interfaces it implements (e.g. <c>ICoreSystemContext</c>, <c>ISecurityContext&lt;…&gt;</c>,
    /// <c>IBaseTenantContext&lt;…&gt;</c>, <c>IHierarchyTenantContext&lt;…&gt;</c>, <c>IBillingContext</c>,
    /// <c>ITenantScopeContext</c>, …). Backed by the scoped <see cref="IDbContextFactory{TContext}"/>, so each call
    /// yields a fresh context with correct tenant/user state, collision-free vs. the shared circuit-scoped context
    /// (Blazor-safe). Generalises the per-interface wrapper factories (e.g. the earlier <c>ICoreSystemContextFactory</c>):
    /// because the concrete context implements every one of these interfaces, a single factory can hand it out as any.
    /// </summary>
    public interface IToolkitContextFactory
    {
        /// <summary>
        /// Creates a fresh per-operation context viewed through abstraction <typeparamref name="T"/>. The caller owns
        /// it and must dispose it — use the <c>Use*</c>-helpers, which manage create/dispose centrally. Scope flags
        /// (ShowAllTenants/HideGlobals) are NOT applied here — the caller sets them on the returned context as needed,
        /// so tenant-scoped consumers are not silently elevated.
        /// </summary>
        T Create<T>() where T : class;

        /// <summary>
        /// Like <see cref="Create{T}"/> but returns a disposable lease — handy for field-holding services that use the
        /// context across several methods/helpers within one operation: <c>using var lease = factory.Lease&lt;T&gt;();
        /// var db = lease.Context;</c>. Disposing the lease disposes the underlying context (works even though the
        /// abstraction interfaces are not themselves <see cref="IDisposable"/>).
        /// </summary>
        IContextLease<T> Lease<T>() where T : class;
    }

    /// <summary>A disposable handle owning a per-operation context exposed as <typeparamref name="T"/>.</summary>
    public interface IContextLease<out T> : IDisposable where T : class
    {
        /// <summary>The leased context. Valid until this lease is disposed.</summary>
        T Context { get; }
    }

    internal sealed class ToolkitContextFactory<TContext> : IToolkitContextFactory
        where TContext : DbContext
    {
        private readonly IDbContextFactory<TContext> inner;

        public ToolkitContextFactory(IDbContextFactory<TContext> inner)
        {
            this.inner = inner;
        }

        public T Create<T>() where T : class
        {
            var ctx = inner.CreateDbContext();
            if (ctx is not T typed)
            {
                (ctx as IDisposable)?.Dispose();
                throw new InvalidOperationException(
                    $"The security context '{typeof(TContext).Name}' does not implement the requested abstraction '{typeof(T).Name}'.");
            }

            return typed;
        }

        public IContextLease<T> Lease<T>() where T : class
        {
            var ctx = inner.CreateDbContext();
            if (ctx is not T typed)
            {
                (ctx as IDisposable)?.Dispose();
                throw new InvalidOperationException(
                    $"The security context '{typeof(TContext).Name}' does not implement the requested abstraction '{typeof(T).Name}'.");
            }

            return new ContextLease<T>(typed, ctx);
        }

        private sealed class ContextLease<T> : IContextLease<T> where T : class
        {
            private readonly IDisposable owner;

            public ContextLease(T context, IDisposable owner)
            {
                Context = context;
                this.owner = owner;
            }

            public T Context { get; }

            public void Dispose() => owner.Dispose();
        }
    }

    /// <summary>
    /// Central helpers that run a unit of work on a fresh, per-operation context obtained through
    /// <see cref="IToolkitContextFactory"/>: create → run body → dispose. Consumers never hold a long-lived/
    /// circuit-shared context and never deal with disposal themselves. No scope elevation is applied — set
    /// ShowAllTenants/HideGlobals inside the body when the operation requires it.
    /// </summary>
    public static class ToolkitContextFactoryExtensions
    {
        public static async Task<TResult> UseAsync<T, TResult>(this IToolkitContextFactory factory, Func<T, Task<TResult>> body)
            where T : class
        {
            var ctx = factory.Create<T>();
            try
            {
                return await body(ctx).ConfigureAwait(false);
            }
            finally
            {
                (ctx as IDisposable)?.Dispose();
            }
        }

        public static async Task UseAsync<T>(this IToolkitContextFactory factory, Func<T, Task> body)
            where T : class
        {
            var ctx = factory.Create<T>();
            try
            {
                await body(ctx).ConfigureAwait(false);
            }
            finally
            {
                (ctx as IDisposable)?.Dispose();
            }
        }

        public static TResult Use<T, TResult>(this IToolkitContextFactory factory, Func<T, TResult> body)
            where T : class
        {
            var ctx = factory.Create<T>();
            try
            {
                return body(ctx);
            }
            finally
            {
                (ctx as IDisposable)?.Dispose();
            }
        }
    }
}
