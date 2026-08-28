using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.DIServices;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.RequestConservation;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.WebPlugins.ServiceModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Extensions
{

    public static class ServiceProviderExtensions
    {
        /// <summary>
        /// Verifies the User-Permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="requiredPermissions">a list of permissions that are requested for a specific action</param>
        /// <param name="checkOnlyForKnownPermissions">indicates whether to check, if the requested permission is explicitly known. unknown permission requests are ignored</param>
        /// <param name="permissionEstimator">provides the selected permission-estimator back outside</param>
        /// <returns>a value indicating whether the current request is legit</returns>
        public static bool VerifyUserPermissions(this IServiceProvider provider, string[] requiredPermissions, bool checkOnlyForKnownPermissions, out ISecurityRepository securityRepository)
        {
            return provider.VerifyUserPermissions(requiredPermissions, checkOnlyForKnownPermissions,
                out securityRepository, out _);
        }

        /// <summary>
        /// Verifies the User-Permissions for the current user and reports whether a negative answer is due to
        /// a missing permission or simply to there being no authenticated user.
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="requiredPermissions">a list of permissions that are requested for a specific action</param>
        /// <param name="checkOnlyForKnownPermissions">indicates whether to check, if the requested permission is explicitly known. unknown permission requests are ignored</param>
        /// <param name="securityRepository">provides the selected permission-estimator back outside</param>
        /// <param name="isUserAuthenticated">
        /// indicates whether there is an authenticated user at all. A <c>false</c> RESULT together with a
        /// <c>false</c> value here means "nobody is signed in", not "this user lacks the permission" - the
        /// two are different answers, and a caller that knowingly serves anonymous requests (e.g. the plugin
        /// loader for a plugin marked as anonymous) needs to tell them apart before overriding the verdict.
        /// </param>
        /// <returns>a value indicating whether the current request is legit</returns>
        public static bool VerifyUserPermissions(this IServiceProvider provider, string[] requiredPermissions,
            bool checkOnlyForKnownPermissions, out ISecurityRepository securityRepository, out bool isUserAuthenticated)
        {
            var permissionScope = provider.GetService<IPermissionScope>();
            var logger = provider.GetService<ILogger<GenericLogTarget>>();//("ITVComponents.WebCoreToolkit.Extensions.ServiceProviderExtensions");
            var userPerms = provider.GetUserPermissions(out securityRepository, out var isAuthenticated);
            isUserAuthenticated = isAuthenticated;
            // Lokale Kopie: out-Parameter lassen sich in einer lokalen Funktion nicht einfangen.
            var permitter = securityRepository;

            // Die zu pruefenden Namen: der angefragte Name und - wenn ein Mandanten-Prefix gilt - zusaetzlich
            // seine prefix-Fassung. Bei einer known-only-Probe bleiben nur die uebrig, die als Berechtigung
            // ueberhaupt DEFINIERT sind; bleibt danach nichts uebrig, verlangt das Angefragte keine.
            string[] Candidates()
            {
                return (from t in requiredPermissions
                        where permissionScope?.PermissionPrefix != null &&
                              !t.StartsWith(permissionScope.PermissionPrefix, StringComparison.OrdinalIgnoreCase)
                        select $"{permissionScope.PermissionPrefix}{t}").Union(requiredPermissions)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Where(n =>
                        !checkOnlyForKnownPermissions || permitter.Permissions.Any(p =>
                            p.PermissionName.Equals(n, StringComparison.OrdinalIgnoreCase) ||
                            $"{permissionScope?.PermissionPrefix}{p.PermissionName}".Equals(n,
                                StringComparison.OrdinalIgnoreCase))).ToArray();
            }

            if (!isAuthenticated)
            {
                // Ohne angemeldeten Benutzer ist die Antwort nein - auch fuer eine known-only-Probe. Das ist
                // Absicht: Aufrufer verlassen sich darauf, dass diese Pruefung zugleich die Anmeldung
                // sicherstellt. Wer anonym etwas zulassen will, muss das AUSDRUECKLICH tun und erkennt den
                // Fall am mitgelieferten isUserAuthenticated (siehe die Ueberladung oben).
                logger?.LogDebug(
                    $"No authenticated user for [{string.Join(", ", requiredPermissions)}] - denied.");
                return false;
            }

            // Ab hier ist der Benutzer angemeldet.
            {
                // Bootstrap: when enabled, hand the requested names OFF the hot-path to the background registrar
                // (no inline DB write). It coalesces them across requests and writes one batch, avoiding the
                // per-permission write-and-invalidate storm. Skipped for known-only probes (e.g. plugin-name
                // checks), which must not create arbitrary permissions. The toggle is read live from options.
                var autoOptions = provider.GetService<IOptions<Security.AutoPermissionsOptions>>()?.Value;
                var autoRegisterActive = !checkOnlyForKnownPermissions && autoOptions is { Enabled: true };
                if (autoRegisterActive)
                {
                    provider.GetService<Security.IAutoPermissionRegistrar>()?.Enqueue(requiredPermissions);
                }

                var extendedPerms = Candidates();
                logger.LogDebug($"Found {extendedPerms.Length} permissions to check.");
                Array.ForEach(extendedPerms, s => logger.LogDebug(s));
                if (extendedPerms.Length == 0 ||
                    extendedPerms.Any(t => userPerms.Contains(t, StringComparer.OrdinalIgnoreCase)))
                {
                    return true;
                }

                // Auto-registration fast-path: a member of the configured receiver global role is granted every
                // requested permission once the background batch lands, so authorize optimistically instead of
                // making the admin wait for the write -> change-signal -> re-resolve round-trip on a first visit.
                // Only consulted here, on a miss — so it adds no cost once the catalogue is populated.
                if (autoRegisterActive && !string.IsNullOrEmpty(autoOptions.GrantToGlobalRole) &&
                    provider.CurrentUserInGlobalRole(securityRepository, autoOptions.GrantToGlobalRole))
                {
                    logger.LogDebug($"Optimistically granting via auto-register receiver role '{autoOptions.GrantToGlobalRole}'.");
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Verifies the User-Permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="requiredFeatures">a list of permissions that are requested for a specific action</param>
        /// <param name="securityRepository">the security-repository that can be used to perform further security-checks</param>
        /// <returns>a value indicating whether the current request is legit</returns>
        public static bool VerifyActivatedFeatures(this IServiceProvider provider, string[] requiredFeatures, out ISecurityRepository securityRepository)
        {
            var permissionScope = provider.GetService<IPermissionScope>();
            var logger = provider.GetService<ILogger<GenericLogTarget>>();//("ITVComponents.WebCoreToolkit.Extensions.ServiceProviderExtensions");
            var isAuthenticated = (provider.IsLegitSharedAssetPath(out securityRepository, out _, out var denied) || provider.IsUserAuthenticated(out securityRepository, out _)) && !denied;
            if (isAuthenticated)
            {
                string[] features = securityRepository.GetFeatures(permissionScope.PermissionPrefix)
                    .Where(n => n.Enabled).Select(n => n.FeatureName).ToArray();
                return requiredFeatures.Any(f => features.Contains(f, StringComparer.OrdinalIgnoreCase));
            }

            return false;
        }

        /// <summary>
        /// Indicates whether the current user holds the given global role. Used by the auto-registration fast-path;
        /// resolved through the security repository, which surfaces the global roles reached via the user's
        /// (tenant-)roles' global-to-local-role mapping.
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="repository">the security repository resolved for the current request</param>
        /// <param name="globalRoleName">the global role to check membership for</param>
        /// <returns>a value indicating whether the current user effectively holds the global role</returns>
        private static bool CurrentUserInGlobalRole(this IServiceProvider provider, ISecurityRepository repository, string globalRoleName)
        {
            if (repository == null || string.IsNullOrEmpty(globalRoleName) ||
                !provider.IsUserAuthenticated(out _, out var identities))
            {
                return false;
            }

            return identities.Any(i =>
                repository.GetGlobalRoles(i.Labels, i.AuthenticationType)
                    .Contains(globalRoleName, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verifies whether the current user is in a legal context
        /// </summary>
        /// <param name="services">the service-provider that holds all services for the current request</param>
        /// <returns>a value indicating whether the user is valid in the current context</returns>
        public static bool VerifyCurrentUser(this IServiceProvider services)
        {
            services.GetUserPermissions(out _, out var isAuthenticated);
            return isAuthenticated;
        }

        /// <summary>
        /// Verifies the User-Permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="requiredPermissions">a list of permissions that are requested for a specific action</param>
        /// <param name="permissionEstimator">provides the selected permission-estimator back outside</param>
        /// <returns>a value indicating whether the current request is legit</returns>
        public static bool VerifyUserPermissions(this IServiceProvider provider, string[] requiredPermissions, out ISecurityRepository securityRepository)
        {
            return VerifyUserPermissions(provider, requiredPermissions, false, out securityRepository);
        }

        /// <summary>
        /// Verifies the User-Permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="requiredPermissions">a list of permissions that are requested for a specific action</param>
        /// <returns>a value indicating whether the current request is legit</returns>
        public static bool VerifyUserPermissions(this IServiceProvider provider, string[] requiredPermissions)
        {
            return VerifyUserPermissions(provider, requiredPermissions, false, out _);
        }

        /// <summary>
        /// Verifies the User-Permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="requiredPermissions">a list of permissions that are requested for a specific action</param>
        /// <param name="checkOnlyForKnownPermissions">indicates whether to check, if the requested permission is explicitly known. unknown permission requests are ignored</param>
        /// <returns>a value indicating whether the current request is legit</returns>
        public static bool VerifyUserPermissions(this IServiceProvider provider, string[] requiredPermissions, bool checkOnlyForKnownPermissions)
        {
            return VerifyUserPermissions(provider, requiredPermissions, checkOnlyForKnownPermissions, out _);
        }

        /// <summary>
        /// Gets the assigned permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="permissionEstimator">provides the selected permission-estimator back outside</param>
        /// <returns>a list of assigned permissions</returns>
        public static string[] GetUserPermissions(this IServiceProvider provider,
            out ISecurityRepository securityRepository, out bool isAuthenticated)
        {
            string[] permissions = null;
            IdentityInfo[] identities;
            isAuthenticated = (provider.IsLegitSharedAssetPath(out securityRepository, out identities, out var denied) ||
                              provider.IsUserAuthenticated(out securityRepository, out identities)) && !denied;

            if (isAuthenticated)
            {
                var rp = securityRepository;
                permissions = identities.SelectMany(i => rp.GetPermissions(i.Labels, i.AuthenticationType)).Select(n => n.PermissionName)
                    .Distinct()
                    .ToArray();
            }

            return permissions ?? Array.Empty<string>();
        }

        public static string[] GetUserPermissions(this IServiceProvider provider,
            string forScope, out ISecurityRepository securityRepository, out bool isAuthenticated)
        {
            string[] permissions = null;
            IdentityInfo[] identities;
            isAuthenticated = provider.IsUserAuthenticated(forScope, out securityRepository, out identities);
            if (isAuthenticated)
            {
                var rp = securityRepository;
                permissions = identities.SelectMany(i => rp.GetPermissions(i.Labels, forScope, i.AuthenticationType)).Select(n => n.PermissionName)
                    .Distinct()
                    .ToArray();
            }

            return permissions ?? [];
        }

        public static T[] GetUserIds<T>(this IServiceProvider provider, out bool isAuthenticated)
        {
            ISecurityRepository securityRepository;
            IdentityInfo[] identities;
            isAuthenticated = (provider.IsLegitSharedAssetPath(out securityRepository, out identities, out var denied) ||
                               provider.IsUserAuthenticated(out securityRepository, out identities)) && !denied;
            T[] retVal = null;
            if (isAuthenticated)
            {
                var rp = securityRepository;
                retVal = identities.SelectMany(i => rp.GetUserIds<T>(i.Labels, i.AuthenticationType)).Distinct()
                    .ToArray();
            }

            return retVal;
        }

        public static T GetUserId<T>(this IServiceProvider provider, out bool isAuthenticated)
        {
            var tmp = provider.GetUserIds<T>(out isAuthenticated);
            if (!isAuthenticated)
            {
                return default;
            }

            if (tmp.Length == 0)
            {
                // Kein Benutzer, aber authentifiziert - das gibt es wirklich: ein anonymer Besucher mit
                // einem Freigabe-Link ist fuer die Anwendung jemand, hat aber keine Benutzerzeile. Frueher
                // lief das in dieselbe Meldung wie der Mehrdeutigkeitsfall und schickte die Suche damit in
                // die voellig falsche Richtung - nach Benutzer-Mappings, die es gar nicht gibt.
                throw new InvalidOperationException(
                    "This request is authenticated but belongs to no user - typically an anonymous visitor inside a shared asset. Use TryGetUserId and decide what your code does without a user.");
            }

            if (tmp.Length != 1)
            {
                throw new InvalidOperationException("Use GetUserIds in Environment with User-Mappings!");
            }

            return tmp[0];
        }

        /// <summary>
        /// Fragt nach dem einen Benutzer der laufenden Anfrage, ohne dass "es gibt keinen" eine Ausnahme
        /// waere. Genau dafuer gibt es sie: innerhalb einer Freigabe kann eine Anfrage authentifiziert sein
        /// und trotzdem zu keiner Benutzerzeile gehoeren - ein anonymer Besucher mit einem Link ist der
        /// Normalfall, kein Fehler. Wer stempelt, protokolliert oder einen Fremdschluessel setzt, muss das
        /// entscheiden koennen, ohne eine Ausnahme zu fangen.
        /// </summary>
        /// <typeparam name="T">der Typ der Benutzerkennung</typeparam>
        /// <param name="provider">der Service-Provider der laufenden Anfrage</param>
        /// <param name="userId">die Benutzerkennung, oder der Vorgabewert</param>
        /// <param name="isAuthenticated">ob die Anfrage ueberhaupt authentifiziert ist</param>
        /// <returns>true, wenn genau ein Benutzer dahinter steht</returns>
        public static bool TryGetUserId<T>(this IServiceProvider provider, out T userId, out bool isAuthenticated)
        {
            var tmp = provider.GetUserIds<T>(out isAuthenticated);
            userId = default;
            if (!isAuthenticated || tmp == null || tmp.Length != 1)
            {
                return false;
            }

            userId = tmp[0];
            return true;
        }

        public static ISecurityRepository GetAssetSecurityRepository(this IServiceProvider services, ISecurityRepository decorated)
        {
            var userProvider = services.GetService<IContextUserProvider>();
            var decorator = new SecurityRepository();
            decorator.PushRepo(decorated);

            // Die Frage "laeuft diese Anfrage in einer Freigabe?" wird bei JEDEM Zugriff neu gestellt und
            // nicht hier einmal beantwortet. Hier ist es dafuer zu frueh: der Prinzipal einer Freigabe
            // entsteht mitten in der Pipeline, und beim anonymen Zugriff faellt diese Fabrik sogar in die
            // Anmeldung selbst hinein - deren Schema braucht ein Repository, um das Zugangs-Token zu
            // entschluesseln. Eine Entscheidung von hier waere also zwangslaeufig die von vorher.
            //
            // Massgeblich ist allein der Mandant der Freigabe: er wird gesetzt, sobald eine gilt. Rechte
            // und Features sind die Nutzlast und duerfen leer sein - eine Vorlage, die nur Rechte gewaehrt
            // oder gar nichts, weil die Seite ohnehin offen ist, ist der Normalfall.
            var lateView = new LateAssetView(userProvider);
            decorator.UseLateView(lateView.Resolve);

            return decorator;
        }

        /// <summary>
        /// Indicates whether the current context runs inside a shared asset that is valid for the requested
        /// location, and - if so - pushes the asset-scoped security repository on top of the stack.
        /// <para>
        /// Host-neutral: the asset comes from <see cref="ISharedAssetContext"/> (path segment, circuit route
        /// data or the deprecated query), not from the query string of a live HTTP request, so this works
        /// inside a Blazor circuit too.
        /// </para>
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="securityRepository">the asset-scoped repository, when one applies</param>
        /// <param name="identities">the identities the asset was granted to</param>
        /// <param name="denied">true when an asset was named but does not apply to the requested location</param>
        /// <returns>true when the current request legitimately runs inside a shared asset</returns>
        public static bool IsLegitSharedAssetPath(this IServiceProvider provider,
            out ISecurityRepository securityRepository, out IdentityInfo[] identities, out bool denied)
        {
            denied = false;
            securityRepository = null;
            identities = null;
            var assetProvider = provider.GetService<ISharedAssetAdapter>();
            var assetContext = provider.GetService<ISharedAssetContext>();
            var userProvider = provider.GetService<IContextUserProvider>();
            if (assetContext?.HasAsset != true || userProvider?.User == null
                || !userProvider.User.HasClaim(n => n.Type == ClaimTypes.FixedUserScope))
            {
                return false;
            }

            var assetKey = assetContext.AssetKey;
            var userScope = userProvider.User.Claims.First(n => n.Type == ClaimTypes.FixedUserScope).Value;
            // Die kanonische Form: ohne Asset-Abschnitt und ohne Mandanten - der Pfad, wie ihn die Route
            // sieht. Roh weitergereicht wuerde derselbe Vergleich in MVC gegen einen Pfad MIT
            // Mandantensegment laufen und in Blazor gegen einen ohne.
            var requestPath = SharedAssetPath.Canonicalize(userProvider.RequestPath, assetContext.Segment,
                provider.GetService<IPermissionScope>()?.PermissionPrefix);
            if (assetProvider != null && !(denied = !assetProvider.VerifyRequestLocation(requestPath, assetKey, userScope, userProvider.User)))
            {
                identities = (from t in userProvider.User.Identities where t.IsAuthenticated select new IdentityInfo{Labels=new []{t.Name}, AuthenticationType=t.AuthenticationType}).ToArray();
                var tmp = provider.GetService<ISecurityRepository>();
                if (tmp is not SecurityRepository seco)
                {
                    throw new InvalidOperationException(
                        "SecurityRepository is required to make this work! Use GetAssetSecurityRepository in your ISecurityRepository dependency injection call.");
                }

                if (seco.Current is not AssetSecurityRepository)
                {
                    seco.PushRepo(new AssetSecurityRepository(userProvider.User, seco.Current, assetProvider.GetAssetInfo(assetKey, userProvider.User)));
                }
                securityRepository = seco;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Indicates whether the current logged-in user is considered authenticated
        /// </summary>
        /// <param name="provider">the service-provider for the current http-context</param>
        /// <param name="securityRepository">the security-context responsible for all authorization-tasks</param>
        /// <param name="labels">the user-labels of the current user</param>
        /// <param name="authType">the authentication-type that was used to log this user in</param>
        /// <returns>a value indicating whether the current user is correlctly authenticated</returns>
        public static bool IsUserAuthenticated(this IServiceProvider provider, out ISecurityRepository securityRepository, out IdentityInfo[] identities)
        {
            var userProvider = provider.GetService<IContextUserProvider>();
            var userMapper = provider.GetService<IUserNameMapper>();
            var currentUser = userProvider.User;
            identities = (from t in currentUser.Identities
                where t.IsAuthenticated
                select new IdentityInfo
                    { AuthenticationType = t.AuthenticationType, Labels = userMapper.GetUserLabels(t) }).ToArray();
            var rp= securityRepository = provider.GetService<ISecurityRepository>();
            return identities.Any(a => rp.IsAuthenticated(a.Labels, a.AuthenticationType));
        }

        /// <summary>
        /// Indicates whether the current logged-in user is considered authenticated
        /// </summary>
        /// <param name="provider">the service-provider for the current http-context</param>
        /// <param name="forScope">the scope, for which to check whether the user is authenticated</param>
        /// <param name="securityRepository">the security-context responsible for all authorization-tasks</param>
        /// <param name="labels">the user-labels of the current user</param>
        /// <param name="authType">the authentication-type that was used to log this user in</param>
        /// <returns>a value indicating whether the current user is correlctly authenticated</returns>
        public static bool IsUserAuthenticated(this IServiceProvider provider, string forScope, out ISecurityRepository securityRepository, out IdentityInfo[] identities)
        {
            var userProvider = provider.GetService<IContextUserProvider>();
            var userMapper = provider.GetService<IUserNameMapper>();
            var currentUser = userProvider.User;
            identities = (from t in currentUser.Identities
                where t.IsAuthenticated
                select new IdentityInfo
                    { AuthenticationType = t.AuthenticationType, Labels = userMapper.GetUserLabels(t) }).ToArray();
            var rp = securityRepository = provider.GetService<ISecurityRepository>();
            return identities.Any(a => rp.IsAuthenticated(a.Labels, forScope, a.AuthenticationType));
        }

        /// <summary>
        /// Gets the assigned permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <returns>a list of assigned permissions</returns>
        public static string[] GetUserPermissions(this IServiceProvider provider, out bool isAuthenticated)
        {
            return provider.GetUserPermissions(out _, out isAuthenticated);
        }

        public static string[] GetUserPermissions(this IServiceProvider provider, string forScope,
            out bool isAuthenticated)
        {
            return provider.GetUserPermissions(forScope, out _, out isAuthenticated);
        }

        /// <summary>
        /// Stores the relevant data of the current request and puts it into an object. The data can be restored later in a different context.
        /// </summary>
        /// <param name="provider">the services that are available in the current context</param>
        /// <returns>an object that contains relevant data of the current context</returns>
        public static object ConserveRequestData(this IServiceProvider provider, HttpContext context)
        {
            var requestData = new ConservedRequestData
            {
                HttpContext = new ConservedHttpContext(context)
            };
            
            var userProvider = provider.GetService<IContextUserProvider>();
            var permissionScope = provider.GetService<IPermissionScope>();
            if (userProvider != null)
            {
                requestData.User = userProvider.User;
                requestData.RouteData = new Dictionary<string, object>(userProvider.RouteData);
                requestData.RequestPath = userProvider.RequestPath;
            }

            if (permissionScope != null)
            {
                requestData.CurrentScope = permissionScope.PermissionPrefix;
            }

            return requestData;
        }

        public static bool IsBackgroundTaskContext(this IServiceProvider provider)
        {
            var httpBuffer = provider.GetService<IHttpContextAccessor>();
            return httpBuffer?.HttpContext is ConservedHttpContext { IsBackgroundServiceContext: true };
        }

        public static void PrepareEmptyContext(this IServiceProvider provider, out HttpContext executionHttpContext)
        {
            var userProvider = provider.GetService<IContextUserProvider>();
            var permissionScope = provider.GetService<IPermissionScope>();
            var httpBuffer = provider.GetService<IHttpContextAccessor>();
            executionHttpContext = httpBuffer.HttpContext = new EmptyHttpContext() { RequestServices = provider };
            if (userProvider is DefaultContextUserProvider dcup)
            {
                dcup.SetDefaults(new ClaimsPrincipal(), new Dictionary<string, object>(), "");
            }
        }

        /// <summary>
        /// Prepares the current (background) scope like <see cref="PrepareEmptyContext(IServiceProvider, out HttpContext)"/>,
        /// but additionally pins the permission/tenant scope to the given value. This lets a background
        /// service (no HTTP request) run deliberately under a specific tenant: tenant-aware contexts that
        /// read their tenant from the <see cref="IPermissionScope"/> then filter to exactly this tenant.
        /// </summary>
        /// <param name="provider">the service-provider of the current scope</param>
        /// <param name="fixedScope">the tenant/scope to pin (null or empty = no fixed scope)</param>
        /// <param name="executionHttpContext">the created, empty execution HttpContext</param>
        public static void PrepareEmptyContext(this IServiceProvider provider, string fixedScope,
            out HttpContext executionHttpContext)
        {
            PrepareEmptyContext(provider, out executionHttpContext);
            if (provider.GetService<IPermissionScope>() is PermissionScopeBase psb)
            {
                // SetFixedScope ist der interne Seam, den auch PrepareContext(conserved) nutzt: er fixiert
                // den Scope unabhaengig von einem HTTP-Kontext.
                psb.SetFixedScope(fixedScope);
            }
        }

        /// <summary>
        /// Bereitet den aktuellen (Hintergrund-)Scope mit einem <b>authentifizierten</b> synthetischen
        /// Benutzer und optional fixiertem Tenant vor. Anders als <see cref="PrepareEmptyContext(IServiceProvider, string, out HttpContext)"/>
        /// (unauthentifiziert) macht dies <c>FilterAvailable</c> true - damit greifen die tenant-abhaengigen
        /// Query-Filter (die im benutzerfreien Zustand komplett aus sind) und scopen auf <paramref name="fixedScope"/>.
        /// </summary>
        /// <remarks>
        /// Der Benutzer muss NICHT physisch existieren, um die Filterung zu aktivieren (das reicht ein
        /// authentifizierter Principal). Legt der Betreiber spaeter einen echten Benutzer dieses Namens an,
        /// laesst sich der Hintergrundprozess ueber die normalen TenantUser-/Rollen-Zuordnungen mit Rechten
        /// ausstatten - ohne Code-Aenderung und ohne Security-Bypass.
        /// </remarks>
        /// <param name="provider">der Service-Provider des aktuellen Scopes</param>
        /// <param name="userName">der (beliebige) Benutzername des Hintergrundprozesses</param>
        /// <param name="fixedScope">der zu fixierende Tenant/Scope (null/leer = kein fixer Scope)</param>
        /// <param name="executionHttpContext">der erzeugte, leere Ausfuehrungs-HttpContext</param>
        public static void PrepareBackgroundContext(this IServiceProvider provider, string userName,
            string fixedScope, out HttpContext executionHttpContext)
        {
            var userProvider = provider.GetService<IContextUserProvider>();
            var httpBuffer = provider.GetService<IHttpContextAccessor>();
            executionHttpContext = httpBuffer.HttpContext = new EmptyHttpContext { RequestServices = provider };
            if (userProvider is DefaultContextUserProvider dcup)
            {
                // Ein authentifizierter Principal (non-empty authenticationType -> IsAuthenticated == true).
                // Der Benutzer muss nicht in der DB existieren; das aktiviert nur FilterAvailable.
                var identity = new ClaimsIdentity("ITVBackgroundProcess");
                if (!string.IsNullOrEmpty(userName))
                {
                    identity.AddClaim(new Claim(identity.NameClaimType, userName));
                }

                dcup.SetDefaults(new ClaimsPrincipal(identity), new Dictionary<string, object>(), "");
            }

            if (provider.GetService<IPermissionScope>() is PermissionScopeBase psb)
            {
                psb.SetFixedScope(fixedScope);
            }
        }

        /// <summary>
        /// Prepares the current scope to values that were conserved before from a different context
        /// </summary>
        /// <param name="provider">the service-provider that holds all dependencies</param>
        /// <param name="conservedRequestData">the previously conserved context data</param>
        public static void PrepareContext(this IServiceProvider provider, object conservedRequestData, out HttpContext restoredHttpContext)
        {
            if (conservedRequestData is not ConservedRequestData crd)
            {
                throw new InvalidOperationException(
                    "An object that was generated using the ConserveRequestData method is required");
            }

            var userProvider = provider.GetService<IContextUserProvider>();
            var permissionScope = provider.GetService<IPermissionScope>();
            var httpBuffer = provider.GetService<IHttpContextAccessor>();
            restoredHttpContext = httpBuffer.HttpContext = crd.HttpContext;
            crd.HttpContext.RequestServices = provider;
            if (userProvider is DefaultContextUserProvider dcup)
            {
                dcup.SetDefaults(crd.User, crd.RouteData, crd.RequestPath);
            }

            if (permissionScope is PermissionScopeBase psb)
            {
                psb.SetFixedScope(crd.CurrentScope);
            }
        }

        public static IObjectProvider GetObjectProvider(this IServiceProvider serviceProvider, string objectName,
            Func<IObjectProvider> defaultProvider)
        {
            IObjectProvider retVal;
            var mc = serviceProvider.GetService<IMemoryCache>();
            if (!(mc?.TryGetValue<IObjectProvider>(objectName, out retVal) ?? false))
            {
                if (mc == null)
                {
                    retVal = defaultProvider?.Invoke();
                }
                else
                {
                    lock (mc)
                    {
                        mc.Set(objectName, retVal = new TimedObjectProvider(),
                            DateTimeOffset.Now.AddDays(1));
                    }
                }
            }

            return retVal;
        }

        public static IObjectProvider GetObjectProvider(this IServiceProvider serviceProvider, string scopeName)
        {
            var objectName = $"{scopeName}_WPHObjects";
            return serviceProvider.GetObjectProvider(objectName, () => new DummyObjectProvider());
        }
    }
}
