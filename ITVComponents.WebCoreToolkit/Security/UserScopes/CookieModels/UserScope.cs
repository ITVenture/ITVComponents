using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.Security.UserScopes.CookieModels
{
    public class UserScope
    {
        public DateTime Created { get; set; } = DateTime.Now;
        public AuthTypeUserLabels[] UserLabels { get; set; }
        public string ScopeName { get; set; }
        public string[] Permissions
        {
            get
            {
                return GetPermissionsOf(ScopeName);
            }
        }
        public Feature[] Features{get
            {
                return GetFeaturesOf(ScopeName);
            }
        }

        public Feature[] KnownFeatures { get; set; }
        public ScopeInfo[] EligibleScopes { get; set; }
        public Permission[] KnownPermissions { get; set; }

        public Feature[] GetFeaturesOf(string scopeName)
        {
            return GetFeaturesOf(scopeName, false);
        }

        public string[] GetPermissionsOf(string scopeName)
        {
            if (!string.IsNullOrEmpty(scopeName) && KnownPermissions != null)
            {
                var sc = FindScope(scopeName);
                if (sc == null)
                {
                    LogEnvironment.LogEvent(
                        $"No permissions for scope '{scopeName}': it is not among the eligible scopes ({DescribeEligibleScopes()}).",
                        LogSeverity.Warning);
                    return null;
                }

                if (DateTime.Now.Subtract(sc.Created).TotalMinutes < 30 && sc.Permissions != null)
                {
                    return (from t in sc.Permissions
                            join p in KnownPermissions on t equals p.Id
                            select p.PermissionName)
                        .ToArray();
                }
            }

            return null;
        }

        public void UpdateScopePermissions(string currentScope, Models.Permission[] knownPermissions, string[] permissions)
        {
            var tmp = KnownPermissions ?? [];
            var mxid = tmp.Select(n => n.Id).DefaultIfEmpty(0).Max() + 1;
            var nw = (from t in knownPermissions
                join e in tmp on t.PermissionName equals e.PermissionName into lj
                from l in lj.DefaultIfEmpty()
                where l == null
                select new Permission { Id = mxid++, PermissionName = t.PermissionName }).ToArray();
            KnownPermissions = tmp.Concat(nw).ToArray();
            var sc = RequireScope(currentScope, "store the resolved permissions");
            sc.Permissions = (from t in permissions
                join k in KnownPermissions on t equals k.PermissionName
                select k.Id).Distinct().ToArray();
        }

        public Feature[] UpdateScopeFeatures(string currentScope, Models.Feature[] features)
        {
            var tmp = KnownFeatures ?? [];
            var mxid = tmp.Select(n => n.Id).DefaultIfEmpty(0).Max() + 1;
            var nw = (from t in features
                join e in tmp on t.FeatureName equals e.FeatureName into lj
                from l in lj.DefaultIfEmpty()
                where l == null
                select new Feature { Id = mxid++, FeatureName = t.FeatureName, Enabled = false}).ToArray();
            KnownFeatures = tmp.Concat(nw).ToArray();
            var sc = RequireScope(currentScope, "store the resolved features");
            sc.Features = (from t in features.Where(n => n.Enabled)
                join k in KnownFeatures on t.FeatureName equals k.FeatureName
                select k.Id).Distinct().ToArray();
            return GetFeaturesOf(currentScope, true);
        }

        public void SetScopeRefreshed(string currentScope)
        {
            var sc = RequireScope(currentScope, "mark the scope as refreshed");
            sc.Created = DateTime.Now;
        }

        /// <summary>
        /// Looks up one of the eligible scopes by name, or <c>null</c> if the name is not among them.
        /// </summary>
        private ScopeInfo FindScope(string scopeName)
        {
            return EligibleScopes?.FirstOrDefault(n =>
                n.ScopeName.Equals(scopeName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Looks up one of the eligible scopes by name and throws a diagnosable exception if it is not
        /// among them.
        /// </summary>
        /// <remarks>
        /// Every caller here assumes the scope it was handed is an eligible one - an assumption that only
        /// holds as long as whoever resolved the scope checked it against <see cref="EligibleScopes"/>.
        /// This used to be a bare <c>First(...)</c>, so when the assumption broke the result was
        /// "Sequence contains no matching element": no scope, no alternatives, and a stack pointing at
        /// whichever component happened to ask first rather than at the resolution that went wrong.
        /// Naming both turns a dead end into a diagnosis.
        /// </remarks>
        private ScopeInfo RequireScope(string scopeName, string operation)
        {
            var retVal = FindScope(scopeName);
            if (retVal == null)
            {
                throw new InvalidOperationException(
                    $"Unable to {operation}: the scope '{scopeName}' is not among the eligible scopes ({DescribeEligibleScopes()}).");
            }

            return retVal;
        }

        /// <summary>
        /// The eligible scopes as a readable list for log messages and exceptions.
        /// </summary>
        private string DescribeEligibleScopes()
        {
            return EligibleScopes is { Length: > 0 }
                ? string.Join(", ", EligibleScopes.Select(n => n.ScopeName))
                : "none";
        }

        private Feature[] GetFeaturesOf(string scopeName, bool ignoreDate)
        {
            if (!string.IsNullOrEmpty(scopeName) && KnownFeatures != null)
            {
                var sc = FindScope(scopeName);
                if (sc == null)
                {
                    LogEnvironment.LogEvent(
                        $"No features for scope '{scopeName}': it is not among the eligible scopes ({DescribeEligibleScopes()}).",
                        LogSeverity.Warning);
                    return null;
                }

                if ((ignoreDate || DateTime.Now.Subtract(sc.Created).TotalMinutes < 30) && sc.Features != null)
                {
                    return (from p in KnownFeatures
                            join t in sc.Features
                                on p.Id equals t into lj
                            from j in lj.DefaultIfEmpty()
                            select new Feature { FeatureName = p.FeatureName, Id = p.Id, Enabled = j != 0 })
                        .ToArray();
                }
            }

            return null;
        }
    }
}
