using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DataSync;
using ITVComponents.EFRepo.DataSync.Models;
using ITVComponents.Json;
using ITVComponents.Plugins;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.ServiceShared.FileHandling.Special
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
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        /// <param name="verifyNextedFile">callback that allows a File-Handler to process nested files</param>
        public FileOperationResult AddFile(string name, byte[] content, IIdentity uploadingIdentity, Func<string, byte[], bool> verifyNextedFile)
        {
            throw new InvalidOperationException("uploadHint missing!");
        }

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="uploadHint">a hint that helps the Uploader-module to decide what to do with the uploaded file</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        public FileOperationResult AddFile(string name, byte[] content, string uploadHint, IIdentity uploadingIdentity)
        {
            string fileType = uploadHint;
            changes.AddRange(handler.PerformCompare(name, fileType, content,  uploadingIdentity));
            return FileOperationResult.Ok();
        }

        /// <summary>
        /// Reads a file with the given file-identifier.
        /// </summary>
        /// <param name="fileIdentifier">the identifier of the file</param>
        /// <param name="downloadingIdentity">the identity that is downloading the requested file</param>
        /// <returns>a <see cref="FileReadResult"/> carrying the requested configuration as JSON</returns>
        public FileReadResult ReadFile(string fileIdentifier, IIdentity downloadingIdentity)
        {
            int colon = fileIdentifier.IndexOf(":");
            string fileType = colon != -1 ? fileIdentifier.Substring(0, colon) : fileIdentifier;
            var filterDic = colon != -1 ? ReadFilterDic(fileIdentifier.Substring(colon + 1)) : new Dictionary<string, int>();
            object desc = handler.DescribeConfig(fileType, filterDic, out var name);
            var descString = JsonHelper.ToJson(desc, SerializationTypingMode.NativePolymorphism, null);
            return new FileReadResult
            {
                Success = true,
                FileContent = new MemoryStream(Encoding.UTF8.GetBytes(descString)),
                DownloadName = $"{name}.json",
                ContentType = "application/json",
                FileDownload = true
            };
        }

        /// <summary>
        /// Gets the response for the complete upload process
        /// </summary>
        /// <returns>a response carrying the computed configuration changes as JSON</returns>
        public FileReadResult GetUploadResult()
        {
            var tmp = changes.ToArray();
            changes.Clear();
            var json = JsonHelper.ToJson(tmp, SerializationTypingMode.StaticTyping, null);
            return new FileReadResult
            {
                Success = true,
                FileContent = new MemoryStream(Encoding.UTF8.GetBytes(json ?? string.Empty)),
                ContentType = "application/json"
            };
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
