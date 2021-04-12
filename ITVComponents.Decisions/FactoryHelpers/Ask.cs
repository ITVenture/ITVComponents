using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Decisions.FactoryHelpers
{
    /// <summary>
    /// Marker Type that is used to indicate, that a specific Parameter must be requested during the construction-process by the Get-Value - Callback
    /// </summary>
    public class Ask
    {
        /// <summary>
        /// Prevents a default instance of the Ask Class from being created
        /// </summary>
        private Ask()
        {
        }

        /// <summary>
        /// Initializes static members of the Ask class
        /// </summary>
        static Ask()
        {
            Value = new Ask();
        }

        /// <summary>
        /// Gets the default instance of the Ask class
        /// </summary>
        public static Ask Value { get; }
    }
}
