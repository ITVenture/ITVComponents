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
    public class ForceTrueAttribute :
        ValidationAttribute
    {
        public ForceTrueAttribute()
        {
        }

        protected override ValidationResult? IsValid(
            object? value, ValidationContext validationContext)
        {
            if (value is not bool b || !b)
            {
                return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
            }

            return ValidationResult.Success;
        }
    }
}

