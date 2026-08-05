using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Factories;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.AspExtensions.PageHandler
{
    internal class FinalPageHandler<TPageModel, THandlerInterface>:IPageHandlerProvider<TPageModel, THandlerInterface>
    where THandlerInterface:IPageHandlerInstance<TPageModel>
    {
        private readonly IPageModelFactory pageModelFactory;
        private readonly IServiceProvider scopeServices;
        private readonly IServiceScopeFactory scopeFactory;
        private THandlerInterface handler;

        public FinalPageHandler(IPageModelFactory pageModelFactory, IServiceProvider scopeServices,
            IServiceScopeFactory scopeFactory)
        {
            this.pageModelFactory = pageModelFactory;
            this.scopeServices = scopeServices;
            this.scopeFactory = scopeFactory;
        }

        // Dieser Anbieter ist scoped, das injizierte IServiceProvider also der Scope, der ihn angefordert hat -
        // unter MVC der Request, unter Blazor der Circuit. Ihn weiterzureichen ist der einzige Weg, wie die
        // Singleton-Fabrik an einen Scope kommt: ueber den HttpContext findet sie ihn nur waehrend einer Anfrage.
        public THandlerInterface Handler =>
            handler ??= pageModelFactory.CreateHandler<TPageModel, THandlerInterface>(scopeServices);

        public IPageHandlerLease<THandlerInterface> BeginOperation()
        {
            var scope = scopeFactory.CreateAsyncScope();
            try
            {
                var operationHandler =
                    pageModelFactory.CreateHandler<TPageModel, THandlerInterface>(scope.ServiceProvider);
                return new PageHandlerLease<THandlerInterface>(scope, operationHandler);
            }
            catch
            {
                // Scheitert das Erzeugen, gehoert der Scope trotzdem weg - sonst haelt jeder misslungene Versuch
                // seine bereits aufgeloesten Dienste fest. Die Ursache reicht weiter an den Aufrufer.
                scope.Dispose();
                throw;
            }
        }
    }
}
