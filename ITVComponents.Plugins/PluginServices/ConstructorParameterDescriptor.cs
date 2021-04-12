using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins.PluginServices
{
     [Serializable]
    public class ConstructorParameterDescriptor
    {
        /// <summary>
        /// Gets or sets the name of the described parameter
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// Gets or sets the type of the described parameter
        /// </summary>
        public string ParameterType { get; set; }

        /// <summary>
        /// Gets or sets the Prefix for the given type
        /// </summary>
        public string TypePrefix { get;set; }

        /// <summary>
        /// gets or sets the description of the described parameter
        /// </summary>
        public string ParameterDescription { get; set; }

         /// <summary>
         /// Gets or sets the unique id of this descriptor object
         /// </summary>
         public int Uid { get; set; }

         /// <summary>
         /// Gets or sets a sample value for this parameter
         /// </summary>
         public string SampleValue { get; set; }

         /// <summary>
         /// Gets or sets additional remarks for the current Constructor parameter
         /// </summary>
         public string Remarks { get; set; }
    }
}
