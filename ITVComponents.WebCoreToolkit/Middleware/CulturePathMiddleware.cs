using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Globalization;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Middleware
{
    /// <summary>
    /// Recognizes the culture prefix <c>/c/{culture}</c> at the beginning of a request path, stashes the
    /// culture in <see cref="HttpContext.Items"/> and moves the two segments from <c>Request.Path</c> to
    /// <c>Request.PathBase</c> - the same trick the shared-asset and the tenant prefix use, one level
    /// further out.
    /// <para>
    /// Because of that move the application routes unchanged, and every relative link, redirect and
    /// sub-resource stays in the chosen language on its own: <c>PathBase</c> is what <c>IUrlHelper</c>
    /// prepends and what a Blazor <c>&lt;base href&gt;</c> is built from. Leaving the prefix means leaving
    /// that space, which is a full page load - so a language can not wander off silently, and switching one
    /// is a navigation and nothing else.
    /// </para>
    /// <para>
    /// Register it as the FIRST middleware - before <c>UseSharedAssetPath</c>, <c>UseStaticFiles</c>,
    /// <c>UseRequestLocalization</c>, <c>UseAuthentication</c>, <c>UseRouting</c> and
    /// <c>UseTenantPathPrefix</c>. That order is what lets everything downstream stay exactly as it was: by
    /// the time the asset middleware looks at the path, the asset segment leads it again, and a Blazor
    /// request arrives at <c>/_blazor</c> instead of <c>/c/de-CH/_blazor</c>.
    /// </para>
    /// <para>
    /// The culture itself is applied by <see cref="CulturePathRequestCultureProvider"/> inside
    /// <c>UseRequestLocalization()</c>; this middleware only reads it off the URL. Both halves are needed -
    /// see <c>UseCulturePath()</c>, which wires them together.
    /// </para>
    /// </summary>
    public sealed class CulturePathMiddleware
    {
        /// <summary>
        /// Guards the "the culture provider is not registered" message: that is a startup configuration and
        /// does not change at runtime, so it is worth exactly one line per process.
        /// </summary>
        private static int providerMissingWarned;

        private readonly RequestDelegate next;
        private readonly IOptions<CulturePathOptions> options;
        private readonly IOptions<RequestLocalizationOptions> localizationOptions;
        private readonly ILogger<CulturePathMiddleware> logger;

        /// <summary>
        /// The cultures the prefix may select, keyed case-insensitively by their name and mapping to the
        /// spelling the localization is configured with. Built once - the configuration behind it can not
        /// change while the process runs.
        /// </summary>
        private Dictionary<string, string> supportedCultures;

        /// <summary>
        /// Initializes a new instance of the <see cref="CulturePathMiddleware"/> class.
        /// </summary>
        /// <param name="next">the next middleware in the pipeline</param>
        /// <param name="options">the culture-path configuration</param>
        /// <param name="localizationOptions">the request-localization configuration, source of the supported cultures</param>
        /// <param name="logger">a logger for prefixes that could not be used</param>
        public CulturePathMiddleware(RequestDelegate next, IOptions<CulturePathOptions> options,
            IOptions<RequestLocalizationOptions> localizationOptions, ILogger<CulturePathMiddleware> logger)
        {
            this.next = next;
            this.options = options;
            this.localizationOptions = localizationOptions;
            this.logger = logger;
        }

        /// <summary>
        /// Runs the middleware for the given request.
        /// </summary>
        /// <param name="context">the current request</param>
        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "/";
            if (!CulturePath.TryRead(path, out var requested, out var prefix))
            {
                if (string.Equals(CulturePath.FirstSegment(path), CulturePath.SegmentName, StringComparison.OrdinalIgnoreCase))
                {
                    // The segment name is reserved for this purpose, so a path leading with it and NOT
                    // carrying a culture behind it is either a typo in a link or a tenant that took the
                    // reserved name. Passing it on is the right answer - it may well be a legitimate page -
                    // but it is also where a "why is my link not translated" question begins, so it says so.
                    logger.LogWarning(
                        "CulturePath: {Path} starts with the culture segment '{Segment}', but what follows does not have the shape of a culture. The path is passed on unchanged and is resolved as an ordinary page path.",
                        path, CulturePath.SegmentName);
                }

                await next(context);
                return;
            }

            WarnWhenProviderMissing();

            var resolved = Resolve(requested);
            if (resolved != null)
            {
                context.Items[Global.CulturePathCultureItemKey] = resolved;
            }
            else
            {
                // The prefix is stripped anyway: the rest of the URL is a valid path and has to keep
                // working, and a link that was sent out with a language the host has meanwhile dropped
                // should degrade to the default language, not to a 404. Which is exactly why it must not
                // be silent - from the outside the page simply appears in the wrong language.
                logger.LogWarning(
                    "CulturePath: {Path} asks for the culture '{Culture}', which is not among the supported ones ({Supported}). The prefix is stripped, but the language falls back to cookie/Accept-Language/default.",
                    path, requested, string.Join(", ", SupportedCultures().Values));
            }

            context.Items[Global.CulturePathPrefixItemKey] = prefix;

            // The prefix goes into PathBase exactly as it was written in the URL - not in the resolved
            // spelling. A Blazor base href is built from it, and the browser's URI has to start with it
            // character for character; "/c/DE-ch/…" answered with a base href of "/c/de-CH/" would leave
            // the circuit unable to place its own address.
            context.Request.PathBase = context.Request.PathBase.Add(new PathString(prefix));
            context.Request.Path = path.Length > prefix.Length
                ? new PathString(path.Substring(prefix.Length))
                : new PathString("/");

            await next(context);
        }

        /// <summary>
        /// Indicates whether a culture prefix was recognized for the given request.
        /// </summary>
        /// <param name="context">the current request, may be null</param>
        /// <returns>true when this middleware took a culture prefix off the path</returns>
        public static bool HasRun(HttpContext context)
            => context?.Items.ContainsKey(Global.CulturePathPrefixItemKey) == true;

        /// <summary>
        /// Maps the culture as it was written in the URL onto the spelling the localization is configured
        /// with, or null when it is not supported.
        /// </summary>
        /// <param name="requested">the culture as it appeared in the URL</param>
        /// <returns>the supported culture name, or null</returns>
        private string Resolve(string requested)
            => SupportedCultures().TryGetValue(requested, out var canonical) ? canonical : null;

        /// <summary>
        /// The supported cultures, from <see cref="CulturePathOptions.SupportedCultures"/> when the host
        /// filled that list, otherwise from the supported UI cultures of the request localization - so
        /// there is one list rather than two that can disagree.
        /// </summary>
        private Dictionary<string, string> SupportedCultures()
        {
            var result = supportedCultures;
            if (result != null)
            {
                return result;
            }

            result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var configured = options.Value.SupportedCultures;
            if (configured != null && configured.Count != 0)
            {
                foreach (var culture in configured.Where(c => !string.IsNullOrEmpty(c)))
                {
                    result[culture] = culture;
                }
            }
            else
            {
                var cultures = localizationOptions.Value.SupportedUICultures ?? new List<CultureInfo>();
                foreach (var culture in cultures.Where(c => !string.IsNullOrEmpty(c?.Name)))
                {
                    result[culture.Name] = culture.Name;
                }
            }

            if (result.Count == 0)
            {
                logger.LogError(
                    "CulturePath: neither CulturePathOptions.SupportedCultures nor RequestLocalizationOptions.SupportedUICultures name a culture. No culture prefix can ever be applied; configure the supported cultures of the request localization.");
            }

            supportedCultures = result;
            return result;
        }

        /// <summary>
        /// Says once per process when the prefix is being read but nothing applies it. Without this the
        /// symptom is a URL that looks right and a page that is stubbornly in the wrong language - with
        /// nothing anywhere pointing at the missing provider registration.
        /// </summary>
        private void WarnWhenProviderMissing()
        {
            if (Volatile.Read(ref providerMissingWarned) != 0)
            {
                return;
            }

            var providers = localizationOptions.Value.RequestCultureProviders;
            if (providers != null && providers.OfType<CulturePathRequestCultureProvider>().Any())
            {
                Interlocked.Exchange(ref providerMissingWarned, 1);
                return;
            }

            if (Interlocked.Exchange(ref providerMissingWarned, 1) == 0)
            {
                logger.LogError(
                    "CulturePath: the culture prefix is read off the URL, but no CulturePathRequestCultureProvider is registered in the RequestLocalizationOptions taken from DI, so the language will not be applied. Use services.AddCulturePath(), or - if the host calls UseRequestLocalization() with an options instance of its own - insert the provider there: options.RequestCultureProviders.Insert(0, new CulturePathRequestCultureProvider()).");
            }
        }
    }
}
