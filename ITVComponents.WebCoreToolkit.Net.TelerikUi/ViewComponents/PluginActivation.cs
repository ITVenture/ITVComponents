using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.Plugins.PluginServices;
using ITVComponents.WebCoreToolkit.Net.PlugInServices;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Data;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewComponents
{
    public class PluginActivation:ViewComponent
    {
        private readonly IInjectablePlugin<WebPluginAnalyzer> localLoader;

        public PluginActivation(IInjectablePlugin<WebPluginAnalyzer> localLoader = null)
        {
            this.localLoader = localLoader;
        }

        public async Task<IViewComponentResult> InvokeAsync(Type type, string pluginName, PluginParameterInfo[] arguments = null)
        {
            arguments ??= Array.Empty<PluginParameterInfo>();
            var description = localLoader.Instance.DescribeType(type);
            if (type.IsGenericTypeDefinition)
            {
                throw new InvalidOperationException("Generic types are not supported with this component.");
            }

            var targetConstructor = description.Constructors.FirstOrDefault(n =>
                n.Parameters.Length == arguments.Length &&
                (from t in n.Parameters join a in arguments on t.ParameterName equals a.ParameterName select a)
                .Count() == arguments.Length);
            if (targetConstructor == null)
            {
                throw new InvalidOperationException("No constructor found that matches the given settings");
            }

            var model = new PluginTemplateModel { UniqueName = pluginName };
            model.Controls = (from t in arguments.SelectMany(n => n.Inputs)
                select new PluginParameterTemplate
                {
                    Name = t.Name,
                    EditorType = t.EditorType,
                    CustomConfig = t.CustomConfig
                }).ToArray();
        }
    }
}
