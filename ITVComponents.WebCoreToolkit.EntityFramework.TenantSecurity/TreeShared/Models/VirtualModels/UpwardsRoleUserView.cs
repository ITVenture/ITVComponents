using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.VirtualModels
{
    public class UpwardsRoleUserView<TUserId>
    {
        public TUserId UserId { get; set; }

        public int ParentTenantId { get; set; }

        public string ParentTenantName { get; set; }

        public int TenantUserId { get; set; }

        public int OutermostLeafTenantId { get; set; }

        public string OutermostLeafTenantName { get; set; }

        public int ParentLevel { get; set; }

        public int OutermostRoleId { get; set; }
    }
}
