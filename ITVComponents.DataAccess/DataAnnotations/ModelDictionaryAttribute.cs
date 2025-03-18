using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;

namespace ITVComponents.DataAccess.DataAnnotations
{
    public class ModelDictionaryAttribute:CustomValueSourceAttribute
    {
        public bool SimpleTypesOnly { get; set; }

        protected internal override object GetCustomValueFor(object originalObject, Func<Type, object> requestInstance)
        {
            return originalObject.ToDictionary(SimpleTypesOnly);
        }
    }
}
