using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.WebToolkitOverrides
{
    internal static class TemporaryGrants
    {
        private static ConcurrentDictionary<string, List<string>> grantTable = new ConcurrentDictionary<string, List<string>>();

        private static ConcurrentDictionary<string, string> services = new ConcurrentDictionary<string, string>();

        public static void GrantTemporaryPermission(string service, string permission)
        {
            if (services.TryGetValue(service, out var user))
            {
                var list = grantTable.GetOrAdd(user, s => new List<string>());
                lock (list)
                {
                    LogEnvironment.LogDebugEvent($"Adding Grant {permission} for {user}.",LogSeverity.Report);
                    list.Add(permission);
                }
            }
        }

        public static void RevokeTemporaryPermission(string service, string permission)
        {
            if (services.TryGetValue(service, out var user))
            {
                var list = grantTable.GetOrAdd(user, s => new List<string>());
                lock (list)
                {
                    LogEnvironment.LogDebugEvent($"Revoking Grant {permission} for {user}.",LogSeverity.Report);
                    list.RemoveAll(n => n == permission);
                }
            }
        }

        public static void RegisterService(string serviceName, string userName)
        {
            LogEnvironment.LogDebugEvent($"Registering Service {serviceName} ran by {userName}.",LogSeverity.Report);
            services.TryAdd(serviceName, userName);
        }

        public static void UnRegisterService(string serviceName)
        {
            LogEnvironment.LogDebugEvent($"Removing Service {serviceName}.",LogSeverity.Report);
            services.TryRemove(serviceName, out _);
        }

        public static IEnumerable<string> GetTemporaryPermissions(string[] userLabels)
        {
            List<string[]> allPerms = new List<string[]>();
            foreach (var label in userLabels)
            {
                LogEnvironment.LogDebugEvent($"Collecting extended permissions for {label}.",LogSeverity.Report);
                if (grantTable.TryGetValue(label, out var l))
                {
                    lock (l)
                    {
                        allPerms.Add(l.ToArray());
                    }
                }
            }

            return allPerms.SelectMany(n => n).Distinct();
        }
    }
}
