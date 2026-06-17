using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection
{
    /// <summary>
    /// An <see cref="IDbContextFactory{TContext}"/> backed by <see cref="ActivatorUtilities"/>. The security
    /// contexts expose a multi-dependency runtime constructor (IPermissionScope, IContextUserProvider, …) which
    /// the <em>default</em> EF factory cannot use (it requires a constructor taking only
    /// <c>DbContextOptions&lt;T&gt;</c>). ActivatorUtilities selects the richest resolvable constructor, so the
    /// produced context is correctly wired (tenant/user filtering, interceptors, lazy proxies).
    ///
    /// Register with <b>Scoped</b> lifetime: the injected <see cref="IServiceProvider"/> is then the current
    /// scope's provider, so each created context receives the scope's IPermissionScope / IContextUserProvider —
    /// while every <see cref="CreateDbContext"/> call returns a <b>fresh, per-operation</b> instance (the
    /// Blazor-safe lifetime that avoids "a second operation was started on this context instance").
    /// The caller owns and disposes the returned context (e.g. <c>using var db = factory.CreateDbContext();</c>).
    /// </summary>
    public sealed class ToolkitDbContextFactory<TContext> : IDbContextFactory<TContext> where TContext : DbContext
    {
        private readonly IServiceProvider services;

        public ToolkitDbContextFactory(IServiceProvider services)
        {
            this.services = services;
        }

        public TContext CreateDbContext()
            => (TContext)ActivatorUtilities.CreateInstance(services, typeof(TContext));
    }
}
