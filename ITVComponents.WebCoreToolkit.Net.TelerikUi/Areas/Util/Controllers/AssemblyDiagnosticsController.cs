using System;
using System.Linq;
using System.Runtime.Loader;
using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Areas.Util.Controllers
{
    [Authorize("HasPermission(AssemblyDiagnostics.View)"), Area("Util")]
    public class AssemblyDiagnosticsController:Controller
    {
        public ViewResult Index()
        {
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
    }
}
