using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins
{
    /// <summary>
    /// Provides the possibility for an object to indicate whether operations are available now
    /// </summary>
    public interface IOperationalProvider
    {
        /// <summary>
        /// Gets a value indicating whether this object is operational
        /// </summary>
        bool Operational { get; }

        /// <summary>
        /// Is Raised when the value for the OperationalFlag has changed
        /// </summary>
        event EventHandler OperationalChanged;
    }
}
