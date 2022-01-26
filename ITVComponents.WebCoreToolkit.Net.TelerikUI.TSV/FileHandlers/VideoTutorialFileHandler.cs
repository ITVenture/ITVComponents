using System;
using System.Data.Common;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Net.FileHandling;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.FileHandlers
{
    public class VideoTutorialFileHandler : IAsyncFileHandler, IPlugin
    {
        private readonly IBaseTenantContext db;

        public VideoTutorialFileHandler(IBaseTenantContext db)
        {
            this.db = db;
        }

        public string UniqueName { get; set; }

        public string[] PermissionsForReason(string reason)
        {
            if (reason == "UploadTutorialStream")
            {
                return new[] { "ModuleHelp.Write" };
            }

            if (reason == "DownloadTutorial")
            {
                return new[] { "ModuleHelp.Write" };
            }

            if (reason == "DownloadTutorialStream")
            {
                return new[] { "ModuleHelp.ViewTutorials" };
            }

            return null;
        }

        public Task AddFile(string name, byte[] content, ModelStateDictionary ms, IIdentity uploadingIdentity, Func<string, byte[], bool> verifyNextedFile)
        {
            throw new InvalidOperationException("Upload-Hint required!");
        }

        public async Task AddFile(string name, byte[] content, string uploadHint, ModelStateDictionary ms, IIdentity uploadingIdentity)
        {
            var ok = uploadHint.StartsWith("##VID#");
            if (ok)
            {
                var idHint = uploadHint.Substring(6).Split("#").Select(n => int.Parse(n)).ToArray();
                if (idHint.Length == 1)
                {
                    var entity = new TutorialStream
                    {
                        LanguageTag = "Default",
                        ContentType = "Unknown",
                        VideoTutorialId = idHint[0],
                        Blob = new TutorialStreamBlob { Content = content }
                    };
                    entity.Blob.Parent = entity;
                    db.TutorialStreams.Add(entity);
                    await db.SaveChangesAsync();
                }
                else if (idHint.Length == 2)
                {
                    var entity = db.TutorialStreams.First(n =>
                        n.VideoTutorialId == idHint[0] && n.TutorialStreamId == idHint[1]);
                    entity.Blob.Content = content;
                    await db.SaveChangesAsync();
                }
                else
                {
                    throw new InvalidOperationException("Invalid Uploadhint provided!");
                }
            }
            else
            {
                throw new InvalidOperationException("Invalid Uploadhint provided!");
            }
        }

        public async Task<AsyncReadFileResult> ReadFile(string fileIdentifier, IIdentity downloadingIdentity)
        {
            var ok = fileIdentifier.StartsWith("##VID#");
            if (ok)
            {
                var idHint = fileIdentifier.Substring(6).Split("#").Select(n => int.Parse(n)).ToArray();
                var retVal = new AsyncReadFileResult();
                retVal.DeferredDisposals.Add(db.Database.UseConnection(out DbCommand cmd));
                retVal.DeferredDisposals.Add(cmd);
                cmd.CommandText = "Select Content from TutorialStreamBlob where TutorialStreamId = @streamId";
                var param = cmd.CreateParameter();
                param.ParameterName = "@streamId";
                param.Value = idHint[1];
                cmd.Parameters.Add(param);
                var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                retVal.DeferredDisposals.Add(reader);
                if (!reader.Read())
                {
                    retVal.DeferredDisposals.Reverse();
                    retVal.DeferredDisposals.ForEach(n => n.Dispose());
                    retVal.DeferredDisposals.Clear();
                    retVal.Success = false;
                    return retVal;
                }

                retVal.FileContent = reader.GetStream(0);
                retVal.Success = true;
                return retVal;
            }

            throw new InvalidOperationException("Invalid File-Identifier provided!");
        }

        public void Dispose()
        {
            OnDisposed();
        }

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Disposed;
    }
}