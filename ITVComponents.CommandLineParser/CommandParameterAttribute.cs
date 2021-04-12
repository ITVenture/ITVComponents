using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.CommandLineParser
{
    [AttributeUsage(AttributeTargets.Property)]
    public class CommandParameterAttribute:Attribute
    {
        /// <summary>
        /// The name of the Argument in the command line (/blah or -blah or something)
        /// </summary>
        public string ArgumentName { get; set; }

        /// <summary>
        /// Indicates whether this is an optional parameter
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>
        /// Provides a description for the parameter
        /// </summary>
        public string ParameterDescription { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the this parameter is the help - parameter that can stand for itself
        /// </summary>
        public bool IsHelpParameter { get; set; }
    }
}
