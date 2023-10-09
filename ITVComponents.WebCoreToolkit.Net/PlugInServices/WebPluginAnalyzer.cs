using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.Plugins.PluginServices;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;

namespace ITVComponents.WebCoreToolkit.Net.PlugInServices
{
    [ScopedDependency(FriendlyName = "WebPluginAnalyzer")]
    public class WebPluginAnalyzer:AssemblyPluginAnalyzer
    {
        public WebPluginAnalyzer(IPluginFactory factory) : base(factory)
        {
        }

        /// <summary>
        /// Analyzes the the current type and extends the descriptor with custom information
        /// </summary>
        /// <param name="currentType">the current type that is being described</param>
        /// <param name="descriptor">the type description that was estimated by the default-implementation</param>
        protected override void AnalyzeType(Type currentType, TypeDescriptor descriptor)
        {
            base.AnalyzeType(currentType, descriptor);
            var scoped = Attribute.GetCustomAttribute(currentType, typeof(ScopedDependencyAttribute)) as ScopedDependencyAttribute;
            if (scoped != null)
            {
                descriptor.Remarks = $"FriendlyName: {scoped.FriendlyName}";
            }
        }
    }
}
