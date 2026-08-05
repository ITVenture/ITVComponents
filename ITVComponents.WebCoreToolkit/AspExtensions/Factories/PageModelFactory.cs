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
            => CreateHandler<TPageModel, THandlerInterface>(null);

        public THandlerInterface CreateHandler<TPageModel, THandlerInterface>(IServiceProvider scopeServices)
            where THandlerInterface : IPageHandlerInstance<TPageModel>
        {
            // Diese Fabrik ist ein Singleton - ihr eigenes "services" ist damit der Root-Provider, aus dem sich
            // kein Scoped-Dienst aufloesen laesst. Der Aufrufer gibt darum seinen Scope mit; nur wenn er das
            // nicht tut, bleibt der bisherige Weg ueber den HttpContext. Auf einem Blazor-Circuit gibt es keinen
            // HttpContext, und dort endete der Rueckfall auf den Root-Provider in einer InvalidOperationException,
            // sobald der Handler etwas Scoped braucht (UserManager, DbContext) - also praktisch immer.
            var currentServices = scopeServices ?? currentContextAccessor.HttpContext?.RequestServices;
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
                        // Erwarteter Normalfall: der Typ laesst sich fuer diese Konfiguration nicht schliessen,
                        // dafuer gibt es den Ausweichtyp. Trotzdem protokolliert - sonst sieht niemand, dass
                        // nicht der gemeinte Handler laeuft.
                        LogEnvironment.LogEvent(
                            $"Unable to finalize {impt.FullName}, falling back to {fat.FallbackType.FullName}: {ex.OutlineException()}",
                            LogSeverity.Warning);
                        impt = fat.FallbackType;
                    }
                    else
                    {
                        // Ohne Ausweichtyp bleibt impt eine offene generische Definition. Das faellt erst
                        // unten bei CreateInstance auf, mit einer Meldung, die die Ursache nicht mehr zeigt -
                        // also hier melden, solange man sie noch hat.
                        LogEnvironment.LogEvent(
                            $"Unable to finalize {impt.FullName} and no {nameof(FallbackPageHandlerAttribute)} declared: {ex.OutlineException()}",
                            LogSeverity.Error);
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
