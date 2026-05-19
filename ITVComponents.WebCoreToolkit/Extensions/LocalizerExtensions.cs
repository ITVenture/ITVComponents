using System;
using ITVComponents.WebCoreToolkit.Localization;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    /// <summary>
    /// Extension methods around <see cref="IStringLocalizerFactory"/> that integrate with
    /// the Toolkit's <see cref="AttributeTranslationOptions"/>-based topic-key translation.
    /// </summary>
    public static class LocalizerExtensions
    {
        /// <summary>
        /// Creates a string-localizer for the given <paramref name="modelType"/> that resolves
        /// topic-prefixed resource keys (e.g. <c>"ITV:RequiredAttribute_ValidationError"</c>) by
        /// first consulting the per-type localizer and falling back to the topic resource type
        /// registered in <paramref name="options"/>.
        /// </summary>
        /// <param name="factory">the localizer factory that produces the underlying per-type localizers</param>
        /// <param name="modelType">the type for which to create the primary (per-type) localizer</param>
        /// <param name="options">the configured attribute-translation options that map topic prefixes to resource types</param>
        /// <returns>an <see cref="IStringLocalizer"/> with the configured fallback behaviour</returns>
        public static IStringLocalizer CreateFallbackLocalizer(
            this IStringLocalizerFactory factory,
            Type modelType,
            AttributeTranslationOptions options)
        {
            return new FallbackLocalizer(factory.Create(modelType), factory, options);
        }
    }
}
