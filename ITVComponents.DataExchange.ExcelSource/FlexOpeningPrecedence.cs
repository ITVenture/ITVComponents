using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.DataExchange.ExcelSource
{
    public enum FlexOpeningPrecedence
    {
        /// <summary>
        /// Indicates that the X-Excel format is preferred
        /// </summary>
        XFormat,

        /// <summary>
        /// Indicates that the legacy-Excel format is preferred
        /// </summary>
        LegacyFormat
    }
}
