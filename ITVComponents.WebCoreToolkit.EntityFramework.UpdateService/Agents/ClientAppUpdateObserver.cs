using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Agents
{
    /// <summary>
    /// Synchronizes a client app with the backend when its agent connects to the service hub: pushes the assignment of
    /// modules and fetches the update state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Listens to the broker of the hub that runs in THIS process, like <c>ClientAppLastSeenObserver</c>. Without one
    /// (the hub runs elsewhere) nothing happens on connect - that is said once at the start; the assignment then reaches
    /// the agent only when it is changed.
    /// </para>
    /// <para>
    /// An agent restarts after every update, so its connect also brings the new version into the backend.
    /// </para>
    /// </remarks>
    public sealed class ClientAppUpdateObserver<TContext, TClientApp, TClientAppTemplate> : IHostedService
        where TContext : DbContext, IUpdateServiceContext, IClientAppContext<TClientApp, TClientAppTemplate>
        where TClientApp : class, IClientAppCore
        where TClientAppTemplate : class, IClientAppTemplateCore
    {
        private readonly IServiceProvider services;
        private readonly UpdateAgentOptions options;
        private readonly ILogger<ClientAppUpdateObserver<TContext, TClientApp, TClientAppTemplate>> logger;
        private readonly ConcurrentDictionary<string, byte> running = new(StringComparer.OrdinalIgnoreCase);
        private readonly CancellationTokenSource stopping = new();
        private IServiceRegistrationEvents? events;

        public ClientAppUpdateObserver(IServiceProvider services, IOptions<UpdateAgentOptions> options,
            ILogger<ClientAppUpdateObserver<TContext, TClientApp, TClientAppTemplate>> logger)
        {
            this.services = services;
            this.options = options.Value;
            this.logger = logger;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            events = services.GetService<IServiceHubProvider>()?.Broker as IServiceRegistrationEvents;
            if (events == null)
            {
                logger.LogInformation("No local service hub broker is available; module assignments are pushed to agents only when they change.");
                return Task.CompletedTask;
            }

            events.ServiceRegistered += OnServiceRegistered;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (events != null)
            {
                events.ServiceRegistered -= OnServiceRegistered;
            }

            stopping.Cancel();
            return Task.CompletedTask;
        }

        private void OnServiceRegistered(object? sender, ServiceRegistrationEventArgs e)
        {
            // An agent that registers again while its synchronization waits does not start a second one.
            if (!running.TryAdd(e.ServiceName, 0))
            {
                return;
            }

            // Not awaited: the event runs in the thread of the registration. Errors are logged in SynchronizeAsync.
            _ = SynchronizeAsync(e.ServiceName);
        }

        private async Task SynchronizeAsync(string serviceName)
        {
            try
            {
                await Task.Delay(options.ConnectDelay, stopping.Token).ConfigureAwait(false);
                await using var scope = services.CreateAsyncScope();
                var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TContext>>();
                int tenantId, clientAppId;
                await using (var db = await dbFactory.CreateDbContextAsync(stopping.Token).ConfigureAwait(false))
                {
                    var app = await db.ClientApps.IgnoreQueryFilters().AsNoTracking()
                        .Where(a => EF.Property<string>(a, nameof(IClientAppCore.ClientKey)) == serviceName
                                    && EF.Property<bool>(a, nameof(IClientAppCore.Enabled)))
                        .Select(a => new { TenantId = EF.Property<int>(a, nameof(IClientAppCore.TenantId)), ClientAppId = EF.Property<int>(a, nameof(IClientAppCore.ClientAppId)) })
                        .FirstOrDefaultAsync(stopping.Token).ConfigureAwait(false);
                    if (app == null)
                    {
                        logger.LogDebug("Service {ServiceName} is no enabled client app; no update synchronization.", serviceName);
                        return;
                    }

                    tenantId = app.TenantId;
                    clientAppId = app.ClientAppId;
                }

                var result = await scope.ServiceProvider.GetRequiredService<IClientAppUpdateSync>().SynchronizeAsync(tenantId, clientAppId, stopping.Token).ConfigureAwait(false);
                if (!result.Reached)
                {
                    logger.LogWarning("Client app {ServiceName} connected, but its update bridge was not reached: {Failure} {Message}", serviceName, result.Failure, result.Message);
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                logger.LogDebug("The update synchronization of {ServiceName} was cancelled by the shutdown.", serviceName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The update synchronization of client app {ServiceName} failed.", serviceName);
            }
            finally
            {
                running.TryRemove(serviceName, out _);
            }
        }
    }
}
