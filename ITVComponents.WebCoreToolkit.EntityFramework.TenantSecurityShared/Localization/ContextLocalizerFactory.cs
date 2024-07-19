using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Annotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Localization
{
    internal class ContextLocalizerFactory:IStringLocalizerFactory
    {
        private readonly IStringLocalizerFactory defaultFactory;
        private readonly IServiceProvider services;

        private ConcurrentDictionary<string, IStringLocalizer> knownLocalizers =
            new ConcurrentDictionary<string, IStringLocalizer>();

        public ContextLocalizerFactory(IServiceProvider services, IOptions<LocalizationOptions> options, ILoggerFactory loggerFactory)
        {
            defaultFactory = new ResourceManagerStringLocalizerFactory(options,loggerFactory);
            this.services = services;
        }

        public IStringLocalizer Create(Type resourceSource)
        {
            string name = resourceSource.AssemblyQualifiedName;

            return knownLocalizers.GetOrAdd(name, n => new ContextStringLocalizer(defaultFactory.Create(resourceSource),
                services,
                n));
        }

        public IStringLocalizer Create(string baseName, string location)
        {
            string name = $"{baseName}.{location}";
            return knownLocalizers.GetOrAdd(name, s => new ContextStringLocalizer(
                defaultFactory.Create(baseName, location), services,
                s));
        }
    }
}
