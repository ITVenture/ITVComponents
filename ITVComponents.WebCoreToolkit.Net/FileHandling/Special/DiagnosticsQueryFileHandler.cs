using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Tokens;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.FileHandling.Special
{
    public abstract class DiagnosticsQueryFileHandler:IAsyncFileHandler
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
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="ms">a model-state dictionary that is used to reflect eventual invalidities back the the file-endpointhandler</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        /// <param name="verifyNextedFile">callback that allows a File-Handler to process nested files</param>
        public Task AddFile(string name, byte[] content, ModelStateDictionary ms, IIdentity uploadingIdentity, Func<string, byte[], bool> verifyNextedFile)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="uploadHint">a hint that helps the Uploader-module to decide what to do with the uploaded file</param>
        /// <param name="ms">a model-state dictionary that is used to reflect eventual invalidities back the the file-endpointhandler</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        public Task AddFile(string name, byte[] content, string uploadHint, ModelStateDictionary ms, IIdentity uploadingIdentity)
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
            var result = ctx.RunDiagnosticsQuery(query, data).Cast<object>().ToArray();
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
    }
}
