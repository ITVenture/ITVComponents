using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Factories;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.AspExtensions.PageHandler
{
    internal class FinalPageHandler<TPageModel, THandlerInterface>:IPageHandlerProvider<TPageModel, THandlerInterface>
    where TPageModel:PageModel
    where THandlerInterface:IPageHandlerInstance<TPageModel>
    {
        private readonly IPageModelFactory pageModelFactory;
        private THandlerInterface handler;

        public FinalPageHandler(IPageModelFactory pageModelFactory)
        {
            this.pageModelFactory = pageModelFactory;
        }

        public THandlerInterface Handler =>
            handler ??= pageModelFactory.CreateHandler<TPageModel, THandlerInterface>();
    }
}
