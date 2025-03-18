using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.DataAccess.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public abstract class CustomObjectValueTranslateAttribute:Attribute
    {
        public abstract T TranslateValue<T>(T source, IServiceProvider services);
    }
}
