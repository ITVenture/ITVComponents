using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Options
{
    public class AttributeAdapterProviderOptions
    {
        private Dictionary<Type, Delegate> constructs = new Dictionary<Type, Delegate>();

        public bool TryAddType<TAttribute>(Func<TAttribute, IStringLocalizer, IAttributeAdapter> constructorFunc)
        {
            return constructs.TryAdd(typeof(TAttribute), constructorFunc);
        }

        public IAttributeAdapter TryGetAdapter(Attribute target, IStringLocalizer localizer)
        {
            IAttributeAdapter retVal = null;
            if (constructs.TryGetValue(target.GetType(), out var del))
            {
                retVal = (IAttributeAdapter)del.DynamicInvoke(target, localizer);
            }

            return retVal;
        }
    }
}
