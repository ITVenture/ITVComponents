using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;


namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.ViewComponents
{
    public class ModuleTemplateContainer: ViewComponent
    {
        private readonly IBaseTenantContext db;

        public ModuleTemplateContainer(IBaseTenantContext db)
        {
            this.db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync(string templateName, TemplateModule module = null, string subElement = "Default")
        {
            ViewData["templateName"] = templateName;
            var template = module ?? db.TemplateModules.First(n => n.TemplateModuleName == templateName);
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
