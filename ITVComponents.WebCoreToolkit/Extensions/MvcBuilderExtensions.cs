using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.Localization;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class MvcBuilderExtensions
    {
        public static IMvcBuilder UseToolkitAttributeTranslator(this IMvcBuilder builder, AttributeTranslationOptions options)
        {
            return builder.AddDataAnnotationsLocalization(opt =>
            {
                opt.DataAnnotationLocalizerProvider = (type, factory) =>
                {
                    var retVal = new FallbackLocalizer(factory.Create(type), factory, options);
                    return retVal;
                };
            });
        }
    }
}
