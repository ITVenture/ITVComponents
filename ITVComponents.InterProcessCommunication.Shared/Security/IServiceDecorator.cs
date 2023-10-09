using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.Shared.Security
{
    /// <summary>
    /// Enables a Plugin to provide a Remote-Service decoration for another PlugIn. This can be useful, if an Object should onyl be exposed with additional security settings
    /// </summary>
    public interface IServiceDecorator 
    {
        /// <summary>
        /// The Decorated Service-Instance
        /// </summary>
        public object DecoratedService { get; }
    }
}
