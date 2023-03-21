using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions
{
    public static class ApplicationPartExtensions
    {
        public static ApplicationPartManager EnableItvExtensionViews(this ApplicationPartManager manager)
        {
            ApplicationPart part = new CompiledRazorAssemblyPart(typeof(ApplicationPartExtensions).Assembly);
            manager.ApplicationParts.Add(part);
            return manager;
        }
    }
}
