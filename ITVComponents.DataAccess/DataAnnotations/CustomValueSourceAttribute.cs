using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.DataAccess.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property,Inherited=true, AllowMultiple = false)]
    public abstract class CustomValueSourceAttribute:IgnorePropertyAttribute
    {
        /// <summary>
        /// Gets the custom value for a Property that is decorated with a derived attribute
        /// </summary>
        /// <param name="originalObject">the original object that is applied to a viewmodel</param>
        /// <param name="requestInstance"></param>
        /// <returns>the value to assign to the target-property on a viewmodel</returns>
        protected internal abstract object GetCustomValueFor(object originalObject, Func<Type, object> requestInstance);
    }
}
