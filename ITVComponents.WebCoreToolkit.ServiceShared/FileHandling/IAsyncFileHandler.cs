using System;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.WebCoreToolkit.ServiceShared.FileHandling
{
    /// <summary>
    /// Implements an up- and downloader component for the ItvFileUpload endPoint.
    /// Framework-neutral: validation feedback is returned as <see cref="FileOperationResult"/>;
    /// the host edge (MVC / Blazor) maps it onto its own validation system.
    /// </summary>
    public interface IAsyncFileHandler : IPlugin
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
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        /// <param name="verifyNestedFile">callback that allows a File-Handler to process nested files</param>
        /// <returns>the result of the upload-operation</returns>
        Task<FileOperationResult> AddFile(string name, byte[] content, IIdentity uploadingIdentity, Func<string, byte[], bool> verifyNestedFile);

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="uploadHint">a hint that helps the Uploader-module to decide what to do with the uploaded file</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        /// <returns>the result of the upload-operation</returns>
        Task<FileOperationResult> AddFile(string name, byte[] content, string uploadHint, IIdentity uploadingIdentity);

        /// <summary>
        /// Adds a file to this fileHandler instance
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="uploadHint">a hint that helps the Uploader-module to decide what to do with the uploaded file</param>
        /// <param name="assetKey">the asset-key of the resource that is being accessed by the calling client</param>
        /// <param name="uploadingPrincipal">the pricipalUser that is uploading the given file</param>
        /// <returns>the result of the upload-operation</returns>
        public Task<FileOperationResult> AddFile(string name, byte[] content, string uploadHint, string assetKey,
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
        public Task<FileOperationResult> AddFile(string name, byte[] content, string assetKey,
            ClaimsPrincipal uploadingPrincipal, Func<string, byte[], bool> verifyNestedFile)
        {
            if (string.IsNullOrEmpty(assetKey))
                return AddFile(name, content, uploadingPrincipal.Identity, verifyNestedFile);
            throw new InvalidOperationException("Asset-Access is not supported by this FileHandler-instance");
        }

        /// <summary>
        /// Reads a file with the given file-identifier. The method can alter the Download-Name and set the fileContent
        /// </summary>
        /// <param name="fileIdentifier">the identifier of the file</param>
        /// <param name="downloadingIdentity">the identity that is downloading the requested file</param>
        /// <returns>a value indicating whether the file was found</returns>
        Task<AsyncReadFileResult> ReadFile(string fileIdentifier, IIdentity downloadingIdentity);

        /// <summary>
        /// Reads a file with the given file-identifier. The method can alter the Download-Name and set the fileContent
        /// </summary>
        /// <param name="fileIdentifier">the identifier of the file</param>
        /// <param name="downloadingIdentity">the identity that is downloading the requested file</param>
        /// <param name="assetKey">the asset-key of the resource that is being accessed by the calling client</param>
        /// <returns>a value indicating whether the file was found</returns>
        public Task<AsyncReadFileResult> ReadFile(string fileIdentifier, ClaimsPrincipal downloadingIdentity, string assetKey)
        {
            if (string.IsNullOrEmpty(assetKey))
                return ReadFile(fileIdentifier, downloadingIdentity.Identity);
            throw new InvalidOperationException("Asset-Access is not supported by this FileHandler-instance");
        }
    }
}
