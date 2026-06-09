using ITVComponents.WebCoreToolkit.ServiceShared.FileHandling;
using ITVComponents.WebCoreToolkit.ServiceShared.Model;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.ServiceShared.Options;

namespace ITVComponents.WebCoreToolkit.ServiceShared.Service
{
    public interface IFileServiceHandler
    {
        /// <summary>
        /// Resolves the <see cref="UploadOptions"/> for an upload-module/reason from the scoped/global settings
        /// providers (framework-neutral). The reason-specific setting <c>{reason}_{module}UploadSettings</c> takes
        /// precedence over the module-wide <c>{module}UploadSettings</c>; an empty result yields the defaults.
        /// </summary>
        /// <param name="uploadModule">the name of the target-upload module</param>
        /// <param name="reason">the reason the upload is performed for</param>
        /// <returns>the resolved upload-options, or the defaults when nothing is configured</returns>
        UploadOptions ResolveUploadOptions(string uploadModule, string reason);

        Task<FileOperationResult> ProcessFileUpload(string uploadModule, string reason, string uploadHint,
            ClaimsPrincipal principal,
            ParsedMultipartFile[] files, UploadOptions options, bool withAuthorization = true, bool hasAsset = false, string assetKey = null);

        /// <summary>
        /// Resolves the named FileHandler-plugin, verifies the download-permissions and reads the requested file —
        /// all framework-neutrally, so MVC and Blazor share the same plugin-handling. The host edge only maps the
        /// returned <see cref="FileOperationResult"/> onto its rendering (MVC <c>IResult</c> / Blazor stream).
        /// </summary>
        /// <param name="downloadModule">the name of the FileHandler-plugin that serves the download</param>
        /// <param name="reason">the reason that drives the required-permission lookup</param>
        /// <param name="fileIdentifier">the identifier passed to the handler's ReadFile</param>
        /// <param name="principal">the principal that is downloading the requested file</param>
        /// <param name="defaultDownloadName">fallback download-name when the handler does not set one</param>
        /// <param name="defaultContentType">fallback content-type when the handler does not set one</param>
        /// <param name="defaultFileDownload">fallback file-download flag when the handler does not set one</param>
        /// <param name="withAuthorization">indicates whether the download is permission-checked</param>
        /// <param name="hasAsset">indicates whether the request is an asset-level access</param>
        /// <param name="assetKey">the asset-key of the resource that is being accessed</param>
        /// <returns>
        /// a <see cref="FileOperationResult"/>: <see cref="FileOperationCode.OkWithContent"/> with the resolved
        /// <see cref="FileOperationResult.ReadResult"/> on success, otherwise NotFound / Forbid / UnAuthorized / BadRequest
        /// </returns>
        Task<FileOperationResult> ProcessFileDownload(string downloadModule, string reason, string fileIdentifier,
            ClaimsPrincipal principal, string defaultDownloadName = null, string defaultContentType = null,
            bool defaultFileDownload = true, bool withAuthorization = true, bool hasAsset = false, string assetKey = null);
    }
}
