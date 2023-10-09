using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ITVComponents.GenericService
{
    public interface IHostStarter
    {
        public IHost Host { get; }

        public IServiceCollection ServiceCollection { get;  }

        public void ApplyServices(IServiceCollection services);

        public void Run();

        public Task RunAsync(CancellationToken cancellation);
        void Shutdown();
        void WithHost(Action<IHostBuilder> configureBuilder);
    }
}
