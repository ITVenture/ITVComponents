using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.WebPlugins;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.GenericService.WebService.Services
{
    internal class SimplePluginInjector<T>: IInjectablePlugin<T> where T : class
    {
        private readonly IPluginFactory factory;
        public SimplePluginInjector(IWebPluginHelper factory)
        {
            this.factory = factory.GetFactory();
        }

        public T Instance => (T)factory.FirstOrDefault(n => n is T);
        public T GetInstance(string name)
        {
            return (T)factory[name];
        }

        public void Dispose()
        {
        }
    }
}
