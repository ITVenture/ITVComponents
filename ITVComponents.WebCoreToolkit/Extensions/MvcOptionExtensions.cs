using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Localization;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class MvcOptionExtensions
    {
        public static void ConfigureLocalizationMetaProvider(this MvcOptions options, AttributeTranslationOptions translationOptions)
        {
            options.ModelMetadataDetailsProviders.Add(new ModelMetaOverrider(translationOptions));
        }
    }
}
