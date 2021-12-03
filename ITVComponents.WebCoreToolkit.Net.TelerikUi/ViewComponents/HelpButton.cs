using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Help;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Net.Options;
using ITVComponents.WebCoreToolkit.Net.ViewModel;
using ITVComponents.WebCoreToolkit.Routing;
using ITVComponents.WebCoreToolkit.Tokens;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewComponents
{
    public class HelpButton : ViewComponent
    {
        private string videoHandler;
        private IRequestCultureFeature cult = null;
        private IUrlFormat formatter;
        private readonly ITutorialSource tutorialSource;

        private string CurrentCulture
        {
            get
            {
                try
                {
                    cult ??= Request.HttpContext.Features.Get<IRequestCultureFeature>();
                }
                catch
                {
                }

                string currentCulture = null;
                if (cult != null)
                {
                    currentCulture = cult.RequestCulture.UICulture.Name;
                }

                return currentCulture;
            }
        }

        public HelpButton(IHierarchySettings<TutorialOptions> options,
            IUrlFormat formatter, ITutorialSource tutorialSource)
        {
            videoHandler = options.Value.VideoFileHandler;
            this.formatter = formatter;
            this.tutorialSource = tutorialSource;
        }

        public async Task<IViewComponentResult> InvokeAsync(string customStyle = null, string customClass = null)
        {
            if (tutorialSource.HasVideoTutorials(HttpContext) && HttpContext.RequestServices.VerifyUserPermissions(new string[] { "ModuleHelp.ViewTutorials" }))
            {
                var tutorials = tutorialSource.GetTutorials(Request.Path.Value, CurrentCulture);
                var model = new List<TutorialDefinition>();
                foreach (var t in tutorials)
                {
                    t.Streams.ForEach(sm => sm.DownloadToken = new DownloadToken
                    {
                        FileDownload = false,
                        ContentType = sm.ContentType,
                        DownloadName =
                            $"Video{sm.TutorialStreamId}.{sm.ContentType.Substring(sm.ContentType.IndexOf("/") + 1)}",
                        DownloadReason = "DownloadTutorialStream",
                        FileIdentifier = $"##VID#{sm.VideoTutorialId}#{sm.TutorialStreamId}",
                        HandlerModuleName = videoHandler
                    }.CompressToken());
                    t.Description = t.Description.Translate(CurrentCulture);
                    t.DisplayName = t.DisplayName.Translate(CurrentCulture);
                    t.VideoMarkup = CreateVideoMarkup(t, customStyle, customClass);
                    model.Add(t);
                }
                return View("Default", model);
            }

            return View("Empty");
        }

        protected virtual string CreateVideoMarkup(TutorialDefinition item, string customStyle, string customClass)
        {
            StringBuilder ctrl = new StringBuilder();
            for (var i = 0; i < item.Streams.Length; i++)
            {
                var str = item.Streams[i];
                string url = formatter == null
                    ? $"/File/{str.DownloadToken}"
                    : formatter.FormatUrl($"[SlashPermissionScope]/File/{str.DownloadToken}");
                ctrl.AppendLine($@"<source src='{url}' type='{str.ContentType}' />");
            }

            var videoTag = $@"<video {(string.IsNullOrEmpty(customStyle) ? @"width=""100%"" height=""100%""" : $@"style=""{customStyle}""")} {(!string.IsNullOrEmpty(customClass) ? $@"class=""{customClass}""" : "")} controls>
{ctrl}
</video>";

            return videoTag;
        }
    }
}
