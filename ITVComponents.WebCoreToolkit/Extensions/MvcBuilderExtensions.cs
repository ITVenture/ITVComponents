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
                    factory.CreateFallbackLocalizer(type, options);
            });
        }
    }
}
