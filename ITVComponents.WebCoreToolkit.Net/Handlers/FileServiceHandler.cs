using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Net.Extensions;
using ITVComponents.WebCoreToolkit.Net.FileHandling;
using ITVComponents.WebCoreToolkit.Net.Handlers.Model;
using ITVComponents.WebCoreToolkit.Net.Options;
using ITVComponents.WebCoreToolkit.Net.ViewModel;
using ITVComponents.WebCoreToolkit.Security.AssetLevelImpersonation;
using ITVComponents.WebCoreToolkit.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace ITVComponents.WebCoreToolkit.Net.Handlers
{
    internal static class FileServiceHandler
    {
        /// <summary>
        /// Upload-Endpoint for File-operations
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="uploadModule">The name of the target-upload module, that will process the uploaded data.</param>
        /// <param name="reason">the reason, why the file was uploaded. The reason us used by the upload-module to determine the required permissios.</param>
        /// <param name="uploadHint">provides additional information about the file to the uploader-module (e.g. an id, if a file is being replaced)</param>
        /// <response code="200">an empty OK-result, when the upload was successful</response>
        /// <response code="200">a custom content result, if the file-handler is able to provide feedback after processing the file.</response>
        /// <response code="400">a bad-request, when the provided data is not a multipart-form upload</response>
        /// <response code="400">when the file could not be validated using the settings for the upload-module</response>
        /// <response code="400">when the file was denied by the uploader module</response>
        /// <response code="400">for any unexpected errors</response>
        /// <response code="404">a not-found when the given Diagnostics-Query does not exist</response>
        public static async Task<IResult> PostWithAuth(HttpContext context, [FromRoute(Name="UploadModule")]string uploadModule,
            [FromRoute(Name="UploadReason")]string reason, [FromQuery(Name="uploadHint")]string uploadHint, MultipartFileModel fileData)
        {
            return await PostFile(context, true, uploadModule, reason, uploadHint, fileData);
        }

        /// <summary>
        /// Upload-Endpoint for File-operations
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="uploadModule">The name of the target-upload module, that will process the uploaded data.</param>
        /// <param name="reason">the reason, why the file was uploaded. The reason us used by the upload-module to determine the required permissios.</param>
        /// <param name="uploadHint">provides additional information about the file to the uploader-module (e.g. an id, if a file is being replaced)</param>
        /// <response code="200">an empty OK-result, when the upload was successful</response>
        /// <response code="200">a custom content result, if the file-handler is able to provide feedback after processing the file.</response>
        /// <response code="400">a bad-request, when the provided data is not a multipart-form upload</response>
        /// <response code="400">when the file could not be validated using the settings for the upload-module</response>
        /// <response code="400">when the file was denied by the uploader module</response>
        /// <response code="400">for any unexpected errors</response>
        /// <response code="404">a not-found when the given Diagnostics-Query does not exist</response>
        public static async Task<IResult> PostNoAuth(HttpContext context, [FromRoute(Name = "UploadModule")] string uploadModule,
            [FromRoute(Name = "UploadReason")] string reason, [FromQuery(Name = "uploadHint")] string uploadHint, MultipartFileModel fileData)
        {
            return await PostFile(context, false, uploadModule, reason, uploadHint, fileData);
        }

        /// <summary>
        /// Upload-Endpoint for File-operations
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="fileToken">A base64 token (encrypted or clear-text) holding information about the file to download.</param>
        /// <response code="200">a file-result (streamable, if supported by the file-handler) of the requested file</response>
        /// <response code="404">when the handler was unable to find the requested file</response>
        /// <response code="401">when the handler denies access to the requested file</response>
        public static async Task<IResult> GetWithAuth(HttpContext context, [FromRoute(Name="FileToken")]string fileToken)
        {
            return await GetFile(context, true, fileToken);
        }

        /// <summary>
        /// Upload-Endpoint for File-operations
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="fileToken">A base64 token (encrypted or clear-text) holding information about the file to download.</param>
        /// <response code="200">a file-result (streamable, if supported by the file-handler) of the requested file</response>
        /// <response code="404">when the handler was unable to find the requested file</response>
        /// <response code="401">when the handler denies access to the requested file</response>
        public static async Task<IResult> GetNoAuth(HttpContext context, [FromRoute(Name = "FileToken")] string fileToken)
        {
            return await GetFile(context, false, fileToken);
        }

        private static async Task<IResult> PostFile(HttpContext context, bool withAuthorization, string uploadModule, string reason, string uploadHint, MultipartFileModel fileData)
        {
            if (fileData == null)
            {
                return Results.BadRequest("Multipart Upload expected!");
            }

            var hasAsset = context.Request.Query.TryGetValue("AssetKey", out var assetKey);
            IImpersonationControl assetImpersonator = null;
            IDisposable assetAccess = null;
            if (!string.IsNullOrEmpty(assetKey))
            {
                assetImpersonator = context.RequestServices.GetRequiredService<IImpersonationControl>();
                assetAccess = assetImpersonator.AsAssetAccessor(assetKey);
            }

            try
            {
                var fileHandler = context.RequestServices.GetFileHandler(uploadModule);
                var syncHandler = fileHandler as IFileHandler;
                var asyncHandler = fileHandler as IAsyncFileHandler;
                var handlerOptionsName = $"{uploadModule}UploadSettings";
                var handlerReasonOptionsName = $"{reason}_{handlerOptionsName}";
                var logger = context.RequestServices.GetService<ILogger<IFileHandler>>();
                var scopeOptions = context.RequestServices.GetService<IScopedSettingsProvider>();
                var globalOptions = context.RequestServices.GetService<IGlobalSettingsProvider>();
                var handlerSettingsRaw = scopeOptions?.GetJsonSetting(handlerOptionsName) ??
                                         globalOptions?.GetJsonSetting(handlerOptionsName);
                var reasonSettingsRaw = scopeOptions?.GetJsonSetting(handlerReasonOptionsName) ??
                                        globalOptions?.GetJsonSetting(handlerReasonOptionsName);
                var finalSettingsRaw = reasonSettingsRaw ?? handlerSettingsRaw;
                var options = new UploadOptions();
                if (!string.IsNullOrEmpty(finalSettingsRaw))
                {
                    options = JsonHelper.FromJsonString<UploadOptions>(finalSettingsRaw);
                }

                var maxSize = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                maxSize.MaxRequestBodySize = options.MaxUploadSize;
                var requiredPermissions = asyncHandler?.PermissionsForReason(reason) ??
                                          syncHandler.PermissionsForReason(reason);
                if (!withAuthorization || (requiredPermissions != null && requiredPermissions.Length != 0 &&
                                           context.RequestServices.VerifyUserPermissions(requiredPermissions)))
                {
                    MultipartSection section;
                    var ms = new ModelStateDictionary();
                    try
                    {
                        while ((section = await fileData.FilePartReader.ReadNextSectionAsync()) != null)
                        {
                            var hasContentDispositionHeader =
                                ContentDispositionHeaderValue.TryParse(
                                    section.ContentDisposition, out var contentDisposition);
                            if (hasContentDispositionHeader)
                            {
                                if (contentDisposition.IsFileDisposition())
                                {
                                    logger.LogDebug(new EventId(0, "Found File-Disposition"),
                                        $"Found a File: {contentDisposition.Name.Value} ({contentDisposition.FileName.Value},{contentDisposition.FileNameStar.Value})");
                                    var content = await section.Body.ToArrayAsync();
                                    string name = contentDisposition.FileName.Value ??
                                                  contentDisposition.FileNameStar.Value ??
                                                  contentDisposition.Name.Value;
                                    if (VerifyFile(name, content, options, out var deniedReason))
                                    {
                                        if (string.IsNullOrEmpty(uploadHint))
                                        {
                                            if (asyncHandler != null)
                                            {
                                                await (!hasAsset
                                                    ? asyncHandler.AddFile(name, content, ms,
                                                        context.User?.Identity,
                                                        (n, c) => VerifyFile(n, c, options, out _))
                                                    : asyncHandler.AddFile(name, content, (string)assetKey, ms,
                                                        context.User, (n, c) => VerifyFile(n, c, options, out _)));
                                            }
                                            else
                                            {
                                                if (!hasAsset)
                                                {
                                                    syncHandler.AddFile(name, content, ms, context.User?.Identity,
                                                        (n, c) => VerifyFile(n, c, options, out _));
                                                }
                                                else
                                                {
                                                    syncHandler.AddFile(name, content, (string)assetKey, ms,
                                                        context.User, (n, c) => VerifyFile(n, c, options, out _));
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (asyncHandler != null)
                                            {
                                                await (!hasAsset
                                                    ? asyncHandler.AddFile(name, content, uploadHint, ms,
                                                        context.User?.Identity)
                                                    : asyncHandler.AddFile(name, content, uploadHint,
                                                        (string)assetKey, ms, context.User));
                                            }
                                            else
                                            {
                                                if (!hasAsset)
                                                {
                                                    syncHandler.AddFile(name, content, uploadHint, ms,
                                                        context.User?.Identity);
                                                }
                                                else
                                                {
                                                    syncHandler.AddFile(name, content, uploadHint, (string)assetKey,
                                                        ms,
                                                        context.User);
                                                }
                                            }
                                        }

                                        if (!ms.IsValid)
                                        {
                                            return Results.BadRequest(string.Join(Environment.NewLine,
                                                (from t in ms where t.Key == "File" select t.Value.Errors)
                                                .SelectMany(m => m).Select(i => i.ErrorMessage)));
                                        }
                                    }
                                    else
                                    {
                                        return Results.BadRequest(deniedReason);
                                    }
                                }
                                else if (contentDisposition.IsFormDisposition())
                                {
                                    string formContent = null;
                                    if (asyncHandler != null && asyncHandler is IAsyncFormProcessor asyncFormer)
                                    {
                                        formContent = await asyncFormer.ProcessForm(section.Headers, section.Body);
                                    }
                                    else if (syncHandler != null && syncHandler is IFormProcessor syncFormer)
                                    {
                                        formContent = syncFormer.ProcessForm(section.Headers, section.Body);
                                    }

                                    formContent ??= Encoding.Default.GetString(await section.Body.ToArrayAsync());
                                    logger.LogDebug(new EventId(0, "Found Form-Disposition"),
                                        $"Form Content: {formContent})");
                                }
                            }
                        }

                        var rfh = syncHandler as IRespondingFileHandler;
                        var arh = asyncHandler as IAsyncRespondingFileHandler;
                        if (rfh == null && arh == null)
                        {
                            return Results.Ok();
                        }
                        else if (arh != null)
                        {
                            return await arh.GetUploadResult();
                        }
                        else
                        {
                            return rfh.GetUploadResult();
                        }
                    }
                    catch (Exception ex)
                    {
                        return Results.BadRequest(ex.Message);
                    }
                }
            }
            finally
            {
                assetAccess?.Dispose();
            }

            return Results.Unauthorized();
        }

        private static async Task<IResult> GetFile(HttpContext context, bool withAuthorization, string fileToken)
        {
            var forceRefreshIndicator = fileToken.IndexOf("?");
            if (forceRefreshIndicator != -1)
            {
                fileToken = fileToken.Substring(0, forceRefreshIndicator);
            }

            var token = fileToken.DecompressToken<DownloadToken>();
            var isAssetAccess = false;
            //var assetAccessLegal = false;
            IImpersonationControl assetImpersonator = null;
            IDisposable assetAccess = null;
            if (!string.IsNullOrEmpty(token.AssetKey))
            {
                assetImpersonator = context.RequestServices.GetRequiredService<IImpersonationControl>();
                isAssetAccess = true;
                assetAccess = assetImpersonator.AsAssetAccessor(token.AssetKey);
            }

            try
            {
                var fileHandler = context.RequestServices.GetFileHandler(token.HandlerModuleName);
                var syncHandler = fileHandler as IFileHandler;
                var asyncHandler = fileHandler as IAsyncFileHandler;
                var requiredPermissions = asyncHandler?.PermissionsForReason(token.DownloadReason) ??
                                          syncHandler.PermissionsForReason(token.DownloadReason);

                if (!withAuthorization || (requiredPermissions != null && requiredPermissions.Length != 0 &&
                                           context.RequestServices.VerifyUserPermissions(requiredPermissions)))
                {
                    string downloadName = token.DownloadName;
                    string contentType = token.ContentType;
                    bool forceDownload = token.FileDownload;
                    byte[] fileContent = null;
                    Stream fileStreamContent = null;
                    bool asyncOk = false;
                    if (asyncHandler != null)
                    {
                        var tmp = !isAssetAccess
                            ? await asyncHandler.ReadFile(token.FileIdentifier, context.User?.Identity)
                            : await asyncHandler.ReadFile(token.FileIdentifier, context.User,
                                token.AssetKey);
                        // ReSharper disable once AssignmentInConditionalExpression
                        if (asyncOk = tmp.Success)
                        {
                            downloadName = tmp.DownloadName ?? downloadName;
                            contentType = tmp.ContentType ?? contentType;
                            forceDownload = tmp.FileDownload ?? forceDownload;
                            fileStreamContent = tmp.FileContent;
                            tmp.DeferredDisposals.ForEach(context.Response.RegisterForDispose);
                        }
                    }

                    if ((asyncOk && fileStreamContent != null) ||
                        (syncHandler != null &&
                         (!isAssetAccess
                             ? syncHandler.ReadFile(token.FileIdentifier,
                                 context.User?.Identity, ref downloadName, ref contentType, ref forceDownload,
                                 out fileContent)
                             : syncHandler.ReadFile(token.FileIdentifier,
                                 context.User, token.AssetKey, ref downloadName, ref contentType,
                                 ref forceDownload,
                                 out fileContent))))
                    {
                        IResult fileResult;
                        if (!forceDownload)
                        {
                            ContentDispositionHeaderValue hv = new ContentDispositionHeaderValue("inline");
                            hv.SetHttpFileName(downloadName);
                            context.Response.Headers["content-disposition"] = hv.ToString();
                            downloadName = null;
                        }
                        if (fileStreamContent != null)
                        {
                            fileResult = Results.Stream(fileStreamContent, contentType, downloadName,
                                enableRangeProcessing: true);
                        }
                        else
                        {
                            fileResult = Results.File(fileContent, contentType, downloadName);
                        }

                        return fileResult;
                    }

                    return Results.NotFound();
                }

                return Results.Unauthorized();
            }
            finally
            {
                assetAccess?.Dispose();
            }
        }

        /// <summary>
        /// Verifies the name and content of an uploaded file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <param name="content">the content of the file</param>
        /// <param name="options">the configured options for the upload</param>
        /// <param name="deniedReason">the reason why the file should not be accepted</param>
        /// <returns>a value indicating whether the file can be accepted by the uploader</returns>
        private static bool VerifyFile(string name, byte[] content, UploadOptions options, out string deniedReason)
        {
            if (options.UncheckedFileExtensions != null && options.UncheckedFileExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            {
                deniedReason = "";
                return true;
            }

            if (!(options.FileExtensions == null || options.FileExtensions.Count == 0 || options.FileExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase)))
            {
                deniedReason = "File-Extension not allowed";
                return false;
            }

            if (!(options.MagicNumbers == null || options.MagicNumbers.Count == 0 || options.MagicNumbers.Any(n => n.SequenceEqual(content.Take(n.Length)))))
            {
                deniedReason = "File-Format not supported";
                return false;
            }

            deniedReason = "";
            return true;
        }
    }
}