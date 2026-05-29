using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers
{
    public interface ISystemTemplateHelper
    {
        SystemTemplateMarkup ExtractTemplate();

        void ApplyTemplate(SystemTemplateMarkup template);
    }
}
