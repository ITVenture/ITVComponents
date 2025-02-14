namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Security.ApplicationToken
{
    internal class ApplicationTokenService:ApplicationTokenService<AspNetTreeSecurityContext>
    {
        public ApplicationTokenService(AspNetTreeSecurityContext context) : base(context)
        {
        }
    }
}
