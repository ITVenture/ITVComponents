using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

namespace ITVComponents.WebCoreToolkit.Net.Handlers.Model
{
    public class MultipartFileModel
    {
        public MultipartReader FilePartReader { get; private set; }

        public static ValueTask<MultipartFileModel?> BindAsync(HttpContext httpContext, ParameterInfo parameter)
        {
            
            if (httpContext.Request.ContentType?.Contains("multipart/", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                var boundary = httpContext.Request.GetMultipartBoundary();
                return ValueTask.FromResult<MultipartFileModel?>(new MultipartFileModel
                {
                    FilePartReader = new MultipartReader(boundary, httpContext.Request.Body) { BodyLengthLimit = null }
                });
            }

            return ValueTask.FromResult(default(MultipartFileModel));
        }
    }
}
