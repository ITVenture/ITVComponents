using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DuckTyping
{
    /// <summary>
    /// Helper interface enables a client object to access the actual Static class wrapper through the interface wrapper
    /// </summary>
    public interface IStaticWrapper
    {
        /// <summary>
        /// Gets the actual Implementation of the StaticWrapper
        /// </summary>
        StaticInterfaceWrapper ActualWrapper { get; }
    }
}
