using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;

namespace ITVComponents.WebCoreToolkit.AspExtensions.SharedData
{
    public class EndPointTrunk
    {
        private IList<IOpenApiDescriptor> registry = new List<IOpenApiDescriptor>();

        /// <summary>
        /// Registers an EndPoint in the list of configurable EndPoints
        /// </summary>
        /// <param name="descriptor">the created Descriptor that enables a later caller to perform some additional configurations on the created endpoint</param>
        public void Register(IOpenApiDescriptor descriptor)
        {
            registry.Add(descriptor);
        }

        /// <summary>
        /// Processes all registered end-points
        /// </summary>
        /// <param name="processCallback">the callback to use for processing the registrations</param>
        public void ProcessEndPoints(Action<IOpenApiDescriptor> processCallback)
        {
            registry.Where(n => !n.Invalid).ForEach(processCallback);
        }
    }
}
