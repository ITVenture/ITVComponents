using System;
using System.Security.Claims;
using System.Security.Principal;
using ITVComponents.Plugins;

namespace ITVComponents.WebCoreToolkit.ServiceShared.FileHandling
{
    /// <summary>
    /// Implements an up- and downloader component for the ItvFileUpload endPoint.
    /// Framework-neutral: validation feedback is returned as <see cref="FileOperationResult"/>;
    /// the host edge (MVC / Blazor) maps it onto its own validation system.
    /// </summary>
    public interface IFileHandler : IFileReasonPermissionProvider
    {
        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        /// <param name="verifyNestedFile">callback that allows a File-Handler to process nested files</param>
        /// <returns>the result of the upload-operation</returns>
        FileOperationResult AddFile(string name, byte[] content, IIdentity uploadingIdentity, Func<string, byte[], bool> verifyNestedFile);

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="uploadHint">a hint that helps the Uploader-module to decide what to do with the uploaded file</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        /// <returns>the result of the upload-operation</returns>
        FileOperationResult AddFile(string name, byte[] content, string uploadHint, IIdentity uploadingIdentity);

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="uploadHint">a hint that helps the Uploader-module to decide what to do with the uploaded file</param>
        /// <param name="assetKey">the asset-key of the resource that is being accessed by the calling client</param>
        /// <param name="uploadingPrincipal">the pricipalUser that is uploading the given file</param>
        /// <returns>the result of the upload-operation</returns>
        public FileOperationResult AddFile(string name, byte[] content, string uploadHint, string assetKey,
            ClaimsPrincipal uploadingPrincipal)
        {
            if (string.IsNullOrEmpty(assetKey))
                return AddFile(name, content, uploadHint, uploadingPrincipal.Identity);
            throw new InvalidOperationException("Asset-Access is not supported by this FileHandler-instance");
        }

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="assetKey">the asset-key of the resource that is being accessed by the calling client</param>
        /// <param name="uploadingPrincipal">the pricipalUser that is uploading the given file</param>
        /// <param name="verifyNestedFile">callback that allows a File-Handler to process nested files</param>
        /// <returns>the result of the upload-operation</returns>
        public FileOperationResult AddFile(string name, byte[] content, string assetKey,
            ClaimsPrincipal uploadingPrincipal, Func<string, byte[], bool> verifyNestedFile)
        {
            if (string.IsNullOrEmpty(assetKey))
                return AddFile(name, content, uploadingPrincipal.Identity, verifyNestedFile);
            throw new InvalidOperationException("Asset-Access is not supported by this FileHandler-instance");
        }

        /// <summary>
        /// Reads a file with the given file-identifier.
        /// </summary>
        /// <param name="fileIdentifier">the identifier of the file</param>
        /// <param name="downloadingIdentity">the identity that is downloading the requested file</param>
        /// <returns>a <see cref="FileReadResult"/> describing the requested file; <see cref="FileReadResult.Success"/> is <c>false</c> when the file was not found</returns>
        FileReadResult ReadFile(string fileIdentifier, IIdentity downloadingIdentity);

        /// <summary>
        /// Reads a file with the given file-identifier.
        /// </summary>
        /// <param name="fileIdentifier">the identifier of the file</param>
        /// <param name="downloadingIdentity">the identity that is downloading the requested file</param>
        /// <param name="assetKey">the asset-key of the resource that is being accessed by the calling client</param>
        /// <returns>a <see cref="FileReadResult"/> describing the requested file; <see cref="FileReadResult.Success"/> is <c>false</c> when the file was not found</returns>
        public FileReadResult ReadFile(string fileIdentifier, ClaimsPrincipal downloadingIdentity, string assetKey)
        {
            if (string.IsNullOrEmpty(assetKey))
                return ReadFile(fileIdentifier, downloadingIdentity.Identity);
            throw new InvalidOperationException("Asset-Access is not supported by this FileHandler-instance");
        }
    }
}
