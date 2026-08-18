using ITVComponents.EFRepo.DataSync;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Configuration
{
    public static class HelpConfigExtensionRegistration
    {
        /// <summary>
        /// Registers the help system as a system-config export section, so topics, their localized contents and
        /// the resource library round-trip in the downloadable system configuration. Requires a config-handler
        /// host (TenantSecurity) whose context also implements <c>IHelpSystemContext</c>; on a context without the
        /// help tables the section describes nothing and compares nothing.
        /// </summary>
        /// <param name="services">The service collection being configured.</param>
        /// <param name="contents">
        /// How much of the resource library travels with the section; omitted means the defaults of
        /// <see cref="HelpConfigExportOptions"/>.
        /// </param>
        public static IServiceCollection AddHelpConfigExtension(this IServiceCollection services, HelpConfigExportOptions? contents = null)
        {
            services.Configure<HelpConfigExportOptions>(o =>
            {
                var source = contents ?? new HelpConfigExportOptions();
                o.IncludeResourceContents = source.IncludeResourceContents;
                o.MaxFileBytes = source.MaxFileBytes;
                o.MaxTotalBytes = source.MaxTotalBytes;
            });

            return services.AddSystemConfigExtension<HelpConfigMarkup>();
        }
    }
}
