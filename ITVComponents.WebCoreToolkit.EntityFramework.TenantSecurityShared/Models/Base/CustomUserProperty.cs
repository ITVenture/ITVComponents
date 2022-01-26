using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    public abstract class CustomUserProperty<TUserId,TUser>:WebCoreToolkit.Models.CustomUserProperty
    {
        [Key]
        public int CustomUserPropertyId { get; set; }

        public TUserId UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual TUser User { get; set; }
    }
}
