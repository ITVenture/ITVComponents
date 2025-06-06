using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Formatting.Extensions;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Settings;

namespace ITVComponents.Formatting.PluginSystemExtensions
{
    public class PluginParameterFormatter: StringFormatProvider

    {
        /// <summary>
        /// indicates whether to include an object identifying the current environment in the format-prototype
        /// </summary>
        private bool includeEnvironment = false;

        /// <summary>
        /// Initializes a default instance of the PluginParameterFormatter class
        /// </summary>
        public PluginParameterFormatter()
        {
        }

        /// <summary>
        /// Initializes a new instance of the PluginParameterFormatter class
        /// </summary>
        /// <param name="includeEnvironment">indicates whether to include the Environment object of the System-Namespace</param>
        public PluginParameterFormatter(bool includeEnvironment)
        {
            this.includeEnvironment = includeEnvironment;
        }

        protected override string FormatStringInternal(string rawString, Dictionary<string, object> customStringFormatArguments)
        {
            return customStringFormatArguments.FormatText(rawString, TextFormat.DefaultFormatPolicyWithPrimitives);
        }

        protected override void FillDictionary(IDictionary<string, object> values)
        {
            if (includeEnvironment)
            {
                values.Add("$$Environment", typeof(Environment));
            }
            foreach (var item in from t in PluginConstSection.Helper.Parameters select new { t.ConstIdentifier, t.ConstValue })
            {
                values.Add(item.ConstIdentifier, item.ConstValue);
            }
        }
    }
}
