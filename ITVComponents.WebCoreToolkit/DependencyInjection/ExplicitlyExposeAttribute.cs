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
    }
}
