using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ValidationAdapters
{
    public class ConditionalRequiredAttributeAdapter: AttributeAdapterBase<ConditionalRequiredAttribute>

    {
        public ConditionalRequiredAttributeAdapter(ConditionalRequiredAttribute attribute, IStringLocalizer stringLocalizer) : base(attribute, stringLocalizer)
        {
        }

        public override string GetErrorMessage(ModelValidationContextBase validationContext)
        {
            return GetErrorMessage(validationContext.ModelMetadata, validationContext.ModelMetadata.GetDisplayName());
        }

        public override void AddValidation(ClientModelValidationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            MergeAttribute(context.Attributes, "data-val", "true");
            MergeAttribute(context.Attributes, "data-val-requiredif", GetErrorMessage(context));

            if (!string.IsNullOrEmpty(Attribute.ClientCondition))
            {
                MergeAttribute(context.Attributes, "data-val-requiredif-condition", Attribute.ClientCondition);
            }
        }
    }
}
