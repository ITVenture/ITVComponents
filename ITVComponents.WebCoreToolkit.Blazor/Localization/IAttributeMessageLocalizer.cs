using System;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Blazor.Localization
{
    /// <summary>
    /// Service that produces an <see cref="IStringLocalizer"/> for resolving topic-prefixed
    /// validation messages (e.g. <c>"ITV:RequiredAttribute_ValidationError"</c>) attached to
    /// DataAnnotation attributes on a Blazor model. The returned localizer applies the same
    /// per-type → topic-resource fallback chain that the MVC pipeline uses via
    /// <c>UseToolkitAttributeTranslator</c>.
    /// </summary>
    public interface IAttributeMessageLocalizer
    {
        /// <summary>
        /// Returns a string-localizer scoped to <paramref name="modelType"/> that the Blazor
        /// validator components use to translate <see cref="System.ComponentModel.DataAnnotations.ValidationAttribute"/>
        /// error messages.
        /// </summary>
        IStringLocalizer GetLocalizerFor(Type modelType);
    }
}
