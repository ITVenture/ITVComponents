using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
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

                if (obj is ValidationAttribute vatt && obj is not DataTypeAttribute && string.IsNullOrEmpty(vatt.ErrorMessage) && string.IsNullOrEmpty(vatt.ErrorMessageResourceName))
                {
                    var tmp = options.ResourceForAttribute(vatt, "ValidationError", null);
                    if (!string.IsNullOrEmpty(tmp))
                    {
                        vatt.ErrorMessage = tmp;
                    }
                    else
                    {
                        Console.WriteLine($"No message found for {vatt}.");
                    }
                }
                else if (obj is DataTypeAttribute datt && string.IsNullOrEmpty(datt.ErrorMessage) &&
                         string.IsNullOrEmpty(datt.ErrorMessageResourceName))
                {
                    var dtyp = datt.DataType != DataType.Custom?datt.DataType.ToString():datt.CustomDataType;
                    var tmp = options.ResourceForAttribute(datt, "ValidationError", dtyp);
                    if (!string.IsNullOrEmpty(tmp))
                    {
                        datt.ErrorMessage = tmp;
                    }
                    else
                    {
                        Console.WriteLine($"No message found for {datt} with type {dtyp}.");
                    }
                }
                else if (obj is ValidationAttribute vatt2)
                {
                    Console.WriteLine($"{vatt2} has a custom message ({vatt2.ErrorMessage}) or a resourcename ({vatt2.ErrorMessageResourceName}).");
                }
                /*attribute.ErrorMessageResourceName = tmp;
                attribute.ErrorMessageResourceType = theType;*/
                // other attributes like RangeAttribute, CompareAttribute, etc
            }
        }
    }
}