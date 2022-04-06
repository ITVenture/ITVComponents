using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers
{
    public interface ISystemTemplateHelper
    {
        SystemTemplateMarkup ExtractTemplate();

        void ApplyTemplate(SystemTemplateMarkup template);
    }
}
