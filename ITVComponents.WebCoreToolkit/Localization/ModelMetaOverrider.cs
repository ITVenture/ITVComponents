using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace ITVComponents.WebCoreToolkit.Localization
{
    internal class ModelMetaOverrider:IValidationMetadataProvider
    {
        private readonly AttributeTranslationOptions options;

        public ModelMetaOverrider(AttributeTranslationOptions options)
        {
            this.options = options;
        }
        
        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException();
            }
            var validators = context.ValidationMetadata.ValidatorMetadata;

            // add [Required] for value-types (int/DateTime etc)
            // to set ErrorMessage before asp.net does it
            var theType = context.Key.ModelType;
            var underlyingType = Nullable.GetUnderlyingType(theType);

            if (theType.IsValueType &&
                underlyingType == null && // not nullable type
                validators.Where(m => m.GetType() == typeof(RequiredAttribute)).Count() == 0)
            {
                validators.Add(new RequiredAttribute());
            }
            
            foreach (var obj in validators)
            {
                if (obj is not  ValidationAttribute)
                {
                    continue;
                }

                if (obj is ValidationAttribute attribute && string.IsNullOrEmpty(attribute.ErrorMessage) && string.IsNullOrEmpty(attribute.ErrorMessageResourceName))
                {
                    var tmp = options.ResourceForAttribute(attribute, "ValidationError");
                    if (!string.IsNullOrEmpty(tmp))
                    {
                        attribute.ErrorMessage = tmp;
                    }
                }
                /*attribute.ErrorMessageResourceName = tmp;
                attribute.ErrorMessageResourceType = theType;*/
                // other attributes like RangeAttribute, CompareAttribute, etc
            }
        }
    }
}