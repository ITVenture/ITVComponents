using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations
{

    public class ConditionalRequiredAttribute :
        ValidationAttribute
    {
        public string ClientCondition { get; set; }
        public string BackEndCondition { get; set; }

        public ConditionalRequiredAttribute()
        {
        }

        protected override ValidationResult? IsValid(
            object? value, ValidationContext validationContext)
        {
            var obj = validationContext.ObjectInstance;
            if (!string.IsNullOrEmpty(BackEndCondition))
            {
                var required = (bool)ExpressionParser.Parse(BackEndCondition, obj);
                bool ok = !required || value != null && (value is not string s || !string.IsNullOrEmpty(s));

                if (!ok)
                {
                    return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
                }
            }

            return ValidationResult.Success;
        }
    }
}

