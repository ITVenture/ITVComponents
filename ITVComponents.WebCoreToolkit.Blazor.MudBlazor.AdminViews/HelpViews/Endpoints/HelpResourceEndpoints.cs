using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Endpoints
{
    /// <summary>
    /// The anonymous resource resolver endpoint (<c>GET /help/res/{name}</c>). Resolves a resource name +
    /// culture to a stored file identifier and streams it through the plain <see cref="IHelpResourceStore"/> —
    /// intentionally NOT the plugin file-handler path (which is security-gated and cannot serve anonymous
    /// requests). Range processing is enabled so video resources seek/stream.
    /// </summary>
    public static class HelpResourceEndpoints
    {
        public static IEndpointRouteBuilder MapHelpResourceEndpoints<TContext>(this IEndpointRouteBuilder endpoints)
            where TContext : DbContext, IHelpSystemContext
        {
            endpoints.MapGet(HelpRoutes.ResourcePrefix + "/{name}", HandleAsync<TContext>).AllowAnonymous();
            return endpoints;
        }

        private static async Task<IResult> HandleAsync<TContext>(string name, string? c, HttpContext http, CancellationToken cancellationToken)
            where TContext : DbContext, IHelpSystemContext
        {
            var sp = http.RequestServices;
            var options = sp.GetService<IGlobalSettings<HelpSystemOptions>>()?.ValueOrDefault ?? new HelpSystemOptions();
            if (!options.Enabled)
            {
                return Results.NotFound();
            }

            if (!options.AnonymousResourceAccess && !(http.User?.Identity?.IsAuthenticated ?? false))
            {
                return Results.Unauthorized();
            }

            var requestedCulture = string.IsNullOrWhiteSpace(c) ? CultureInfo.CurrentUICulture.Name : c;

            var factory = sp.GetRequiredService<IDbContextFactory<TContext>>();
            string? fileIdentifier;
            await using (var db = await factory.CreateDbContextAsync(cancellationToken))
            {
                var resourceId = await db.HelpResources.AsNoTracking()
                    .Where(r => r.Name == name)
                    .Select(r => (int?)r.HelpResourceId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (resourceId == null)
                {
                    return Results.NotFound();
                }

                var files = await db.HelpResourceFiles.AsNoTracking()
                    .Where(f => f.HelpResourceId == resourceId.Value)
                    .Select(f => new { f.Culture, f.FileIdentifier })
                    .ToListAsync(cancellationToken);

                var culture = HelpCulture.Resolve(requestedCulture, files.Select(f => f.Culture));
                fileIdentifier = files.FirstOrDefault(f => f.Culture == culture)?.FileIdentifier;
            }

            if (string.IsNullOrEmpty(fileIdentifier))
            {
                return Results.NotFound();
            }

            var store = sp.GetRequiredService<IHelpResourceStore>();
            var content = await store.OpenAsync(fileIdentifier, cancellationToken);
            if (content == null)
            {
                return Results.NotFound();
            }

            // Inline stream (not an attachment) so images/videos render in place; range processing for seeking.
            return Results.Stream(content.Content, content.ContentType ?? "application/octet-stream",
                enableRangeProcessing: true);
        }
    }
}
