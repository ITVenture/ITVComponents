using ITVComponents.Json;
using ITVComponents.WebCoreToolkit.BackgroundProcessing;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security.AssetLevelImpersonation;
using ITVComponents.WebCoreToolkit.ServiceShared.Extensions;
using ITVComponents.WebCoreToolkit.ServiceShared.FileHandling;
using ITVComponents.WebCoreToolkit.ServiceShared.Model;
using ITVComponents.WebCoreToolkit.ServiceShared.Options;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.ServiceShared.Service.Impl
{
    internal class DefaultFileServiceHandler:IFileServiceHandler
    {
        private readonly IServiceProvider services;
        private readonly ILogger<DefaultFileServiceHandler> logger;

        public DefaultFileServiceHandler(IServiceProvider services, ILogger<DefaultFileServiceHandler> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        public UploadOptions ResolveUploadOptions(string uploadModule, string reason)
        {
            var handlerOptionsName = $"{uploadModule}UploadSettings";
            var handlerReasonOptionsName = $"{reason}_{handlerOptionsName}";
            var scopeOptions = services.GetService<IScopedSettingsProvider>();
            var globalOptions = services.GetService<IGlobalSettingsProvider>();
            var handlerSettingsRaw = scopeOptions?.GetJsonSetting(handlerOptionsName) ??
                                     globalOptions?.GetJsonSetting(handlerOptionsName);
            var reasonSettingsRaw = scopeOptions?.GetJsonSetting(handlerReasonOptionsName) ??
                                    globalOptions?.GetJsonSetting(handlerReasonOptionsName);
            var finalSettingsRaw = reasonSettingsRaw ?? handlerSettingsRaw;
            var options = new UploadOptions();
            if (!string.IsNullOrEmpty(finalSettingsRaw))
            {
                options = JsonHelper.FromJsonString<UploadOptions>(finalSettingsRaw, SerializationTypingMode.StaticTyping);
            }

            return options;
        }

        public async Task<FileOperationResult> ProcessFileUpload(string uploadModule, string reason, string uploadHint,
            ClaimsPrincipal principal,
            ParsedMultipartFile[] files, UploadOptions options, bool withAuthorization = true, bool hasAsset = false, string assetKey = null)
        {
            IImpersonationControl assetImpersonator = null;
            IDisposable assetAccess = null;
            if (!string.IsNullOrEmpty(assetKey))
            {
                assetImpersonator = services.GetRequiredService<IImpersonationControl>();
                assetAccess = assetImpersonator.AsAssetAccessor(assetKey);
            }

            try
            {
                // Load the handler from a per-operation plugin-scope: a handler that takes the system-context as a
                // scope-owned dependency (MLM §8) then receives a fresh, per-operation context that is disposed when
                // this upload completes. Without §8 opt-in the scope is empty → behavior unchanged.
                using var opScope = services.GetService<IWebPluginHelper>().CreateOperationScope();
                var fileHandler = services.GetFileHandler(opScope, uploadModule);
                var syncHandler = fileHandler as IFileHandler;
                var asyncHandler = fileHandler as IAsyncFileHandler;
                var requiredPermissions = fileHandler.PermissionsForReason(reason);
                if (!withAuthorization || (requiredPermissions != null && requiredPermissions.Length != 0 &&
                                           services.VerifyUserPermissions(requiredPermissions)))
                {
                    try
                    {
                        foreach (var section in files)
                        {

                            if (!section.IsFormData)
                            {
                                if (VerifyFile(section.FileName, section.Content, options, out var deniedReason))
                                {
                                    FileOperationResult uploadResult;
                                    if (string.IsNullOrEmpty(uploadHint))
                                    {
                                        if (asyncHandler != null)
                                        {
                                            uploadResult = await (!hasAsset
                                                ? asyncHandler.AddFile(section.FileName, section.Content,
                                                    principal?.Identity,
                                                    (n, c) => VerifyFile(n, c, options, out _))
                                                : asyncHandler.AddFile(section.FileName, section.Content, (string)assetKey,
                                                    principal, (n, c) => VerifyFile(n, c, options, out _)));
                                        }
                                        else
                                        {
                                            uploadResult = !hasAsset
                                                ? syncHandler.AddFile(section.FileName, section.Content, principal?.Identity,
                                                    (n, c) => VerifyFile(n, c, options, out _))
                                                : syncHandler.AddFile(section.FileName, section.Content, (string)assetKey,
                                                    principal, (n, c) => VerifyFile(n, c, options, out _));
                                        }
                                    }
                                    else
                                    {
                                        if (asyncHandler != null)
                                        {
                                            uploadResult = await (!hasAsset
                                                ? asyncHandler.AddFile(section.FileName, section.Content, uploadHint,
                                                    principal?.Identity)
                                                : asyncHandler.AddFile(section.FileName, section.Content, uploadHint,
                                                    (string)assetKey, principal));
                                        }
                                        else
                                        {
                                            uploadResult = !hasAsset
                                                ? syncHandler.AddFile(section.FileName, section.Content, uploadHint,
                                                    principal?.Identity)
                                                : syncHandler.AddFile(section.FileName, section.Content, uploadHint, (string)assetKey,
                                                    principal);
                                        }
                                    }

                                    if (uploadResult is { Success: false })
                                    {
                                        return FileOperationResult.BadRequest(uploadResult.Errors == null || uploadResult.Errors.Count == 0
                                            ? "Upload failed."
                                            : string.Join(Environment.NewLine, uploadResult.Errors.Select(e => e.Message)));
                                    }
                                }
                                else
                                {
                                    return FileOperationResult.BadRequest(deniedReason);
                                }
                            }
                            else if (section.IsFormData)
                            {
                                using var body = new MemoryStream(section.Content);
                                string formContent = null;
                                if (asyncHandler != null && asyncHandler is IAsyncFormProcessor asyncFormer)
                                {
                                    formContent = await asyncFormer.ProcessForm(section.Headers, body);
                                }
                                else if (syncHandler != null && syncHandler is IFormProcessor syncFormer)
                                {
                                    formContent = syncFormer.ProcessForm(section.Headers, body);
                                }

                                formContent ??= Encoding.Default.GetString(section.Content);
                                logger.LogDebug(new EventId(0, "Found Form-Disposition"),
                                    $"Form Content: {formContent})");
                            }
                        }

                        var rfh = syncHandler as IRespondingFileHandler;
                        var arh = asyncHandler as IAsyncRespondingFileHandler;
                        if (rfh == null && arh == null)
                        {
                            return FileOperationResult.Ok();
                        }

                        var uploadResponse = arh != null ? await arh.GetUploadResult() : rfh.GetUploadResult();
                        if (uploadResponse == null)
                        {
                            return FileOperationResult.Ok();
                        }

                        return FileOperationResult.OkWithContent(uploadResponse);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"An error occurred while processing the file upload upload-Module: {uploadModule}");
                        return FileOperationResult.BadRequest(ex.Message);
                    }
                }
                else if (services.VerifyCurrentUser())
                {
                    return FileOperationResult.Forbid();
                }
            }
            finally
            {
                assetAccess?.Dispose();
            }

            return FileOperationResult.UnAuthorized();
        }

        public async Task<FileOperationResult> ProcessFileDownload(string downloadModule, string reason, string fileIdentifier,
            ClaimsPrincipal principal, string defaultDownloadName = null, string defaultContentType = null,
            bool defaultFileDownload = true, bool withAuthorization = true, bool hasAsset = false, string assetKey = null)
        {
            IImpersonationControl assetImpersonator = null;
            IDisposable assetAccess = null;
            if (!string.IsNullOrEmpty(assetKey))
            {
                assetImpersonator = services.GetRequiredService<IImpersonationControl>();
                assetAccess = assetImpersonator.AsAssetAccessor(assetKey);
            }

            try
            {
                // Load the handler from a per-operation plugin-scope (see ProcessFileUpload). For a successful,
                // content-bearing download the scope-owned context may still back a lazily-read stream, so the
                // scope's disposal is handed to FileReadResult.DeferredDisposals (both edges dispose those only
                // after the stream has been served) instead of being released synchronously here.
                var opScope = services.GetService<IWebPluginHelper>().CreateOperationScope();
                var disposeScope = true;
                try
                {
                    var fileHandler = services.GetFileHandler(opScope, downloadModule);
                    var syncHandler = fileHandler as IFileHandler;
                    var asyncHandler = fileHandler as IAsyncFileHandler;
                    if (syncHandler == null && asyncHandler == null)
                    {
                        return FileOperationResult.NotFound(new FileError("Handler", $"No file-handler '{downloadModule}' found."));
                    }

                    var requiredPermissions = fileHandler.PermissionsForReason(reason);
                    if (!withAuthorization || (requiredPermissions != null && requiredPermissions.Length != 0 &&
                                               services.VerifyUserPermissions(requiredPermissions)))
                    {
                        FileReadResult readResult;
                        if (asyncHandler != null)
                        {
                            readResult = !hasAsset
                                ? await asyncHandler.ReadFile(fileIdentifier, principal?.Identity)
                                : await asyncHandler.ReadFile(fileIdentifier, principal, assetKey);
                        }
                        else
                        {
                            readResult = !hasAsset
                                ? syncHandler.ReadFile(fileIdentifier, principal?.Identity)
                                : syncHandler.ReadFile(fileIdentifier, principal, assetKey);
                        }

                        if (readResult is { Success: true, FileContent: not null })
                        {
                            // Apply the host-supplied defaults for anything the handler left open.
                            readResult.DownloadName ??= defaultDownloadName;
                            readResult.ContentType ??= defaultContentType;
                            readResult.FileDownload ??= defaultFileDownload;
                            readResult.DeferredDisposals.Add(opScope);
                            disposeScope = false;
                            return FileOperationResult.OkWithContent(readResult);
                        }

                        return FileOperationResult.NotFound();
                    }

                    if (services.VerifyCurrentUser())
                    {
                        return FileOperationResult.Forbid();
                    }

                    return FileOperationResult.UnAuthorized();
                }
                finally
                {
                    if (disposeScope)
                    {
                        opScope.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while processing the file download download-Module: {downloadModule}");
                return FileOperationResult.BadRequest(ex.Message);
            }
            finally
            {
                // Released after ReadFile returned: the content-stream may be backed by a lazily-read DB
                // resource that is kept alive through FileReadResult.DeferredDisposals (disposed by the host
                // once the stream has been served), but the asset-access scope is no longer needed.
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
