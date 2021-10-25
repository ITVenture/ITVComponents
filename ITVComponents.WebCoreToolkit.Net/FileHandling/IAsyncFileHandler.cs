using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ITVComponents.WebCoreToolkit.Net.FileHandling
{
    /// <summary>
    /// Implements an up- and downloader component for the ItvFileUpload endPoint
    /// </summary>
    public interface IAsyncFileHandler
    {
        /// <summary>
        /// Provides a list of Permissions that a user must have any of, to perform a specific task
        /// </summary>
        /// <param name="reason">the reason why this component is being invoked</param>
        /// <returns>a list of required permissions</returns>
        string[] PermissionsForReason(string reason);

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="ms">a model-state dictionary that is used to reflect eventual invalidities back the the file-endpointhandler</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        /// <param name="verifyNextedFile">callback that allows a File-Handler to process nested files</param>
        Task AddFile(string name, byte[] content, ModelStateDictionary ms, IIdentity uploadingIdentity, Func<string, byte[], bool> verifyNextedFile);

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="uploadHint">a hint that helps the Uploader-module to decide what to do with the uploaded file</param>
        /// <param name="ms">a model-state dictionary that is used to reflect eventual invalidities back the the file-endpointhandler</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        Task AddFile(string name, byte[] content, string uploadHint, ModelStateDictionary ms, IIdentity uploadingIdentity);

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
        Task<AsyncReadFileResult> ReadFile(string fileIdentifier, IIdentity downloadingIdentity);
    }
}
