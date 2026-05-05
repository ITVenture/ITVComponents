using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.PageHandler
{
    public interface IPageHandlerProvider<TPageModel, THandlerInterface>
        where THandlerInterface : IPageHandlerInstance<TPageModel>
    {
        public THandlerInterface Handler { get; }
    }
}
