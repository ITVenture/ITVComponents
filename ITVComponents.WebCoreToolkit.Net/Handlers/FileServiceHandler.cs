using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Json;
﻿using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.BackgroundProcessing;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Net.Extensions;
using ITVComponents.WebCoreToolkit.ServiceShared.FileHandling;
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
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System.Xml.Linq;
using ITVComponents.WebCoreToolkit.ServiceShared.Model;
using ITVComponents.WebCoreToolkit.ServiceShared.Options;
using ITVComponents.WebCoreToolkit.ServiceShared.Service;
using static System.Collections.Specialized.BitVector32;

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
            [FromRoute(Name="UploadReason")]string reason, [FromQuery(Name="uploadHint")]string uploadHint, MultipartFileModel fileData,
            [FromServices] IFileServiceHandler fileServiceHandler)
        {
            return await PostFile(context, true, uploadModule, reason, uploadHint, fileData, fileServiceHandler);
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
            [FromRoute(Name = "UploadReason")] string reason, [FromQuery(Name = "uploadHint")] string uploadHint, MultipartFileModel fileData,
            [FromServices] IFileServiceHandler fileServiceHandler)
        {
            return await PostFile(context, false, uploadModule, reason, uploadHint, fileData, fileServiceHandler);
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

        /// <summary>
        /// Upload-Endpoint for File-operations
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="fileToken">A base64 token (encrypted or clear-text) holding information about the file to download.</param>
        /// <response code="200">a file-result (streamable, if supported by the file-handler) of the requested file</response>
        /// <response code="404">when the handler was unable to find the requested file</response>
        /// <response code="401">when the handler denies access to the requested file</response>
        public static async Task<IResult> GetQueryWithAuth(HttpContext context, [FromQuery(Name = "FileToken")] string fileToken)
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
        public static async Task<IResult> GetQueryNoAuth(HttpContext context, [FromQuery(Name = "FileToken")] string fileToken)
        {
            return await GetFile(context, false, fileToken);
        }

        private static async Task<IResult> PostFile(HttpContext context, bool withAuthorization, string uploadModule, string reason, string uploadHint, MultipartFileModel fileData, IFileServiceHandler fileServiceHandler)
        {
            if (fileData == null)
            {
                return Results.BadRequest("Multipart Upload expected!");
            }

            var options = fileServiceHandler.ResolveUploadOptions(uploadModule, reason);

            var maxSize = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            maxSize.MaxRequestBodySize = options.MaxUploadSize;
            ParsedMultipartFile[] parsedFiles;
            try
            {
                parsedFiles = await ProcessMultipartFileUpload(fileData);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ex.Message);
            }

            var hasBackgroundRule = context.Request.Query.TryGetValue("UseBackground", out var backgroundValue);
            
            var useBackground = hasBackgroundRule && bool.TryParse(backgroundValue, out var bgVal) && bgVal;
            var hasAsset = context.Request.Query.TryGetValue("AssetKey", out var assetKey);
            if (!useBackground)
            {
                var tmp = await fileServiceHandler.ProcessFileUpload(uploadModule, reason, uploadHint, context.User, parsedFiles,
                    options, withAuthorization, hasAsset, assetKey);
                switch (tmp.Code)
                {
                    case FileOperationCode.OK:
                        if (tmp.Success)
                            return Results.Ok();
                        break;
                    case FileOperationCode.OkWithContent:
                        if (tmp.Success)
                        {
                            tmp.ReadResult.DeferredDisposals.ForEach(context.Response.RegisterForDispose);
                            return Results.Stream(tmp.ReadResult.FileContent, tmp.ReadResult.ContentType, tmp.ReadResult.DownloadName);
                        }
                        break;
                    case FileOperationCode.BadRequest:
                        if (!tmp.Success)
                            return Results.BadRequest(string.Join(Environment.NewLine,
                                tmp.Errors.Select(n => $"{n.Key}: {n.Message}")));
                        break;
                    case FileOperationCode.UnAuthorized:
                        if (!tmp.Success)
                            return Results.Unauthorized();
                        break;
                    case FileOperationCode.Forbid:
                        if (!tmp.Success)
                            return Results.Forbid();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                throw new InvalidOperationException(
                    $"Unexpected Combination: Success={tmp.Success}, Code={tmp.Code}, Message={string.Join(Environment.NewLine, tmp.Errors.Select(n => $"{n.Key}: {n.Message}"))}");
            }
            else
            {
                var newContext = context.RequestServices.ConserveRequestData(context);
                IBackgroundTaskQueue queue = context.RequestServices.GetService<IBackgroundTaskQueue>();
                await queue.Enqueue(async (ctx, args) =>
                {
                    var bgJob = args as BackgroundFileJob;
                    IHttpContextAccessor accessor = ctx.Services.GetService<IHttpContextAccessor>();
                    IFileServiceHandler backgroundServiceHandler = ctx.Services.GetService<IFileServiceHandler>();
                    await backgroundServiceHandler.ProcessFileUpload(bgJob.UploadModule, bgJob.Reason, bgJob.UploadHint, accessor.HttpContext.User,
                        bgJob.ParsedFiles, bgJob.Options, bgJob.WithAuthorization, bgJob.HasAsset, bgJob.AssetKey);
                }, newContext, new BackgroundFileJob
                {
                    Options = options,
                    ParsedFiles = parsedFiles,
                    Reason = reason,
                    UploadHint = uploadHint,
                    UploadModule = uploadModule,
                    WithAuthorization = withAuthorization,
                    HasAsset = hasAsset,
                    AssetKey = assetKey
                });
                
                return Results.Ok("File upload scheduled in background.");
            }
        }

        private static async Task<ParsedMultipartFile[]> ProcessMultipartFileUpload(MultipartFileModel fileData)
        {
            var retVal = new List<ParsedMultipartFile>();
            MultipartSection section;
            while ((section = await fileData.FilePartReader.ReadNextSectionAsync()) != null)
            {
                var hasContentDispositionHeader =
                    ContentDispositionHeaderValue.TryParse(
                        section.ContentDisposition, out var contentDisposition);
                if (hasContentDispositionHeader)
                {
                    var content = await section.Body.ToArrayAsync();
                    if (contentDisposition.IsFileDisposition())
                    {
                        string name = contentDisposition.FileName.Value ??
                                      contentDisposition.FileNameStar.Value ??
                                      contentDisposition.Name.Value;
                        retVal.Add(new ParsedMultipartFile
                        {
                            Content = content,
                            FileName = name,
                            ContentType = section.ContentType,
                            IsFormData = false,
                            Headers = section.Headers != null?new Dictionary<string, StringValues>(section.Headers):null
                        });
                    }
                    else if (contentDisposition.IsFormDisposition())
                    {
                        retVal.Add(new ParsedMultipartFile
                        {
                            Content = content,
                            FileName = "--FORM--",
                            ContentType = section.ContentType,
                            IsFormData = true,
                            Headers = section.Headers != null ? new Dictionary<string, StringValues>(section.Headers) : null
                        });
                    }
                }
            }

            return retVal.ToArray();
        }

        private static async Task<IResult> GetFile(HttpContext context, bool withAuthorization, string fileToken)
        {
            var forceRefreshIndicator = fileToken.IndexOf("?");
            if (forceRefreshIndicator != -1)
            {
                fileToken = fileToken.Substring(0, forceRefreshIndicator);
            }

            var token = fileToken.DecompressToken<DownloadToken>();
            var fileServiceHandler = context.RequestServices.GetRequiredService<IFileServiceHandler>();
            var hasAsset = !string.IsNullOrEmpty(token.AssetKey);

            // All plugin-handling (resolve, authorize, async/sync ReadFile, asset-impersonation) lives centrally
            // in the IFileServiceHandler; this edge only maps the neutral result onto an MVC IResult.
            var result = await fileServiceHandler.ProcessFileDownload(token.HandlerModuleName, token.DownloadReason,
                token.FileIdentifier, context.User, token.DownloadName, token.ContentType, token.FileDownload,
                withAuthorization, hasAsset, token.AssetKey);

            switch (result.Code)
            {
                case FileOperationCode.OkWithContent when result.Success:
                    var readResult = result.ReadResult;
                    readResult.DeferredDisposals.ForEach(context.Response.RegisterForDispose);
                    var downloadName = readResult.DownloadName;
                    if (readResult.FileDownload != true)
                    {
                        ContentDispositionHeaderValue hv = new ContentDispositionHeaderValue("inline");
                        hv.SetHttpFileName(downloadName);
                        context.Response.Headers["content-disposition"] = hv.ToString();
                        downloadName = null;
                    }

                    return Results.Stream(readResult.FileContent, readResult.ContentType, downloadName,
                        enableRangeProcessing: true);
                case FileOperationCode.Forbid:
                    return Results.Forbid();
                case FileOperationCode.UnAuthorized:
                    return Results.Unauthorized();
                case FileOperationCode.BadRequest:
                    return Results.BadRequest(string.Join(Environment.NewLine,
                        result.Errors.Select(n => $"{n.Key}: {n.Message}")));
                default:
                    return Results.NotFound();
            }
        }
    }
}