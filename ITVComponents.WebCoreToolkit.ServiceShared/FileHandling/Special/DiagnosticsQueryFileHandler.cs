using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.ServiceShared.FileHandling.Special
{
    public abstract class DiagnosticsQueryFileHandler : IAsyncFileHandler, IPlugin
    {
        private readonly IServiceProvider services;

        private IDiagnosticsStore repo;

        public DiagnosticsQueryFileHandler(IServiceProvider services)
        {
            this.services = services;
        }

        /// <summary>
        /// Provides the Diagnostics store that was used to get the query-data
        /// </summary>
        protected IDiagnosticsStore Repo => repo ??= services.GetService<IDiagnosticsStore>();

        /// <summary>
        /// Provides the ServiceProvider that can be used to retrieve further services
        /// </summary>
        protected IServiceProvider Services => services;

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Provides a list of Permissions that a user must have any of, to perform a specific task
        /// </summary>
        /// <param name="reason">the reason why this component is being invoked</param>
        /// <returns>a list of required permissions</returns>
        public string[] PermissionsForReason(string reason)
        {
            var query = Repo.GetQuery(reason);
            if (query != null)
            {
                return new string[] { query.Permission };
            }

            return null;
        }

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        public Task<FileOperationResult> AddFile(string name, byte[] content, IIdentity uploadingIdentity, Func<string, byte[], bool> verifyNestedFile)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        public Task<FileOperationResult> AddFile(string name, byte[] content, string uploadHint, IIdentity uploadingIdentity)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reads a file with the given file-identifier. The method can alter the Download-Name and set the fileContent
        /// </summary>
        /// <param name="fileIdentifier">the identifier of the file</param>
        /// <param name="downloadingIdentity">the identity that is downloading the requested file</param>
        /// <returns>a value indicating whether the file was found</returns>
        public async Task<AsyncReadFileResult> ReadFile(string fileIdentifier, IIdentity downloadingIdentity)
        {
            var data = fileIdentifier.DecompressToken<Dictionary<string, string>>();
            var queryName = data["$$QUERYNAME"];
            var area = data["$$QUERYAREA"];
            var ctx = services.ContextForDiagnosticsQuery(queryName, area, out var query);
            var httpContext = services.GetService<IHttpContextAccessor>();
            var result = ctx.RunDiagnosticsQuery(query, httpContext?.HttpContext, data).Cast<object>().ToArray();
            return await MaterializeQueryData(result, queryName, downloadingIdentity);
        }

        /// <summary>
        /// Materializes the QueryData of the requested DiagnoseQuery into the capable file format
        /// </summary>
        /// <param name="data">the result of the diagnose query</param>
        /// <param name="queryName">the name of the query that was executed</param>
        /// <param name="downloadIdentity">the download-identity that was used to request the data</param>
        /// <returns>a file-read result that describes the retrieved data</returns>
        protected abstract Task<AsyncReadFileResult> MaterializeQueryData(object[] data, string queryName, IIdentity downloadIdentity);

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Releases all required resources
        /// </summary>
        public void Dispose()
        {
            repo = null;
            OnDisposed();
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
