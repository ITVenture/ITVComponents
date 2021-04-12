using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins.PluginServices
{
    [Serializable]
    public class ConstructorDescriptor
    {
        /// <summary>
        /// Gets or sets the Simple-description of the Constructor (like .ctor[1,2,3,4,etc.])
        /// </summary>
        public string ConstructorName { get; set; }

        /// <summary>
        /// Gets or sets the Parameter - Description of all parameters
        /// </summary>
        public ConstructorParameterDescriptor[] Parameters { get; set; }

        /// <summary>
        /// The Unique Id of this descriptor object
        /// </summary>
        public int Uid { get; set; }

        /// <summary>
        /// A Sample Constructor string
        /// </summary>
        public string Sample { get;set; }

        /// <summary>
        /// Gets or sets additional remarks for the current Constructor
        /// </summary>
        public string Remarks { get; set; }
    }
}
