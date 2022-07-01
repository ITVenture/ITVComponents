using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace ITVComponents.WebCoreToolkit.Net.Handlers
{
    internal static class FileSystemHandler
    {
        /// <summary>
        /// Exposes the local file-system for users with appropriate permissions
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="action">the action to perform (always read-only). accepted values are: list, download, search</param>
        /// <param name="basePath">the path to a directory or file on which to perform the demanded action.</param>
        /// <param name="pattern">if the action is set to search, a pattern can be provided, about what file to search.</param>
        /// <response code="200">a list of files and directories formatted as json for list and search actions</response>
        /// <response code="200">a file-download if download is given as action</response>
        /// <response code="404">when the requested file or directory was not found, or when the user does not have the required permission to list or download.</response>
        public static async Task<IResult> FileSystemAccessWithAuth(HttpContext context, [FromRoute(Name="Action")]string action, [FromQuery(Name="Path")]string basePath,
            [FromQuery(Name="Pattern")]string pattern)
        {
            return await FileSystemAccess(context, true, action, basePath, pattern);
        }

        /// <summary>
        /// Exposes the local file-system for users with appropriate permissions
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="action">the action to perform (always read-only). accepted values are: list, download, search</param>
        /// <param name="basePath">the path to a directory or file on which to perform the demanded action.</param>
        /// <param name="pattern">if the action is set to search, a pattern can be provided, about what file to search.</param>
        /// <response code="200">a list of files and directories formatted as json for list and search actions</response>
        /// <response code="200">a file-download if download is given as action</response>
        /// <response code="404">when the requested file or directory was not found, or when the user does not have the required permission to list or download.</response>
        public static async Task<IResult> FileSystemAccessNoAuth(HttpContext context, [FromRoute(Name="Action")]string action, [FromQuery(Name = "Path")] string basePath,
            [FromQuery(Name = "Pattern")] string pattern)
        {
            return await FileSystemAccess(context, false, action, basePath, pattern);
        }

        private static async Task<IResult> FileSystemAccess(HttpContext context, bool withAuthorization, string action, string basePath, string pattern)
        {
            if (!withAuthorization || context.RequestServices.VerifyUserPermissions(new[] { "BrowseFs" }))
            {
                StringBuilder path = new StringBuilder("/");
                if (!string.IsNullOrEmpty(basePath))
                {
                    path.Append(basePath);
                }

                var pth = path.ToString();
                switch (action.ToLower())
                {
                    case "download":
                        if (File.Exists(pth))
                        {
                            var p = Path.GetFileName(pth);
                            return Results.File(File.ReadAllBytes(pth), "application/octet-stream", p);
                        }

                        break;
                    case "list":
                        if (Directory.Exists(pth))
                        {
                            return Results.Json((from t in Directory.GetFiles(pth)
                                    select new
                                    {
                                        Name = Path.GetFileName(t),
                                        Type = "File",
                                        Path = Path.GetDirectoryName(t)
                                    }).Union((from t in Directory.GetDirectories(pth)
                                    select new
                                    {
                                        Name = Path.GetFileName(t),
                                        Type = "Directory",
                                        Path = Path.GetDirectoryName(t)
                                    }).OrderBy(n => n.Name)).ToArray(), new JsonSerializerOptions());
                        }

                        break;
                    case "search":
                        if (Directory.Exists(pth) && !string.IsNullOrEmpty(pattern))
                        {
                            var directories = new List<string>();
                            var dir = new string[] { pth };
                            var files = new List<string>();
                            while (dir.Length != 0)
                            {
                                foreach (var item in dir)
                                {
                                    try
                                    {
                                        files.AddRange(Directory.GetFiles(item, pattern));
                                        directories.AddRange(Directory.GetDirectories(item));
                                    }
                                    catch (Exception ex)
                                    {
                                    }
                                }

                                dir = directories.ToArray();
                                directories.Clear();
                            }

                            return Results.Json((from t in files
                                select new
                                {
                                    Name = Path.GetFileName(t),
                                    Type = "File",
                                    Path = Path.GetDirectoryName(t)
                                }).ToArray(), new JsonSerializerOptions());
                        }

                        break;
                }
            }

            return Results.NotFound();
        }
    }
}
