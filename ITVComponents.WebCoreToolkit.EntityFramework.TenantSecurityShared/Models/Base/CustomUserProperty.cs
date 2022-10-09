using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Linq;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    [Index(nameof(UserId), nameof(PropertyType), nameof(PropertyName), IsUnique = true, Name = "UniqueUserProp")]
    public abstract class CustomUserProperty<TUserId,TUser>:WebCoreToolkit.Models.CustomUserProperty
    {
        [Key]
        public int CustomUserPropertyId { get; set; }

        public TUserId UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual TUser User { get; set; }
    }
}
