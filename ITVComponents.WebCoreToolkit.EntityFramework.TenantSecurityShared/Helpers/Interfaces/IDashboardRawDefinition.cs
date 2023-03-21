using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Interfaces
{
    public interface IDashboardRawDefinition
    {
        public string DisplayName { get; set; }

        public string TitleTemplate { get; set; }

        public string Template { get; set; }
    }
}
