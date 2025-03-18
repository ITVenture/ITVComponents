using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ITVComponents.WebCoreToolkit.AspExtensions.PageHandler
{
    public interface IPageHandlerProvider<TPageModel, THandlerInterface>
        where TPageModel : PageModel
        where THandlerInterface : IPageHandlerInstance<TPageModel>
    {
        public THandlerInterface Handler { get; }
    }
}
