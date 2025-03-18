using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Options
{
    public class PageHandlerFactoryOptions
    {
        private ConcurrentDictionary<Type, Type> handlerTypes = new ConcurrentDictionary<Type, Type>();
        private ConcurrentDictionary<string, Type> genericArguments = new();
        public Type GetImplType<THandlerInterface>()
        {
            if (handlerTypes.TryGetValue(typeof(THandlerInterface), out var retT))
            {
                return retT;
            }

            return null;
        }

        public bool ConfigureGenericArgument(string name, Type type)
        {
            return genericArguments.TryAdd(name, type);
        }

        public void ConfigureHandlerType<TPageModel, THandlerInterface, THandler>(bool update)
        where TPageModel:PageModel
        where THandlerInterface:IPageHandlerInstance<TPageModel>
        where THandler:class, THandlerInterface
        {
            handlerTypes.AddOrUpdate(typeof(THandlerInterface), typeof(THandler),
                (th, ti) => update ? typeof(THandler) : ti);
        }

        public void ConfigureHandlerType(Type pageModelType, Type handlerInterfaceType, Type handlerType, bool update)
        {

            if (!typeof(PageModel).IsAssignableFrom(pageModelType))
            {
                throw new ArgumentException("PageModel implementation expected", nameof(pageModelType));
            }

            if (!typeof(IPageHandlerInstance<>).MakeGenericType(pageModelType).IsAssignableFrom(handlerInterfaceType))
            {
                throw new ArgumentException($"PageHandlerInstance of Type {pageModelType.FullName} expected.",
                    nameof(handlerInterfaceType));
            }

            if (!handlerType.GetInterfaces().Contains(handlerInterfaceType))
            {
                throw new ArgumentException(
                    $"Type {handlerType.FullName} does not implement {handlerInterfaceType.FullName}", nameof(handlerType));
            }

            handlerTypes.AddOrUpdate(handlerInterfaceType, handlerType, (th, ti) => update ? handlerType : ti);
        }

        public Dictionary<string, Type> GetConfiguredGenerics()
        {
            return new Dictionary<string, Type>(genericArguments);
        }
    }
}
