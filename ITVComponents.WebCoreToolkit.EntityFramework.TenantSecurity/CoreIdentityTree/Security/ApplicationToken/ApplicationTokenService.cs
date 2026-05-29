namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Security.ApplicationToken
{
    internal class ApplicationTokenService:ApplicationTokenService<AspNetTreeSecurityContext>
    {
        public ApplicationTokenService(AspNetTreeSecurityContext context) : base(context)
        {
        }
    }
}
