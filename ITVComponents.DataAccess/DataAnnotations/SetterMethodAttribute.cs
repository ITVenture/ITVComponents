using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.DataAccess.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SetterMethodAttribute:Attribute
    {
        /// <summary>
        /// Initializes a new instance of the SetterMethodAttribute class
        /// </summary>
        /// <param name="methodName">the Method that is used to assign a value to the decorated Property</param>
        public SetterMethodAttribute(string methodName)
        {
            MethodName = methodName;
        }

        /// <summary>
        /// Gets the Methodname that is used to set the assigned property
        /// </summary>
        public string MethodName { get; }
    }
}
