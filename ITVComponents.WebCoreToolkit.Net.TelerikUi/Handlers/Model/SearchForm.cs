using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Net.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Handlers.Model
{
    public class SearchForm
    {
        public static ValueTask<SearchForm?> BindAsync(HttpContext httpContext, ParameterInfo parameter)
        {
            var newDic = Tools.TranslateForm(httpContext.Request.Form, true, false);
            return ValueTask.FromResult<SearchForm?>(new SearchForm { SearchDictionary = newDic });
        }

        public Dictionary<string, object> SearchDictionary { get; set; }

    }
}
