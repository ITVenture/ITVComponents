using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewComponents
{
    public class Dashboard : ViewComponent
    {
        private readonly IDiagnosticsStore context;
        private IRequestCultureFeature cult;

        public Dashboard(IDiagnosticsStore context)
        {
            this.context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync(string name, int columnCount = 3)
        {
            cult ??= HttpContext.Features.Get<IRequestCultureFeature>();
            string currentCulture = null;
            if (cult != null)
            {
                currentCulture = cult.RequestCulture.UICulture.Name;
            }

            ViewData["name"] = name;
            ViewData["EligibleDBItems"] = context.GetWidgetTemplates(currentCulture)
                .Where(t => HttpContext.RequestServices.VerifyUserPermissions(new[]
                    { t.DiagnosticsQuery.Permission}))
                .Select(t => new ForeignKeyData<string>
                {
                    Key = t.SystemName,
                    Label = t.DisplayName.Translate(currentCulture),
                    FullRecord = t.ToDictionary(true)
                }).ToArray();
            ViewData["DashboardItems"] = context.GetUserWidgets(HttpContext.User.Identity.Name, currentCulture).Where(t => HttpContext.RequestServices.VerifyUserPermissions(new[] { t.DiagnosticsQuery.Permission })).ToArray();
            ViewData["columnCount"] = columnCount;
            return View();
        }
    }
}
