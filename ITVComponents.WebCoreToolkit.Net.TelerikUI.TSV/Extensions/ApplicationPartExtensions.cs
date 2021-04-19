using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Extensions
{
    public static class ApplicationPartExtensions
    {
        public static ApplicationPartManager EnableItvTenantViews(this ApplicationPartManager manager)
        {
            ApplicationPart part = new AssemblyPart(typeof(ApplicationPartExtensions).Assembly);
            manager.ApplicationParts.Add(part);
            return manager;
        }
    }
}
