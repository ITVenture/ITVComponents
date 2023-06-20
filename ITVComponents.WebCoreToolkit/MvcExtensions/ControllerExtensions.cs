using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public static async Task<bool> TryUpdateModelAsync<TViewModel, TModel>(this Controller controller, TModel model, Action<TViewModel, TModel> postProcess = null) where TViewModel : class, new() where TModel : class
        {
            TViewModel tmp = new TViewModel();
            var retVal = await controller.TryUpdateModelAsync(tmp);
            if (retVal)
            {
                tmp.CopyToDbModel(model);
            }

            postProcess?.Invoke(tmp,model);
            return retVal;
        }

        public static async Task<bool> TryUpdateModelAsync<TViewModel, TModel>(this Controller controller, TModel model, string prefix, Func<ModelMetadata,bool> propertyFilter, Action<TViewModel, TModel> postProcess = null) where TViewModel : class, new() where TModel : class
        {
            TViewModel tmp = new TViewModel();
            List<string> propertiesToUse = new List<string>();
            var retVal = await controller.TryUpdateModelAsync(tmp, prefix, n =>
            {
                var retVal = propertyFilter(n);
                if (retVal)
                {
                    propertiesToUse.Add(n.Name);
                }

                return retVal;
            });
            if (retVal)
            {
                tmp.CopyToDbModel(model, n => propertiesToUse.Contains(n));
            }

            postProcess?.Invoke(tmp, model);
            return retVal;
        }
    }
}
