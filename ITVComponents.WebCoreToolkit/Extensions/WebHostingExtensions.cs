using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.FileProviders;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class WebHostingExtensions
    {
        /// <summary>
        /// /Generates a list of dynamic Scripts that result from the provided paths
        /// </summary>
        /// <param name="host">the host that points to the webRoot of the web</param>
        /// <param name="customScripts">the custom scripts that must be selected</param>
        /// <returns>a list of strings that need to be loaded for the current view</returns>
        public static IEnumerable<string> GetCustomScripts(this IWebHostEnvironment host, params string[] customScripts)
        {
            return (from t in customScripts select FindScripts(host, t)).SelectMany(n=> n).Distinct();
        }

        /// <summary>
        /// Selects all scripts within a specific path
        /// </summary>
        /// <param name="host">the host providing access to the webroot directory</param>
        /// <param name="customScript">the current processed script-hint</param>
        /// <returns>a list of string, that result from the provided script-hint</returns>
        private static IEnumerable<string> FindScripts(IWebHostEnvironment host, string customScript)
        {
            string raw = customScript;
            string filter = "^.*\\.js$";
            List<(string Root, string Name)> l = new List<(string Root, string Name)>();
            if (raw.Contains("|"))
            {
                filter = raw.Substring(raw.IndexOf("|") + 1);
                raw = raw.Substring(0, raw.IndexOf("|"));
            }

            l.Add((Root: "", Name: raw));
            for (int i = 0; i < l.Count; i++)
            {
                var rw = l[i].Name;
                if (string.IsNullOrEmpty(l[i].Root))
                {
                    rw = $"{l[i].Root}/{rw}";
                }
                if (host.WebRootFileProvider.GetFileInfo(rw).IsDirectory)
                {
                    l.AddRange(host.WebRootFileProvider.GetDirectoryContents(raw).Where(n => Regex.IsMatch(n.Name, filter)).Select(n => (Root: rw, Name: n.Name)));
                }
                else
                {
                    yield return rw;
                }
            }
        }
    }
}
