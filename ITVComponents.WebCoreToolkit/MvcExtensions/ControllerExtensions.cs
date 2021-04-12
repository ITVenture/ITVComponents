using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ITVComponents.WebCoreToolkit.MvcExtensions
{
    public static class ControllerExtensions
    {
        public static async Task<bool> TryUpdateModelAsync<TViewModel, TModel>(this Controller controller, TModel model) where TViewModel : class, new() where TModel : class
        {
            TViewModel tmp = new TViewModel();
            var retVal = await controller.TryUpdateModelAsync(tmp);
            if (retVal)
            {
                tmp.CopyToDbModel(model);
            }

            return retVal;
        }

        public static async Task<bool> TryUpdateModelAsync<TViewModel, TModel>(this Controller controller, TModel model, string prefix, Func<ModelMetadata,bool> propertyFilter) where TViewModel : class, new() where TModel : class
        {
            TViewModel tmp = new TViewModel();
            var retVal = await controller.TryUpdateModelAsync(tmp, prefix, propertyFilter);
            if (retVal)
            {
                tmp.CopyToDbModel(model);
            }

            return retVal;
        }
    }
}
