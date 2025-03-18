using ITVComponents.EFRepo.DataAnnotations;
using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.BinderContext.Model
{
    [BinderEntity]
    public class BinderUser
    {
        [Key]
        public int UserId { get; set; }

        public string UserName { get; set; }
    }
}
