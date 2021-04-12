using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.WebPlugins.Initialization
{
    internal class NoPluginsInit:IConfigureOptions<GlobalPluginsInitOptions>
    {
        /// <summary>
        /// Invoked to configure a <typeparamref name="TOptions" /> instance.
        /// </summary>
        /// <param name="options">The options instance to configure.</param>
        public void Configure(GlobalPluginsInitOptions options)
        {
        }
    }
}
