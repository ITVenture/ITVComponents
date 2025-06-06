using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.Security.UserScopes.Helpers
{
    internal static class UserValidateHelper
    {
        internal static bool IsUserOk(string[] userLabels, string authenticationType, AuthTypeUserLabels[] detectedLabels, string targetScope = null, string currentScope = null)
        {
            var lb = detectedLabels.FirstOrDefault(n => n.AuthenticationType == authenticationType);
            if (lb == null)
            {
                lb = detectedLabels.FirstOrDefault(n => n.AuthenticationType == null);  
            }

            return (string.IsNullOrEmpty(targetScope) || targetScope == currentScope) && lb != null &&
                   lb.UserLabels.Length == userLabels.Length && lb.UserLabels.All(userLabels.Contains);
        }

        internal static bool IsUserOk(AuthTypeUserLabels[] desiredLabels, AuthTypeUserLabels[] detectedLabels,
            string targetScope = null, string currentScope = null)
        {
            return desiredLabels.Any(n =>
                IsUserOk(n.UserLabels, n.AuthenticationType, detectedLabels, targetScope, currentScope));
        }
    }
}
