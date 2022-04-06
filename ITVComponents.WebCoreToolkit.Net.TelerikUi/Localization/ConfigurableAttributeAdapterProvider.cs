using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Options;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Localization
{
    public class ConfigurableAttributeAdapterProvider : IValidationAttributeAdapterProvider
    {
        private readonly IOptions<AttributeAdapterProviderOptions> options;

        private readonly IValidationAttributeAdapterProvider baseProvider =
            new ValidationAttributeAdapterProvider();

        public ConfigurableAttributeAdapterProvider(IOptions<AttributeAdapterProviderOptions> options)
        {
            this.options = options;
        }

        public IAttributeAdapter GetAttributeAdapter(ValidationAttribute attribute,
            IStringLocalizer stringLocalizer)
        {
            var retVal = options.Value.TryGetAdapter(attribute, stringLocalizer);
            if (retVal == null)
            {
                retVal = baseProvider.GetAttributeAdapter(attribute, stringLocalizer);
            }

            return retVal;
        }
    }
}
