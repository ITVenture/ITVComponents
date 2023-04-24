using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Configuration
{
    /// <summary>
    /// Injects Scope-driven or global settings with a specific settings-type. This requires scoped and global settings to be activated.
    /// </summary>
    /// <typeparam name="TSettings">the demanded settings-type</typeparam>
    public interface IHierarchySettings<TSettings>
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
        /// Gets a value indicating whether this setting was loaded from global or from scope
        /// </summary>
        HierarchyScope SettingScope { get; }

        /// <summary>
        /// Gets the deserialized Settings-value. If it is not configured, an object is constructed, using the Default-Constructor.
        /// </summary>
        TSettings GetValue(string explicitSettingName);

        /// <summary>
        /// Gets the deserialized Settings-value. If it is not configured, null is returned (-> default(TSettings)).
        /// </summary>
        TSettings GetValueOrDefault(string explicitSettingName);

        /// <summary>
        /// Gets a value indicating whether this setting was loaded from global or from scope
        /// </summary>
        HierarchyScope GetSettingScope(string explicitSettingName);
    }

    public enum HierarchyScope
    {
        /// <summary>
        /// Setting was loaded from a global settings-provider
        /// </summary>
        Global,

        /// <summary>
        /// Setting was loaded from a local settings provider
        /// </summary>
        Scoped,

        /// <summary>
        /// Setting was not found on any provider
        /// </summary>
        None
    }
}
