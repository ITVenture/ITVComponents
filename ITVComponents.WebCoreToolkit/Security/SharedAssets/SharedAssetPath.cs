using System;
using System.Text;
using ITVComponents.WebCoreToolkit.Globalization;
using Microsoft.AspNetCore.WebUtilities;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// The single place that knows how a shared asset appears in a URL. Both the middleware that strips the
    /// segment and everything that builds links use these methods, so stripping and link-building can not
    /// drift apart.
    /// <para>
    /// Form: <c>/~{Base64Url(AssetKey)}[.{AccessToken}]/{tenant}/rest</c> — the segment is always the FIRST
    /// one, in front of the tenant, in every host flavour. It answers "who are you and what may you do",
    /// which precedes "where are you": the tenant is a side effect of the asset (it carries its owner in
    /// <see cref="AssetInfo.UserScopeName"/>), not the other way round. Putting it first also means a single
    /// middleware registration at the very start of the pipeline and one canonical URL shape for MVC and
    /// Blazor alike.
    /// </para>
    /// </summary>
    public static class SharedAssetPath
    {
        /// <summary>
        /// Separates the asset key from the anonymous access-token inside the segment. Neither Base64Url
        /// alphabet contains it, so it can not appear in either half.
        /// </summary>
        private const char TokenSeparator = '.';

        /// <summary>
        /// Unterscheidet ein Ad-hoc-Ticket von einer gespeicherten Freigabe. Ein Zeichen mehr, und der
        /// Parser weiss, ob er ueberhaupt in die Datenbank muss.
        /// </summary>
        public const string TicketMarker = "!";

        /// <summary>
        /// Builds the path segment (without leading slash) for the given asset key and optional anonymous
        /// access token.
        /// </summary>
        /// <param name="assetKey">the key of the shared asset</param>
        /// <param name="accessToken">the anonymous access token, or null for a link aimed at signed-in recipients</param>
        /// <returns>the segment, e.g. <c>~QWJjZGVm</c></returns>
        public static string BuildSegment(string assetKey, string accessToken = null)
        {
            if (string.IsNullOrEmpty(assetKey))
            {
                throw new ArgumentNullException(nameof(assetKey));
            }

            var encodedKey = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(assetKey));
            return !string.IsNullOrEmpty(accessToken)
                ? $"{Global.SharedAssetPathMarker}{encodedKey}{TokenSeparator}{accessToken}"
                : $"{Global.SharedAssetPathMarker}{encodedKey}";
        }

        /// <summary>
        /// Indicates whether the given segment is marked as a shared-asset segment. Cheap enough to run on
        /// every request; a real parse only follows when this is true.
        /// </summary>
        /// <param name="segment">a single path segment, without slashes</param>
        /// <returns>true when the segment carries the asset marker</returns>
        public static bool IsAssetSegment(string segment)
            => !string.IsNullOrEmpty(segment)
               && segment.StartsWith(Global.SharedAssetPathMarker, StringComparison.Ordinal)
               && segment.Length > Global.SharedAssetPathMarker.Length;

        /// <summary>
        /// Baut den Abschnitt eines Ad-hoc-Tickets. Der Mandant steht im Klartext, weil ohne ihn der
        /// Schluessel zum Entschluesseln der Nutzlast nicht bestimmbar waere.
        /// </summary>
        /// <param name="tenantName">der Mandant, dem das Ticket gehoert</param>
        /// <param name="payload">die verschluesselte Nutzlast (Base64Url)</param>
        /// <returns>der Abschnitt, z.B. <c>~!VGVuYW50QQ.eyJ0Ijo…</c></returns>
        public static string BuildTicketSegment(string tenantName, string payload)
        {
            if (string.IsNullOrEmpty(tenantName))
            {
                throw new ArgumentNullException(nameof(tenantName));
            }

            if (string.IsNullOrEmpty(payload))
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var encodedTenant = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(tenantName));
            return $"{Global.SharedAssetPathMarker}{TicketMarker}{encodedTenant}{TokenSeparator}{payload}";
        }

        /// <summary>
        /// Zerlegt einen markierten Abschnitt - gespeicherte Freigabe oder Ad-hoc-Ticket.
        /// </summary>
        /// <param name="segment">der Abschnitt, mit Marker, ohne Schraegstriche</param>
        /// <param name="parsed">das Ergebnis</param>
        /// <returns>true, wenn der Abschnitt lesbar war</returns>
        public static bool TryParse(string segment, out AssetSegment parsed)
        {
            parsed = null;
            if (!IsAssetSegment(segment))
            {
                return false;
            }

            var payload = segment.Substring(Global.SharedAssetPathMarker.Length);
            if (payload.StartsWith(TicketMarker, StringComparison.Ordinal))
            {
                var body = payload.Substring(TicketMarker.Length);
                var split = body.IndexOf(TokenSeparator);
                if (split <= 0 || split == body.Length - 1)
                {
                    return false;
                }

                string tenant;
                try
                {
                    tenant = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(body.Substring(0, split)));
                }
                catch (FormatException)
                {
                    return false;
                }

                if (tenant.Length == 0)
                {
                    return false;
                }

                parsed = new AssetSegment
                {
                    Raw = segment,
                    Kind = AssetSegmentKind.Ticket,
                    TenantName = tenant,
                    Payload = body.Substring(split + 1)
                };
                return true;
            }

            if (!TryParseSegment(segment, out var assetKey, out var accessToken))
            {
                return false;
            }

            parsed = new AssetSegment
            {
                Raw = segment,
                Kind = AssetSegmentKind.StoredAsset,
                AssetKey = assetKey,
                AccessToken = accessToken
            };
            return true;
        }

        /// <summary>
        /// Parses a marked segment back into asset key and (optional) access token.
        /// <b>Nur fuer gespeicherte Freigaben</b> - ein Ticket-Abschnitt ergibt hier false. Der
        /// allgemeine Weg ist <see cref="TryParse"/>.
        /// </summary>
        /// <param name="segment">the segment, with marker, without slashes</param>
        /// <param name="assetKey">the decoded asset key</param>
        /// <param name="accessToken">the anonymous access token, or null when the segment carries none</param>
        /// <returns>true when the segment could be parsed; false for a malformed segment</returns>
        public static bool TryParseSegment(string segment, out string assetKey, out string accessToken)
        {
            assetKey = null;
            accessToken = null;
            if (!IsAssetSegment(segment))
            {
                return false;
            }

            var payload = segment.Substring(Global.SharedAssetPathMarker.Length);
            if (payload.StartsWith(TicketMarker, StringComparison.Ordinal))
            {
                // Ein Ticket traegt keinen Schluessel - wer hier landet, hat den falschen Weg genommen.
                return false;
            }

            var separator = payload.IndexOf(TokenSeparator);
            var rawKey = separator < 0 ? payload : payload.Substring(0, separator);
            var rawToken = separator < 0 ? null : payload.Substring(separator + 1);
            if (rawKey.Length == 0)
            {
                return false;
            }

            try
            {
                assetKey = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(rawKey));
            }
            catch (FormatException)
            {
                // A segment that starts with the marker but does not decode is not a page path either - the
                // caller answers 404. Logged there, where the request is known.
                return false;
            }

            if (assetKey.Length == 0)
            {
                return false;
            }

            accessToken = string.IsNullOrEmpty(rawToken) ? null : rawToken;
            return true;
        }

        /// <summary>
        /// Gets the first segment of the given path, without slashes. Empty when the path has none.
        /// </summary>
        /// <param name="path">an absolute request path</param>
        /// <returns>the first segment or an empty string</returns>
        public static string FirstSegment(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var trimmed = path.Trim('/');
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            var slash = trimmed.IndexOf('/');
            return slash < 0 ? trimmed : trimmed.Substring(0, slash);
        }

        /// <summary>
        /// Builds the absolute prefix every root-relative link needs in front of it: the asset segment and
        /// the tenant, in that order. Either part may be absent.
        /// </summary>
        /// <param name="assetSegment">the asset segment (with marker, without slashes) or null</param>
        /// <param name="tenant">the current permission scope, or null when the host has none in the path</param>
        /// <returns>the prefix with a leading slash, or an empty string when there is nothing to prefix</returns>
        public static string BuildPrefix(string assetSegment, string tenant)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(assetSegment))
            {
                builder.Append('/').Append(assetSegment);
            }

            if (!string.IsNullOrEmpty(tenant))
            {
                builder.Append('/').Append(tenant);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Reduces a request path to the canonical form the asset's location check works on: without the
        /// asset segment and without the tenant segment - the path as <c>@page</c> / the route template sees
        /// it. Both prefixes are optional and are only removed when they actually lead the path, so the same
        /// method is correct whether the caller passes a raw path (MVC, tenant still in it), an
        /// already-stripped one (Blazor) or a re-assembled <c>PathBase + Path</c>.
        /// </summary>
        /// <param name="path">the path to reduce</param>
        /// <param name="assetSegment">the asset segment of the current request, or null</param>
        /// <param name="tenant">the current permission scope, or null</param>
        /// <returns>the canonical, prefix-free path, always starting with a slash</returns>
        public static string Canonicalize(string path, string assetSegment, string tenant)
        {
            var retVal = string.IsNullOrEmpty(path) ? "/" : path;
            // The culture prefix leads the URL in front of the asset (see CulturePath), so it comes off
            // first. It is stripped unconditionally rather than against a value the caller passes in,
            // because a path that carries it always carries it in the same place - and because the callers
            // that reach here from a Blazor circuit have nothing but the path to go by. A host without the
            // culture prefix is unaffected: nothing leads with the reserved segment there.
            retVal = CulturePath.Strip(retVal);
            retVal = StripLeadingSegment(retVal, assetSegment);
            retVal = StripLeadingSegment(retVal, tenant);
            return retVal;
        }

        private static string StripLeadingSegment(string path, string segment)
        {
            if (string.IsNullOrEmpty(segment))
            {
                return path;
            }

            var first = FirstSegment(path);
            if (!first.Equals(segment, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            var index = path.IndexOf(first, StringComparison.OrdinalIgnoreCase) + first.Length;
            var rest = path.Substring(index);
            return rest.Length == 0 ? "/" : rest;
        }
    }
}
