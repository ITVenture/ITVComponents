using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Plugins.Initialization
{
    public interface IStringFormatProvider:IPlugin
    {
        /// <summary>
        /// Processes a raw-string and uses it as format-string of the configured const-collection
        /// </summary>
        /// <param name="rawString">the raw-string that was read from a plugin-configuration string</param>
        /// <returns>the format-result of the raw-string</returns>
        string ProcessLiteral(string rawString);
    }
}
