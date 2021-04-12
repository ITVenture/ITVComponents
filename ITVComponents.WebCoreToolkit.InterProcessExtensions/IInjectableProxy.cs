using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions
{
    /// <summary>
    /// Enables any object to consume a configurable remote object via dependencyInjection
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IInjectableProxy<T> where T:class
    {
        /// <summary>
        /// Gets the remote instance as proxy-object
        /// </summary>
        T Value { get; }
    }
}
