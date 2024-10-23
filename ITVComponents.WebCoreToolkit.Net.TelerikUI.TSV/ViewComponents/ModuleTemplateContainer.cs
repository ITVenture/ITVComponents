using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewComponents
{
    public class ModuleTemplateContainer: ViewComponent
    {
        private readonly ICoreSystemContext db;

        public ModuleTemplateContainer(ICoreSystemContext db)
        {
            this.db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync(string templateName, TemplateModule module = null, string subElement = "Default")
        {
            ViewData["templateName"] = templateName;
            var template = module ?? db.TemplateModules.First(n => n.TemplateModuleName.ToLower() == templateName.ToLower());
            switch (subElement)
            {
                case "scripts":

                    return View("scripts", (from t in template.Scripts select t.ScriptFile).ToList());
                case "container":
                    return View("container", template);
            }

            return View(template);
        }
    }
}
