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
        /// Gets the deserialized Settings-value
        /// </summary>
        TSettings Value { get; }
    }
}
