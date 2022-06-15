using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class RequestExtensions
    {
        public static IQueryCollection GetRefererQuery(this HttpRequest request)
        {
            var rff = request.GetTypedHeaders().Referer;
            if (rff != null && !string.IsNullOrEmpty(rff.LocalPath))
            {
                var q = QueryHelpers.ParseNullableQuery(rff.Query);
                if (q != null)
                {
                    return new QueryCollection(q);
                }
            }

            return null;
        }
    }
}
