using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Factories
{
    
    internal class PageModelFactory:IPageModelFactory
    {
        private readonly IServiceProvider services;
        private readonly IOptions<PageHandlerFactoryOptions> options;
        private readonly IHttpContextAccessor currentContextAccessor;
        private readonly ConcurrentDictionary<Type, Type> configuredHandlerTypes = new();

        public PageModelFactory(IServiceProvider services,  IOptions<PageHandlerFactoryOptions> options, IHttpContextAccessor currentContextAccessor)
        {
            this.services = services;
            this.options = options;
            this.currentContextAccessor = currentContextAccessor;
        }

        public THandlerInterface CreateHandler<TPageModel, THandlerInterface>()
            where THandlerInterface : IPageHandlerInstance<TPageModel>
        {
            var currentServices = currentContextAccessor.HttpContext?.RequestServices;
            if (currentServices == null)
            {
                currentServices = services;
                LogEnvironment.LogEvent("Trying to use global Service-Provider to create handler-instance", LogSeverity.Warning);
            }

            var opt = options.Value;
            if (!configuredHandlerTypes.TryGetValue(typeof(THandlerInterface), out var impt))
            {
                impt = opt.GetImplType<THandlerInterface>();
            }

            if (impt == null)
            {
                throw new InvalidOperationException(
                    "Unable to resolve requested Handler-Type");
            }

            if (impt.IsGenericTypeDefinition)
            {
                try
                {
                    impt = GenericTypeHelper.FinalizeType(impt, opt.GetConfiguredGenerics());
                }
                catch (Exception ex)
                {
                    if (Attribute.GetCustomAttribute(impt, typeof(FallbackPageHandlerAttribute)) is
                        FallbackPageHandlerAttribute fat)
                    {
                        impt = fat.FallbackType;
                    }
                }
            }

            var tmp = ActivatorUtilities.CreateInstance(currentServices, impt);
            if (tmp is not THandlerInterface ret)
            {
                throw new InvalidOperationException(
                    $"The resolved Handler-Type ({tmp.GetType().FullName}) does not implement {typeof(THandlerInterface).FullName}");
            }

            configuredHandlerTypes.TryAdd(typeof(THandlerInterface), impt);
            return ret;
        }
    }
}
