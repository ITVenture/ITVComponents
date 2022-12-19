using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.BackgroundProcessing
{
    /// <summary>
    /// Background-Service class that can be injected into a Web-Application to perform background-tasks that will ptentially take a long time
    /// </summary>
    public class BackgroundTaskProcessorService<TQueue, TItem> : BackgroundService where TItem:BackgroundTask where TQueue:ITaskQueue<TItem>
    {
        private readonly IServiceProvider services;
        private readonly TQueue queue;
        private readonly ILogger<BackgroundTaskProcessorService<TQueue,TItem>> logger;

        /// <summary>
        /// Initializes a new instance of the BackgroundTaskProcessorService class
        /// </summary>
        /// <param name="services">the service-provider of the global scope</param>
        /// <param name="queue">a Task-Queue that will provide this worker with tasks from frontend requests</param>
        /// <param name="logger">a logger that is used to log errors and warnings</param>
        public BackgroundTaskProcessorService(IServiceProvider services, TQueue queue, ILogger<BackgroundTaskProcessorService<TQueue, TItem>> logger)
        {
            this.services = services;
            this.queue = queue;
            this.logger = logger;
        }

        /// <summary>
        /// This method is called when the <see cref="T:Microsoft.Extensions.Hosting.IHostedService" /> starts. The implementation should return a task that represents
        /// the lifetime of the long running operation(s) being performed.
        /// </summary>
        /// <param name="stoppingToken">Triggered when <see cref="M:Microsoft.Extensions.Hosting.IHostedService.StopAsync(System.Threading.CancellationToken)" /> is called.</param>
        /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> that represents the long running operations.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Work(stoppingToken);
        }

        /// <summary>
        /// Processes the open tasks until the web is stopped
        /// </summary>
        /// <param name="stoppingToken">cancellation token for stoppingn the processing</param>
        /// <returns>an awaitable task</returns>
        private async Task Work(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var task = await queue.Dequeue(stoppingToken);
                if (task != null)
                {
                    try
                    {
                        using (var scope = services.CreateScope())
                        {
                            if (task.ConservedContext != null)
                            {
                                scope.ServiceProvider.PrepareContext(task.ConservedContext);
                            }
                            else
                            {
                                scope.ServiceProvider.PrepareEmptyContext();
                            }

                            await task.Task(
                                new BackgroundTaskContext(scope.ServiceProvider,
                                    scope.ServiceProvider.GetService<IContextUserProvider>()),
                                task.AdditionalArguments);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Error occurred during Task execution");
                    }
                }
            }
        }
    }
}
