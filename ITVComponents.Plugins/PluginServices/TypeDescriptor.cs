using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins.PluginServices
{
    /// <summary>
    /// Describes a Plugin-Type of an assembly
    /// </summary>
    public class TypeDescriptor
    {
        /// <summary>
        /// The Full-Name of the type
        /// </summary>
        public string TypeFullName { get; set; }

        /// <summary>
        /// The available constructors of the Type
        /// </summary>
        public ConstructorDescriptor[] Constructors { get; set; }

        public GenericParameterDescriptor[] GenericParameters { get; set; }

        /// <summary>
        /// The Unique Id of this Description object
        /// </summary>
        public int Uid { get; set; }

        /// <summary>
        /// Gets or sets additional remarks for the current Type
        /// </summary>
        public string Remarks { get; set; }
    }
}
