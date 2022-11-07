using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.Models.RequestConservation
{
    internal class ConservedHttpResponse:HttpResponse
    {

        public ConservedHttpResponse(HttpResponse original, HttpContext parent)
        {
            HttpContext = parent;
            StatusCode = original.StatusCode;
            Headers = new HeaderDictionary(new Dictionary<string, StringValues>(original.Headers));
            Body = new MemoryStream();
            Cookies = new ConservedReqResCookies();
            HasStarted = false;
        }
        public override void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public override void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public override void Redirect(string location, bool permanent)
        {
        }

        public sealed override HttpContext HttpContext { get; }
        public sealed override int StatusCode { get; set; }
        public sealed override IHeaderDictionary Headers { get; }
        public sealed override Stream Body { get; set; }
        public sealed override long? ContentLength { get; set; }
        public sealed override string ContentType { get; set; }
        public sealed override IResponseCookies Cookies { get; }
        public sealed override bool HasStarted { get; }
    }
}
