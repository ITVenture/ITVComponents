using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub
{
    class ServiceStartup
    {
        public ServiceStartup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        
        public void ConfigureServices(IServiceCollection services)
        {
            ServiceHubProvider.Instance.ConfigureServices(services);
            services.AddControllers();
            services.AddGrpc(opt =>
            {
                opt.EnableDetailedErrors = true;
                opt.MaxReceiveMessageSize = null;
                opt.MaxSendMessageSize = null;
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            ServiceHubProvider.Instance.ConfigureApp(app, env);
            app.UseEndpoints(e =>
            {
                ServiceHubProvider.Instance.MapEndPoints(e);
            });
        }
    }
}
