using System;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Blazor.Localization
{
    /// <summary>
    /// Default <see cref="IAttributeMessageLocalizer"/> implementation that delegates to the
    /// <see cref="LocalizerExtensions.CreateFallbackLocalizer"/> extension on the registered
    /// <see cref="IStringLocalizerFactory"/>. Same lookup chain as the MVC pipeline's
    /// <c>UseToolkitAttributeTranslator</c>, just exposed as a DI-injectable service for Blazor.
    /// </summary>
    internal sealed class AttributeMessageLocalizer : IAttributeMessageLocalizer
    {
        private readonly IStringLocalizerFactory factory;
        private readonly AttributeTranslationOptions options;

        public AttributeMessageLocalizer(IStringLocalizerFactory factory, AttributeTranslationOptions options)
        {
            this.factory = factory;
            this.options = options;
        }

        public IStringLocalizer GetLocalizerFor(Type modelType)
            => factory.CreateFallbackLocalizer(modelType, options);
    }
}
