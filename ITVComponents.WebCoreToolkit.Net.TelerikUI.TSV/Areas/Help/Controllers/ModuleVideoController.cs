using System.Linq;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using ITVComponents.WebCoreToolkit.Net.ViewModel;
using ITVComponents.WebCoreToolkit.Tokens;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorialStream = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.TutorialStream;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Help.Controllers
{
    [Authorize("HasPermission(ModuleHelp.View,ModuleHelp.Write)"), Area("Help")]
    public class ModuleVideoController : Controller
    {
        private readonly IBaseTenantContext db;
        private readonly IHierarchySettings<TutorialOptions> options;
        private string videoFileHandler;

        public ModuleVideoController(IBaseTenantContext db, IHierarchySettings<TutorialOptions> options)
        {
            this.db = db;
            this.options = options;
        }

        private string VideoFileHandler => videoFileHandler ??= options.Value.VideoFileHandler;

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Streams(int videoTutorialId)
        {
            ViewData["Handler"] = VideoFileHandler;
            return PartialView(videoTutorialId);
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            return Json(db.Tutorials.ToDataSourceResult(request, n => n.ToViewModel<VideoTutorial, VideoTutorialViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(ModuleHelp.Write)")]
        public IActionResult Create([DataSourceRequest] DataSourceRequest request)
        {
            var tut = new VideoTutorial();
            this.TryUpdateModelAsync<VideoTutorialViewModel, VideoTutorial>(tut);
            db.Tutorials.Add(tut);
            db.SaveChanges();
            //tutorial.VideoTutorialId = tut.VideoTutorialId;
            return Json(new[] { tut }.ToDataSourceResult(request, ModelState,
                v => v.ToViewModel<VideoTutorial, VideoTutorialViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(ModuleHelp.Write)")]
        public IActionResult Update([DataSourceRequest] DataSourceRequest request, VideoTutorialViewModel tutorial)
        {
            var tut = db.Tutorials.First(n => n.VideoTutorialId == tutorial.VideoTutorialId);
            this.TryUpdateModelAsync<VideoTutorialViewModel, VideoTutorial>(tut);
            //tutorial.VideoTutorialId = tut.VideoTutorialId;
            db.SaveChanges();
            return Json(new[] { tut }.ToDataSourceResult(request, ModelState,
                v => v.ToViewModel<VideoTutorial, VideoTutorialViewModel>()));
        }


        [HttpPost]
        [Authorize("HasPermission(ModuleHelp.Write)")]
        public IActionResult Delete([DataSourceRequest] DataSourceRequest request, VideoTutorialViewModel tutorial)
        {
            var tut = db.Tutorials.First(n => n.VideoTutorialId == tutorial.VideoTutorialId);
            db.Tutorials.Remove(tut);
            //tutorial.VideoTutorialId = tut.VideoTutorialId;
            db.SaveChanges();
            return Json(new[] { tutorial }.ToDataSourceResult(request, ModelState));
        }

        [HttpPost]
        public IActionResult ReadStream([DataSourceRequest] DataSourceRequest request, [FromQuery] int videoTutorialId)
        {
            return Json(db.TutorialStreams.Where(n => n.VideoTutorialId == videoTutorialId).ToDataSourceResult(request, n => n.ToViewModel<TutorialStream, TutorialStreamViewModel>(BuildDownloadToken)));
        }

        [HttpPost]
        [Authorize("HasPermission(ModuleHelp.Write)")]
        public IActionResult UpdateStream([DataSourceRequest] DataSourceRequest request, TutorialStreamViewModel tutorial)
        {
            var tut = db.TutorialStreams.First(n => n.TutorialStreamId == tutorial.TutorialStreamId);
            this.TryUpdateModelAsync<TutorialStreamViewModel, TutorialStream>(tut);
            //tutorial.VideoTutorialId = tut.VideoTutorialId;
            db.SaveChanges();
            return Json(new[] { tut }.ToDataSourceResult(request, ModelState,
                v => v.ToViewModel<TutorialStream, TutorialStreamViewModel>(BuildDownloadToken)));
        }


        [HttpPost]
        [Authorize("HasPermission(ModuleHelp.Write)")]
        public IActionResult DeleteStream([DataSourceRequest] DataSourceRequest request, TutorialStreamViewModel tutorial)
        {
            var tut = db.TutorialStreams.First(n => n.TutorialStreamId == tutorial.TutorialStreamId);
            db.TutorialStreams.Remove(tut);
            //tutorial.VideoTutorialId = tut.VideoTutorialId;
            db.SaveChanges();
            return Json(new[] { tutorial }.ToDataSourceResult(request, ModelState));
        }

        private void BuildDownloadToken(TutorialStream model, TutorialStreamViewModel vm)
        {
            var token = new DownloadToken
            {
                FileDownload = true,
                ContentType = vm.ContentType,
                DownloadName = $"Video{model.TutorialStreamId}.{model.ContentType.Substring(model.ContentType.IndexOf("/") + 1)}",
                DownloadReason = "DownloadTutorial",
                FileIdentifier = $"##VID#{vm.VideoTutorialId}#{vm.TutorialStreamId}",
                HandlerModuleName = VideoFileHandler
            }.CompressToken();
            vm.DownloadToken = token;
        }
    }
}
