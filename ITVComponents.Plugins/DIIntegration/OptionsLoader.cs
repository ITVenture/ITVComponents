using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.Plugins.DIIntegration
{
    public class OptionsLoader<T>:IOptions<T> where T : class
    {
        private readonly IOptionsProvider<T> provider;
        private readonly IServiceProvider services;
        private bool useDi = false;

        public OptionsLoader(IServiceProvider services)
        {
            this.services = services;
            useDi = true;
        }

        public OptionsLoader(IOptionsProvider<T> provider)
        {
            this.provider = provider;
        }

        public T Value => useDi?services.GetService<IOptions<T>>()?.Value:provider.GetOptions(null);
    }
}
