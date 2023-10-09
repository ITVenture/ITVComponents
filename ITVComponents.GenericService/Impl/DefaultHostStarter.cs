using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ITVComponents.GenericService.Impl
{
    internal class DefaultHostStarter:IHostStarter
    {
        private IHostBuilder hostBuilder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder();

        public IHost Host { get; private set; }
        
        public IServiceCollection ServiceCollection { get; private set; } = new ServiceCollection();
        public void ApplyServices(IServiceCollection services)
        {
            if (services != ServiceCollection)
            {
                foreach (var desc in services)
                {
                    ServiceCollection.Add(desc);
                }
            }
        }

        public void Run()
        {
            hostBuilder.ConfigureServices(c =>
            {
                var oriSvc = ServiceCollection;
                ServiceCollection = c;
                ApplyServices(oriSvc);
            });

            Host = hostBuilder.Build();
            Host.Run();
        }

        public Task RunAsync(CancellationToken cancellation)
        {
            ServiceCollection.add
            hostBuilder.ConfigureServices(c =>
            {
                var oriSvc = ServiceCollection;
                ServiceCollection = c;
                ApplyServices(oriSvc);
            });

            Host = hostBuilder.Build();
            return Host.RunAsync(cancellation);
        }

        public void Shutdown()
        {
            Host.Dispose();
        }

        public void WithHost(Action<IHostBuilder> configureBuilder)
        {
            configureBuilder(hostBuilder);
        }
    }
}
