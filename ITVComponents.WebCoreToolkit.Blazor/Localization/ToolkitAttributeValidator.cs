using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Blazor.Localization
{
    /// <summary>
    /// Stateless validation engine used by <c>ToolkitDataAnnotationsValidator</c> to apply
    /// DataAnnotation attributes and translate their error messages through an
    /// <see cref="IStringLocalizer"/> obtained from <see cref="IAttributeMessageLocalizer"/>.
    ///
    /// Mirrors the MVC topic-key conventions: an attribute's <see cref="ValidationAttribute.ErrorMessage"/>
    /// may be a topic-prefixed key like <c>"ITV:RequiredAttribute_ValidationError"</c>; if it is empty,
    /// a default topic-key is computed via <see cref="AttributeTranslationOptions.ResourceForAttribute"/>.
    /// Attributes themselves are never mutated, so this is safe to call concurrently across circuits
    /// even when the runtime caches attribute instances.
    /// </summary>
    public static class ToolkitAttributeValidator
    {
        /// <summary>
        /// Validates every property on <paramref name="model"/> and pushes translated error
        /// messages into <paramref name="messageStore"/>. Does not clear the store — the caller
        /// is expected to do so before invoking this method when re-validating.
        /// </summary>
        public static void ValidateModel(
            object model,
            EditContext editContext,
            ValidationMessageStore messageStore,
            IStringLocalizer localizer,
            AttributeTranslationOptions options)
        {
            foreach (var property in model.GetType().GetProperties())
            {
                ValidateProperty(model, property, editContext, messageStore, localizer, options);
            }
        }

        /// <summary>
        /// Validates a single field. Use this from <c>EditContext.OnFieldChanged</c> so that only
        /// the touched field is re-evaluated rather than the whole model.
        /// </summary>
        public static void ValidateField(
            object model,
            FieldIdentifier fieldIdentifier,
            ValidationMessageStore messageStore,
            IStringLocalizer localizer,
            AttributeTranslationOptions options)
        {
            var property = fieldIdentifier.Model.GetType().GetProperty(fieldIdentifier.FieldName);
            if (property == null)
            {
                return;
            }

            ValidateProperty(fieldIdentifier.Model, property, editContext: null, messageStore, localizer, options, explicitField: fieldIdentifier);
        }

        private static void ValidateProperty(
            object model,
            PropertyInfo property,
            EditContext? editContext,
            ValidationMessageStore messageStore,
            IStringLocalizer localizer,
            AttributeTranslationOptions options,
            FieldIdentifier? explicitField = null)
        {
            var attributes = property.GetCustomAttributes<ValidationAttribute>(inherit: true).ToArray();
            if (attributes.Length == 0)
            {
                return;
            }

            var value = property.GetValue(model);
            var displayName = GetDisplayName(property);
            var field = explicitField ?? editContext!.Field(property.Name);

            // Use GetValidationResult (not IsValid) so attributes that need the surrounding model
            // (CompareAttribute reaches OtherProperty via the context) don't throw.
            var validationContext = new ValidationContext(model)
            {
                MemberName = property.Name,
                DisplayName = displayName
            };

            foreach (var attribute in attributes)
            {
                var result = attribute.GetValidationResult(value, validationContext);
                if (result == ValidationResult.Success || result is null)
                {
                    continue;
                }

                var message = ResolveMessage(attribute, displayName, localizer, options, validationContext.ObjectType);
                messageStore.Add(field, message);
            }
        }

        private static string ResolveMessage(
            ValidationAttribute attribute,
            string displayName,
            IStringLocalizer localizer,
            AttributeTranslationOptions options,
            Type modelType)
        {
            // 1. Determine the topic-key to look up: an explicit ErrorMessage wins, otherwise
            //    compute the default key from AttributeTranslationOptions (mirrors what MVC's
            //    ModelMetaOverrider does at metadata-build time).
            var topicKey = !string.IsNullOrEmpty(attribute.ErrorMessage)
                ? attribute.ErrorMessage!
                : options.ResourceForAttribute(attribute, "ValidationError", GetValidationType(attribute));

            if (string.IsNullOrEmpty(topicKey))
            {
                // No mapping configured for this attribute type — let the attribute do its
                // native (English) formatting rather than show an unhelpful raw key.
                return attribute.FormatErrorMessage(displayName);
            }

            // 2. Look up the localized format string. ResourceNotFound → keep the raw key so the
            //    developer sees something actionable instead of a silent fallback.
            var localized = localizer[topicKey];
            var formatString = localized.ResourceNotFound ? topicKey : localized.Value;

            // 3. Substitute placeholders. Different attribute types expose different parameters
            //    (StringLength's max/min, Range's min/max, …); we mirror what each attribute's own
            //    FormatErrorMessage override would pass to string.Format.
            return FormatMessage(formatString, displayName, attribute, modelType);
        }

        private static string FormatMessage(string format, string displayName, ValidationAttribute attribute, Type modelType)
        {
            try
            {
                return attribute switch
                {
                    StringLengthAttribute sla => string.Format(format, displayName, sla.MaximumLength, sla.MinimumLength),
                    MinLengthAttribute mla => string.Format(format, displayName, mla.Length),
                    MaxLengthAttribute mxa => string.Format(format, displayName, mxa.Length),
                    RangeAttribute ra => string.Format(format, displayName, ra.Minimum, ra.Maximum),
                    RegularExpressionAttribute rea => string.Format(format, displayName, rea.Pattern),
                    CompareAttribute ca => string.Format(format, displayName, GetOtherPropertyDisplayName(ca, modelType)),
                    _ => string.Format(format, displayName)
                };
            }
            catch (FormatException)
            {
                // The translator returned a format string with more/different placeholders than the
                // attribute provides. Return the unformatted string rather than crash validation.
                return format;
            }
        }

        private static string GetOtherPropertyDisplayName(CompareAttribute ca, Type modelType)
        {
            // CompareAttribute.OtherPropertyDisplayName is set by MVC's model-metadata pipeline;
            // outside MVC (i.e. in Blazor) it stays null, so we resolve it ourselves: look at the
            // OtherProperty on the model and honour its [Display(Name)] if present.
            if (!string.IsNullOrEmpty(ca.OtherPropertyDisplayName))
            {
                return ca.OtherPropertyDisplayName;
            }

            var otherProperty = modelType.GetProperty(ca.OtherProperty);
            if (otherProperty != null)
            {
                var name = otherProperty.GetCustomAttribute<DisplayAttribute>()?.GetName();
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            return ca.OtherProperty;
        }

        private static string? GetValidationType(ValidationAttribute attribute)
        {
            // DataTypeAttribute and its subclasses (EmailAddress, Phone, CreditCard, …) carry the
            // sub-kind in the DataType enum; that becomes part of the topic-key so DefaultModelMessages
            // can host distinct entries like "DataTypeAttribute.EmailAddress_ValidationError".
            if (attribute is DataTypeAttribute dta)
            {
                return dta.DataType != DataType.Custom ? dta.DataType.ToString() : dta.CustomDataType;
            }
            return null;
        }

        private static string GetDisplayName(PropertyInfo property)
        {
            var display = property.GetCustomAttribute<DisplayAttribute>();
            if (display != null)
            {
                var name = display.GetName();
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            return property.Name;
        }
    }
}
