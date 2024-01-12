using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Net.Handlers.Model
{
    public class TenantSwitchForm
    {
        public string NewTenant { get; set; }

        public static ValueTask<TenantSwitchForm?> BindAsync(HttpContext httpContext, ParameterInfo parameter)
        {
            var newDic = Tools.TranslateForm(httpContext.Request.Form);
            if (newDic.ContainsKey("NewTenant"))
            {
                return ValueTask.FromResult<TenantSwitchForm?>(new TenantSwitchForm
                    { NewTenant = (string)newDic["NewTenant"] });
            }

            return ValueTask.FromResult(default(TenantSwitchForm));
        }
    }
}
