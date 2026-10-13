using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Impl
{
    /// <summary>
    /// Schreibt <see cref="IClientAppCore.LastSeenUtc"/>, wenn sich eine ClientApp am Dienst-Verteiler an- oder
    /// abmeldet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hört auf den Broker des Verteilers, der in DIESEM Prozess läuft. Gibt es keinen (der Verteiler läuft
    /// anderswo), bleibt <c>LastSeenUtc</c> leer — das wird beim Start einmal gesagt, nicht verschwiegen.
    /// </para>
    /// <para>
    /// Die ClientApp wird über ihren <c>ClientKey</c> gefunden, der systemweit eindeutig ist; beim Anmelden gibt
    /// es noch keinen Mandanten. Geschrieben wird nur diese eine Spalte. Dienste, die keine ClientApp sind
    /// (der Name passt auf keinen Schlüssel), ändern nichts.
    /// </para>
    /// </remarks>
    public class ClientAppLastSeenObserver<TContext, TClientApp, TClientAppTemplate> : IHostedService
        where TContext : DbContext, IClientAppContext<TClientApp, TClientAppTemplate>
        where TClientApp : class, IClientAppCore
        where TClientAppTemplate : class, IClientAppTemplateCore
    {
        private readonly IServiceProvider services;
        private readonly ILogger<ClientAppLastSeenObserver<TContext, TClientApp, TClientAppTemplate>> logger;
        private IServiceRegistrationEvents? events;

        /// <summary>Initializes a new instance of the <see cref="ClientAppLastSeenObserver{TContext, TClientApp, TClientAppTemplate}"/> class.</summary>
        public ClientAppLastSeenObserver(IServiceProvider services,
            ILogger<ClientAppLastSeenObserver<TContext, TClientApp, TClientAppTemplate>> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            events = services.GetService<IServiceHubProvider>()?.Broker as IServiceRegistrationEvents;
            if (events == null)
            {
                logger.LogInformation("No local service hub broker is available; LastSeenUtc of client apps is not maintained by this process.");
                return Task.CompletedTask;
            }

            events.ServiceRegistered += OnServiceChanged;
            events.ServiceUnregistered += OnServiceChanged;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (events != null)
            {
                events.ServiceRegistered -= OnServiceChanged;
                events.ServiceUnregistered -= OnServiceChanged;
            }

            return Task.CompletedTask;
        }

        private void OnServiceChanged(object? sender, ServiceRegistrationEventArgs e)
        {
            // Nebenlaeufig: das Ereignis laeuft im Thread der Anmeldung, und die soll nicht auf die Datenbank
            // warten. Fehler werden in TouchAsync protokolliert.
            _ = TouchAsync(e.ServiceName, DateTime.UtcNow);
        }

        private async Task TouchAsync(string serviceName, DateTime now)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TContext>>();
                await using var db = await dbFactory.CreateDbContextAsync();
                var touched = await db.ClientApps.IgnoreQueryFilters()
                    .Where(a => EF.Property<string>(a, nameof(IClientAppCore.ClientKey)) == serviceName)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => EF.Property<DateTime?>(a, nameof(IClientAppCore.LastSeenUtc)), now));
                if (touched == 0)
                {
                    logger.LogDebug("Service {ServiceName} is no client app; LastSeenUtc is not touched.", serviceName);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "LastSeenUtc of client app {ServiceName} could not be written.", serviceName);
            }
        }
    }
}
