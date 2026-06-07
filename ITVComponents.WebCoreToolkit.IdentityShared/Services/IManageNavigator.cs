using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.IdentityShared.Services.Options;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Services
{
    public interface IManageNavigator
    {
        string ActivePage { get; set; }

        IEnumerable<ManageNavPage> NavPages { get; }

        string PageNavClass(ViewContext viewContext, string page);
    }
}
