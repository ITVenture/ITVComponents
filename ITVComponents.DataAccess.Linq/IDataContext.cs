using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Plugins;

namespace ITVComponents.DataAccess.Linq
{
    public interface IDataContext:IPlugin
    {
        /// <summary>
        /// Gets a List of available Tables for this DataContext
        /// </summary>
        IDictionary<string, IEnumerable<DynamicResult>> Tables { get; } 
    }
}
