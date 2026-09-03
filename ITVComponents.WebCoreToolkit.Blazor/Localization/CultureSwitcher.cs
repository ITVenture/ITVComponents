using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ITVComponents.WebCoreToolkit.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Blazor.Localization
{
    /// <summary>
    /// Default <see cref="ICultureSwitcher"/> for a Blazor host running <c>UseCulturePath()</c>.
    /// </summary>
    public sealed class CultureSwitcher : ICultureSwitcher
    {
        private readonly NavigationManager navigation;
        private readonly IOptions<RequestLocalizationOptions> localizationOptions;
        private readonly ILogger<CultureSwitcher> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CultureSwitcher"/> class.
        /// </summary>
        /// <param name="navigation">the navigation manager of the current circuit</param>
        /// <param name="localizationOptions">the request-localization configuration, source of the offered languages</param>
        /// <param name="logger">a logger for the case that the current address can not be determined</param>
        public CultureSwitcher(NavigationManager navigation, IOptions<RequestLocalizationOptions> localizationOptions,
            ILogger<CultureSwitcher> logger)
        {
            this.navigation = navigation;
            this.localizationOptions = localizationOptions;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public IReadOnlyList<CultureInfo> AvailableCultures
            => (localizationOptions.Value.SupportedUICultures ?? new List<CultureInfo>())
                .Where(c => !string.IsNullOrEmpty(c?.Name))
                .ToList();

        /// <inheritdoc/>
        public string CurrentCulture
        {
            get
            {
                // The URL is asked first: it is what the visitor sees and what a bookmark carries, and it
                // is the only source that can say "this page is explicitly French" rather than "French was
                // negotiated". Only when the path carries no prefix does the negotiated culture answer.
                var path = CurrentPath();
                return CulturePath.TryRead(path, out var culture, out _)
                    ? culture
                    : CultureInfo.CurrentUICulture?.Name;
            }
        }

        /// <inheritdoc/>
        public string BuildUrlFor(string culture)
        {
            var uri = CurrentUri();
            if (uri == null)
            {
                return CulturePath.Replace("/", culture);
            }

            return CulturePath.Replace(uri.AbsolutePath, culture) + uri.Query + uri.Fragment;
        }

        /// <inheritdoc/>
        public void SwitchTo(string culture)
        {
            // forceLoad is not optional here. The base href of the new page differs from the current one,
            // and the target lies outside the base-URI space this circuit was built for - a soft navigation
            // would either be refused or leave the circuit pointing at a base it no longer has. The reload
            // is also what makes the new language take effect everywhere at once, including in the strings
            // that were already rendered.
            navigation.NavigateTo(BuildUrlFor(culture), forceLoad: true);
        }

        /// <summary>
        /// The path of the address currently showing, or "/" when it can not be determined.
        /// </summary>
        private string CurrentPath() => CurrentUri()?.AbsolutePath ?? "/";

        /// <summary>
        /// The address currently showing. Null before the navigation manager has been initialized, which
        /// happens in prerender edge cases - the callers fall back to the root rather than throwing, since
        /// a language picker must never be the reason a page fails to render.
        /// </summary>
        private Uri CurrentUri()
        {
            try
            {
                return new Uri(navigation.Uri);
            }
            catch (InvalidOperationException ex)
            {
                // The navigation manager has not been initialized yet. Expected in prerender edge cases,
                // hence Debug and not Error - but not silent: if a picker ever offers the wrong targets,
                // this is the line that says why.
                logger.LogDebug(ex,
                    "CultureSwitcher: the current address is not available yet; language targets are built against the root.");
                return null;
            }
        }
    }
}
