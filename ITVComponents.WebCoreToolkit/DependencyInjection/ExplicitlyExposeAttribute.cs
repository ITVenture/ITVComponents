using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.DependencyInjection
{
    /// <summary>
    /// When applied to an interface, the RegisterExplicityInterfaces call will register the given interface additional to the basic-registration
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
    public class ExplicitlyExposeAttribute:Attribute
    {
        /// <summary>
        /// Gets or sets a value indicating whether to expose all sub-Types no matter, if they are decorated with the ExplicitlyExpose Attribute or not.
        /// </summary>
        public bool ExposeEntireTree { get; set; }
    }
}
