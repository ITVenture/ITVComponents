using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Impl
{
    public class CustomConfiguratorAttribute: WebPartRegistrationMethodAttribute
    {
        /// <summary>
        /// Gets the type of the configured custom object that this webpart-configurator is capable of configuring
        /// </summary>
        public Type ConfiguredObjectType { get; }

        /// <summary>
        /// Initializes a new instance of the CustomConfiguratorAttribute class
        /// </summary>
        /// <param name="configuredObjectType">the instance type that is configured with decorated method</param>
        public CustomConfiguratorAttribute(Type configuredObjectType)
        {
            ConfiguredObjectType = configuredObjectType;
        }
    }
}
