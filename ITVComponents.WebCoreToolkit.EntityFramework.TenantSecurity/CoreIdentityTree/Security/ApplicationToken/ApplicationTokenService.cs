using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Security.ApplicationToken
{
    internal class ApplicationTokenService:ApplicationTokenService<AspNetTreeSecurityContext>
    {
        public ApplicationTokenService(IToolkitContextFactory contextFactory) : base(contextFactory)
        {
        }
    }
}
