using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Configuration
{
    /// <summary>
    /// Injects Scope-driven settings with a specific settings-type
    /// </summary>
    /// <typeparam name="TSettings">the demanded settings-type</typeparam>
    public interface IGlobalSettings<TSettings> where TSettings : class, new()
    {
        /// <summary>
        /// Gets the deserialized Settings-value. If it is not configured, an object is constructed, using the Default-Constructor.
        /// </summary>
        TSettings Value { get; }

        /// <summary>
        /// Gets the deserialized Settings-value. If it is not configured, null is returned (-> default(TSettings)).
        /// </summary>
        TSettings ValueOrDefault { get; }

        /// <summary>
        /// Gets the deserialized Settings-value. If it is not configured, an object is constructed, using the Default-Constructor.
        /// </summary>
        TSettings GetValue(string explicitSettingName);

        /// <summary>
        /// Gets the deserialized Settings-value. If it is not configured, null is returned (-> default(TSettings)).
        /// </summary>
        TSettings GetValueOrDefault(string explicitSettingName);
    }
}
