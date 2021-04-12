using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins.PluginServices;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel
{
    public class AssemblyDescriptionModel
    {
        public string AssemblyName { get; private set; }

        public TypeDescriptor[] Types { get; private set; }

        public AssemblyDescriptionModel(string assemblyName, TypeDescriptor[] types)
        {
            AssemblyName = assemblyName;
            Types = types;
        }
    }
}
