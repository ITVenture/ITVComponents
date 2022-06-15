using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ITVComponents.WebCoreToolkit.Net.FileHandling
{
    /// <summary>
    /// Implements an up- and downloader component for the ItvFileUpload endPoint
    /// </summary>
    public interface IFileHandler
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
        void AddFile(string name, byte[] content, ModelStateDictionary ms, IIdentity uploadingIdentity, Func<string, byte[], bool> verifyNextedFile);

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="uploadHint">a hint that helps the Uploader-module to decide what to do with the uploaded file</param>
        /// <param name="ms">a model-state dictionary that is used to reflect eventual invalidities back the the file-endpointhandler</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        void AddFile(string name, byte[] content, string uploadHint, ModelStateDictionary ms, IIdentity uploadingIdentity);

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="uploadHint">a hint that helps the Uploader-module to decide what to do with the uploaded file</param>
        /// <param name="assetKey">the asset-key of the resource that is being accessed by the calling client</param>
        /// <param name="ms">a model-state dictionary that is used to reflect eventual invalidities back the the file-endpointhandler</param>
        /// <param name="uploadingPrincipal">the pricipalUser that is uploading the given file</param>
        public void AddFile(string name, byte[] content, string uploadHint, string assetKey, ModelStateDictionary ms,
            ClaimsPrincipal uploadingPrincipal)
        {
            if (string.IsNullOrEmpty(assetKey))
            {
                AddFile(name, content, uploadHint, ms, uploadingPrincipal.Identity);
            }
            else
            {
                throw new InvalidOperationException("Asset-Access is not supported by this FileHandler-instance");
            }
        }

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="assetKey">the asset-key of the resource that is being accessed by the calling client</param>
        /// <param name="ms">a model-state dictionary that is used to reflect eventual invalidities back the the file-endpointhandler</param>
        /// <param name="uploadingPrincipal">the pricipalUser that is uploading the given file</param>
        /// <param name="verifyNestedFile">callback that allows a File-Handler to process nested files</param>
        public void AddFile(string name, byte[] content, string assetKey, ModelStateDictionary ms,
            ClaimsPrincipal uploadingPrincipal, Func<string, byte[], bool> verifyNestedFile)
        {
            if (string.IsNullOrEmpty(assetKey))
            {
                AddFile(name, content, ms, uploadingPrincipal.Identity, verifyNestedFile);
            }
            else
            {
                throw new InvalidOperationException("Asset-Access is not supported by this FileHandler-instance");
            }
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
        bool ReadFile(string fileIdentifier, IIdentity downloadingIdentity, ref string downloadName, ref string contentType, ref bool fileDownload, out byte[] fileContent);

        /// <summary>
        /// Reads a file with the given file-identifier. The method can alter the Download-Name and set the fileContent
        /// </summary>
        /// <param name="fileIdentifier">the identifier of the file</param>
        /// <param name="downloadingIdentity">the identity that is downloading the requested file</param>
        /// <param name="assetKey">the asset-key of the resource that is being accessed by the calling client</param>
        /// <param name="downloadName">the download-name of the file</param>
        /// <param name="contentType">the content-type that is set in the result-header</param>
        /// <param name="fileDownload">indicates whether the provided file should be served as file-download or as embeddable file-result</param>
        /// <param name="fileContent">the content of the file</param>
        /// <returns>a value indicating whether the file was found</returns>
        public bool ReadFile(string fileIdentifier, ClaimsPrincipal downloadingIdentity, string assetKey,
            ref string downloadName, ref string contentType, ref bool fileDownload, out byte[] fileContent)
        {
            if (string.IsNullOrEmpty(assetKey))
                return ReadFile(fileIdentifier, downloadingIdentity.Identity, ref downloadName, ref contentType, ref fileDownload, out fileContent);
            throw new InvalidOperationException("Asset-Access is not supported by this FileHandler-instance");
        }
    }
}
