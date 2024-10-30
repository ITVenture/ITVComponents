using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.VirtualModels
{
    public class UpwardsRoleUserPermissionsView<TUserId>
    {
        public TUserId UserId { get; set; }

        public int ParentTenantId { get; set; }

        public string ParentTenantName { get; set; }

        public int TenantUserId { get; set; }

        public int OutermostLeafTenantId { get; set; }

        public string OutermostLeafTenantName { get; set; }

        public string PermissionName { get; set; }

        public int ParentLevel { get; set; }
    }
}
