using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DataAccess.Linq
{
    /// <summary>
    /// Not-Threadsafe Context. Use this Context only if you need tables in multiple threads and you know what you're doing!
    /// </summary>
    public class UnsafeLinqContext:IDataContext
    {
        public UnsafeLinqContext()
        {
            Tables = new Dictionary<string, IEnumerable<DynamicResult>>();
        }

        /// <summary>
        /// Gets a List of available Tables for this DataContext
        /// </summary>
        public IDictionary<string, IEnumerable<DynamicResult>> Tables { get; private set; }
    }
}
