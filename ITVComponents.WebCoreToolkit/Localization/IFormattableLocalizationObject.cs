using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Localization
{
    /// <summary>
    /// Allows Json-Localization-Objects to contain string-format expressions without having to prepare the entire localization-entry for string-format.
    /// </summary>
    public interface IFormattableLocalizationObject
    {
        /// <summary>
        /// Formats all properties with the given format-arguments
        /// </summary>
        /// <param name="formatArgs"></param>
        void FormatProperties(params object[] formatArgs);
    }
}
