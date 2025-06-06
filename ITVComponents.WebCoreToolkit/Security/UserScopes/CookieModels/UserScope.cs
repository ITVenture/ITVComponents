using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
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
                var sc = EligibleScopes.First(n => n.ScopeName.Equals(scopeName, StringComparison.OrdinalIgnoreCase));
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
            var sc = EligibleScopes.First(n => n.ScopeName.Equals(currentScope, StringComparison.OrdinalIgnoreCase));
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
            var sc = EligibleScopes.First(n => n.ScopeName.Equals(currentScope, StringComparison.OrdinalIgnoreCase));
            sc.Features = (from t in features.Where(n => n.Enabled)
                join k in KnownFeatures on t.FeatureName equals k.FeatureName
                select k.Id).Distinct().ToArray();
            return GetFeaturesOf(currentScope, true);
        }

        public void SetScopeRefreshed(string currentScope)
        {
            var sc = EligibleScopes.First(n => n.ScopeName.Equals(currentScope, StringComparison.OrdinalIgnoreCase));
            sc.Created = DateTime.Now;
        }

        private Feature[] GetFeaturesOf(string scopeName, bool ignoreDate)
        {
            if (!string.IsNullOrEmpty(scopeName) && KnownFeatures != null)
            {
                var sc = EligibleScopes.First(n => n.ScopeName.Equals(scopeName, StringComparison.OrdinalIgnoreCase));
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
