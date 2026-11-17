using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Globalization
{
    /// <summary>
    /// Gives every cookie authentication scheme (Identity's application, external and two-factor cookies
    /// included) a <see cref="CulturePathCookieBuilder"/>, so the culture prefix never ends up in a cookie
    /// path. Registered by <c>AddCulturePath()</c>.
    /// </summary>
    internal sealed class CulturePathCookieSetup : IPostConfigureOptions<CookieAuthenticationOptions>
    {
        private readonly ILogger<CulturePathCookieSetup> logger;

        public CulturePathCookieSetup(ILogger<CulturePathCookieSetup> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc />
        public void PostConfigure(string name, CookieAuthenticationOptions options)
        {
            if (options.Cookie is CulturePathCookieBuilder)
            {
                return;
            }

            if (options.Cookie.Path != null)
            {
                logger.LogDebug("CulturePath: the cookie of the authentication scheme '{Scheme}' has the explicit path '{Path}'; it is left as it is.", name, options.Cookie.Path);
                return;
            }

            if (options.Cookie.GetType() != typeof(RequestPathBaseCookieBuilder))
            {
                // A builder of its own may compute its path in its own way; replacing it would throw that away.
                logger.LogWarning("CulturePath: the authentication scheme '{Scheme}' uses the cookie builder {Builder}, which is left as it is. Under a culture prefix its cookie path may contain the prefix, and signing out there would then miss the cookie written without it. Set Cookie.Path explicitly to avoid that.",
                    name, options.Cookie.GetType().FullName);
                return;
            }

            options.Cookie = new CulturePathCookieBuilder(options.Cookie);
            if (options.EventsType != null)
            {
                logger.LogDebug("CulturePath: the authentication scheme '{Scheme}' takes its events from {EventsType}; cookies written under a culture prefix before this version are not cleaned up on sign-out.", name, options.EventsType.FullName);
                return;
            }

            options.Events ??= new CookieAuthenticationEvents();
            var previous = options.Events.OnSigningOut;
            options.Events.OnSigningOut = async context =>
            {
                if (previous != null)
                {
                    await previous(context);
                }

                (context.Options.Cookie as CulturePathCookieBuilder)?.DeleteCookieUnderCulturePrefix(context);
            };
        }
    }
}
