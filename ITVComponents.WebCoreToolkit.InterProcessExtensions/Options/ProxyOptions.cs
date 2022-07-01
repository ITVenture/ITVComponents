using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Options
{
    public class ProxyOptions
    {
        /// <summary>
        /// holds a list of usable proxy-injectors
        /// </summary>
        private Dictionary<Type, IProxyInjector> injectors = new Dictionary<Type, IProxyInjector>();

        /// <summary>
        /// Registers an injector to the list of available proxy-injectors
        /// </summary>
        /// <typeparam name="T">the interface of which a proxy needs to be created</typeparam>
        /// <param name="injector">the injector used to create the demanded instance</param>
        public void AddInjector<T>(ProxyInjector<T> injector) where T : class
        {
            var t = typeof(T);
            injectors.Add(t, injector);
        }

        /// <summary>
        /// Registers an injector to the list of available proxy-injectors
        /// </summary>
        /// <typeparam name="T">the interface of which a proxy needs to be created</typeparam>
        /// <param name="injector">the injector used to create the demanded instance</param>
        public void AddInjector(Type t, IProxyInjector injector)
        {
            injectors.Add(t, injector);
        }

        /// <summary>
        /// Creates the requested proxy object
        /// </summary>
        /// <typeparam name="T">the proxy-type to return</typeparam>
        /// <param name="services">a services collection that provides required services</param>
        /// <returns>the created proxy instance</returns>
        internal T GetProxy<T>(IServiceProvider services) where T:class
        {
            if (!injectors.ContainsKey(typeof(T)))
            {
                throw new InvalidOperationException("the provided proxy-type is not configured!");
            }

            var injector = (ProxyInjector<T>) injectors[typeof(T)];
            return injector.GetProxyInstance(services);
        }
    }
}
