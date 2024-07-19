using System;
using System.Linq;
using System.Runtime.Loader;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DataSync;
using ITVComponents.EFRepo.DataSync.Models;
using ITVComponents.Scripting.CScript.Core.Native;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Health;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewComponents;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel;
using ITVComponents.WebCoreToolkit.Net.ViewModel;
using ITVComponents.WebCoreToolkit.Tokens;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Telerik.SvgIcons;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Areas.Util.Controllers
{
    [Authorize("HasPermission(AssemblyDiagnostics.View)"), Area("Util")]
    public class AssemblyDiagnosticsController:Controller
    {
        private readonly IInjectablePlugin<IConfigurationHandler> configurator;
        private readonly ApplicationPartManager partMgr;
        private readonly ConfigExchangeOptions options;
        private readonly HealthCheckService health;

        public AssemblyDiagnosticsController(IInjectablePlugin<IConfigurationHandler> configurator, IHierarchySettings<ConfigExchangeOptions> options, ApplicationPartManager partMgr, IServiceProvider services)
        {
            this.configurator = configurator;
            this.partMgr = partMgr;
            this.options = options.Value;
            health = services.GetService<HealthCheckService>();
        }

        public ViewResult Index()
        {
            ViewData["UseConfigExchange"] = options.UseConfigExchange;
            return View();
        }

        public PartialViewResult AssemblyList()
        {
            return PartialView();
        }

        public PartialViewResult ClaimList()
        {
            return PartialView();
        }

        public PartialViewResult ControllerList()
        {
            return PartialView();
        }

        public PartialViewResult ViewComponentList()
        {
            return PartialView();
        }

        public PartialViewResult ViewList()
        {
            return PartialView();
        }

        public PartialViewResult TagHelperList()
        {
            return PartialView();
        }

        public PartialViewResult HealthList()
        {
            if (health != null)
            {
                return PartialView();
            }

            return PartialView("HealthUnavailable");
        }

        public PartialViewResult HealthCheckDetailList(string checkName)
        {
            if (health != null)
            {
                ViewData["checkName"] = checkName;
                return PartialView();
            }

            return PartialView("HealthUnavailable");
        }


        public PartialViewResult ConfigExchange()
        {
            ViewData["ConfigLink"] = new DownloadToken
            {
                ContentType = "application/json",
                DownloadName = options.DownloadName,
                DownloadReason = options.DownloadReason,
                FileDownload = true,
                FileIdentifier = options.FileIdentifier,
                HandlerModuleName = options.UploadModuleName
            }.CompressToken();
            return PartialView(options);
        }

        public IActionResult ReadClaims([DataSourceRequest] DataSourceRequest request)
        {
            if (User.Identity is ClaimsIdentity ci)
            {
                return Json((ci.Claims.Select(n => new ClaimData
                {
                    Type = n.Type,
                    Issuer = n.Issuer,
                    ValueType = n.ValueType,
                    OriginalIssuer = n.OriginalIssuer,
                    Value = n.Value
                })).ToDataSourceResult(request));
            }

            return Json(Array.Empty<ClaimData>().ToDataSourceResult(request));
        }

        [HttpPost]
        public IActionResult ResetNativeScripts()
        {
            NativeScriptHelper.ResetNativeScripts();
            return Ok();
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            return Json(AssemblyLoadContext.All.SelectMany(n => n.Assemblies.Select(a => new {Context = n.Name, Assembly = a})).Select(n => new AssemblyDiagnosticsItemViewModel
            {
                FullName=n.Assembly.FullName,
                IsDynamic = n.Assembly.IsDynamic,
                Location = !n.Assembly.IsDynamic?n.Assembly.Location:"--DYNAMIC--",
                LoadContext = n.Context,
                RuntimeVersion = n.Assembly.ImageRuntimeVersion,
                IsCollectible = n.Assembly.IsCollectible
            }).ToDataSourceResult(request));
        }

        public IActionResult ApplyChanges([FromBody] ApplyConfigBaseViewModel configData)
        {
            var messages = new StringBuilder();
            configurator.Instance.ApplyChanges(configData.Changes, messages);
            var msg = messages.Length != 0 ? $"<pre>{messages}</pre>" : "OK";
            return new ContentResult() { Content = msg, ContentType = "text/plain" };
        }

        public async Task<IActionResult> ReadTests([DataSourceRequest] DataSourceRequest request)
        {
            var healthStatus = await health.CheckHealthAsync();
            return Json(await (from t in healthStatus.Entries
                select new HealthTestData
                {
                    Name = t.Key,
                    Result = t.Value.Status.ToString(),
                    Description = t.Value.Description,
                    Tags = string.Join(", ", t.Value.Tags),
                    Message = t.Value.Exception?.Message ?? "OK",
                    Bubble = BuildBubble(t.Value)
                }).ToDataSourceResultAsync(request));
        }

        public async Task<IActionResult> ReadTestDetails([DataSourceRequest] DataSourceRequest request, [FromQuery]string checkName)
        {
            var healthStatus = await health.CheckHealthAsync(p => p.Name == checkName);
            bool ok = healthStatus.Entries.TryGetValue(checkName, out var detailItem);
            if (ok)
            {
                return Json(await (from t in detailItem.Data.Where(n => n.Value is IHealthDetailResult)
                    let m = (IHealthDetailResult)t.Value
                    select new HealthTestData
                    {
                        Name = t.Key,
                        Result = m.Status.ToString(),
                        Message = m.StatusText,
                        Bubble = BuildBubble(m)
                    }).ToDataSourceResultAsync(request));
            }

            return Json(await Array.Empty<HealthTestData>().ToDataSourceResultAsync(request));

        }

        [HttpPost]
        public IActionResult ReadControllerList([DataSourceRequest] DataSourceRequest request)
        {
            ControllerFeature feature = new ControllerFeature();
            partMgr.PopulateFeature(feature);
            var data = (from t in feature.Controllers
                select new ControllerDataViewModel
                {
                    FullName = t.FullName,
                    AssemblyLocation = t.Assembly.Location
                }).ToArray();

            return Json(data.ToDataSourceResult(request));
        }

        [HttpPost]
        public IActionResult ReadViewComponentList([DataSourceRequest] DataSourceRequest request)
        {
            ViewComponentFeature feature = new ();
            partMgr.PopulateFeature(feature);
            var data = (from t in feature.ViewComponents
                select new ViewComponentDataViewModel
                {
                    FullName = t.FullName,
                    AssemblyLocation = t.Assembly.Location
                }).ToArray();
            return Json(data.ToDataSourceResult(request));
        }

        [HttpPost]
        public IActionResult ReadViewList([DataSourceRequest] DataSourceRequest request)
        {
            ViewsFeature feature = new ();
            partMgr.PopulateFeature(feature);
            var data = (from t in feature.ViewDescriptors
                select new ViewDataViewModel
                {
                    Path = t.RelativePath,
                    Identifier = t.Item?.Identifier,
                    Kind = t.Item?.Kind,
                    AssemblyLocation = t.Type?.Assembly.Location
                }).ToArray();

            return Json(data.ToDataSourceResult(request));
        }

        [HttpPost]
        public IActionResult ReadTagHelperList([DataSourceRequest] DataSourceRequest request)
        {
            TagHelperFeature feature = new();
            partMgr.PopulateFeature(feature);
            var data = (from t in feature.TagHelpers
                let att = Attribute.GetCustomAttributes(t, typeof(HtmlTargetElementAttribute)).Cast<HtmlTargetElementAttribute>().ToArray()
                        select new
                {
                    FullName = t.FullName,
                    AssemblyLocation = t.Assembly.Location,
                    TargetTag = att
                    
                }).SelectMany(n => n.TargetTag.Select(t => new TagHelperDataViewModel
            {
                FullName = n.FullName,
                TargetTag = t.Tag,
                AssemblyLocation = n.AssemblyLocation
            })).Distinct().ToArray();
            return Json(data.ToDataSourceResult(request));
        }

        private HealthCheckBubbleInfo BuildBubble(IHealthDetailResult detail)
        {
            var retVal = new HealthCheckBubbleInfo
            {
                Message = detail.StatusText
            };

            ColorizeBubble(retVal, detail.Status);
            return retVal;
        }

        private HealthCheckBubbleInfo BuildBubble(HealthReportEntry entry)
        {
            var innerTests = entry.Data.Where(n => n.Value is IHealthDetailResult).ToArray();
            var det = string.Join("\r\n",
                innerTests.GroupBy(n => ((IHealthDetailResult)n.Value).Status)
                    .Select(g => $"{g.Count()} checks resulted to {g.Key}"));
            var msg = $@"{innerTests.Length} checks were performed.
{det}";
            var retVal = new HealthCheckBubbleInfo
            {
                Message = msg
            };
            ColorizeBubble(retVal, entry.Status);

            return retVal;
        }

        private void ColorizeBubble(HealthCheckBubbleInfo info, HealthStatus status)
        {
            if (status == HealthStatus.Healthy)
            {
                info.IconClass = "fa-duotone fa-check-circle";
                info.IconStyle = "color:green";
                info.PopupStyle = "background-color:green;text-shadow:0 -1px 0 #1a3c4d;color:white";
            }
            else if (status == HealthStatus.Degraded)
            {
                info.IconClass = "fa-duotone fa-exclamation";
                info.IconStyle = "color:orange";
                info.PopupStyle = "background-color:#9b7022;text-shadow:0 -1px 0 #1a3c4d;color:white";
            }
            else
            {
                info.IconClass = "fa-duotone fa-times";
                info.IconStyle = "color:red";
                info.PopupStyle = "background-color:#951616;text-shadow:0 -1px 0 #1a3c4d;color:white";
            }
        }
    }
}
