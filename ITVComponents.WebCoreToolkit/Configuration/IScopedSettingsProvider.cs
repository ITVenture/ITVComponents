using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Configuration
{
    /// <summary>
    /// Provides custom Permission-scope-aware settings
    /// </summary>
    public interface IScopedSettingsProvider:ISettingsProvider
    {
        /// <summary>
        /// Gets a Json-formatted setting with the given key
        /// </summary>
        /// <param name="key">the demanded key</param>
        /// <param name="explicitUserScope">the explicit scope under which to get the requested settings</param>
        /// <returns>the string-representation of the requested setting</returns>
        string GetJsonSetting(string key, string explicitUserScope);

        /// <summary>
        /// Gets an unformatted plain setting with the given key
        /// </summary>
        /// <param name="key">the demanded key</param>
        /// <param name="explicitUserScope">the explicit scope under which to get the requested settings</param>
        /// <returns>the string representation of the requested setting</returns>
        string GetLiteralSetting(string key, string explicitUserScope);
    }
}
