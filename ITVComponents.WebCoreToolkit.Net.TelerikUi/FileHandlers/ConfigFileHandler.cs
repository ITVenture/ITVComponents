using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DataSync;
using ITVComponents.EFRepo.DataSync.Models;
using ITVComponents.Helpers;
using ITVComponents.Plugins;
using ITVComponents.Scripting.CScript.Core.Native;
using ITVComponents.WebCoreToolkit.Net.FileHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.FileHandlers
{
    public class ConfigFileHandler:IPlugin, IRespondingFileHandler
    {
        /// <summary>
        /// A regex used to extract filter-instructions from a download-query
        /// </summary>
        private static readonly Regex filterRegex = new Regex(";?(?<column>[_\\w@-]+);(?<value>\\d+)", RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        /// <summary>
        /// Holds all current changes in a list
        /// </summary>
        private List<Change> changes = new List<Change>();

        private IConfigurationHandler handler;

        public ConfigFileHandler(IConfigurationHandler handler)
        {
            this.handler = handler;
        }

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
            return handler.PermissionsForReason(reason);
        }

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="ms">a model-state dictionary that is used to reflect eventual invalidities back the the file-endpointhandler</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        /// <param name="verifyNextedFile">callback that allows a File-Handler to process nested files</param>
        public void AddFile(string name, byte[] content, ModelStateDictionary ms, IIdentity uploadingIdentity, Func<string, byte[], bool> verifyNextedFile)
        {
            throw new InvalidOperationException("uploadHint missing!");
        }

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="uploadHint">a hint that helps the Uploader-module to decide what to do with the uploaded file</param>
        /// <param name="ms">a model-state dictionary that is used to reflect eventual invalidities back the the file-endpointhandler</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        public void AddFile(string name, byte[] content, string uploadHint, ModelStateDictionary ms, IIdentity uploadingIdentity)
        {
            string fileType = uploadHint;
            changes.AddRange(handler.PerformCompare(name, fileType, content,  uploadingIdentity));
        }

        /// <summary>
        /// Reads a file with the given file-identifier. The method can alter the Download-Name and set the fileContent
        /// </summary>
        /// <param name="fileIdentifier">the identifier of the file</param>
        /// <param name="downloadingIdentity">the identity that is downloading the requested file</param>
        /// <param name="downloadName">the download-name of the file</param>
        /// <param name="contentType">the content-type that is set in the result-header</param>
        /// <param name="fileDownload">indicates whether the provided file should be served as file-download or as embeddable file-result</param>
        /// <param name="fileContent">the content of the file</param>
        /// <returns>a value indicating whether the file was found</returns>
        public bool ReadFile(string fileIdentifier, IIdentity downloadingIdentity, ref string downloadName, ref string contentType,
            ref bool fileDownload, out byte[] fileContent)
        {
            int colon = fileIdentifier.IndexOf(":");
            string fileType = colon != -1 ? fileIdentifier.Substring(0, colon) : fileIdentifier;
            var filterDic = colon != -1 ? ReadFilterDic(fileIdentifier.Substring(colon + 1)) : new Dictionary<string, int>();
            object desc = handler.DescribeConfig(fileType, filterDic, out var name);
            var descString = JsonHelper.ToJsonStrongTyped(desc);
            fileContent = Encoding.UTF8.GetBytes(descString);
            downloadName = $"{name}.json";
            contentType = "application/json";
            fileDownload = true;
            return true;
        }

        /// <summary>
        /// Gets the ActionResult for the complete upload process
        /// </summary>
        /// <returns>an action result as a reaction of the given upload request</returns>
        public IResult GetUploadResult()
        {
            var tmp = changes.ToArray();
            changes.Clear();
            return Results.Content(JsonHelper.ToJson(tmp), "application/json");
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            Dispose(true);
            OnDisposed();
        }


        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// reads the provided filter and extracts filter-instructions
        /// </summary>
        /// <param name="substring">the filter-part of the file-description</param>
        /// <returns>a dictionary holding further filter-instructions</returns>
        private IDictionary<string, int> ReadFilterDic(string substring)
        {
            Dictionary<string, int> retVal = new Dictionary<string, int>();
            var m = filterRegex.Matches(substring);
            foreach (Match mx in m)
            {
                retVal.Add(mx.Groups["column"].Value, int.Parse(mx.Groups["value"].Value));
            }

            return retVal;
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
