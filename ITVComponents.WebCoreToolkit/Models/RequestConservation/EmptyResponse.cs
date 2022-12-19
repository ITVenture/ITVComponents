using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Models.RequestConservation
{
    internal class EmptyResponse:HttpResponse
    {
        private bool hasStarted;

        public EmptyResponse(HttpContext context)
        {
            HttpContext = context;
        }

        public override void OnStarting(Func<object, Task> callback, object state)
        {
            hasStarted = true;
        }

        public override void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public override void Redirect(string location, bool permanent)
        {
        }

        public override HttpContext HttpContext { get; }
        public override int StatusCode { get; set; }
        public override IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public override Stream Body { get; set; }
        public override long? ContentLength { get; set; }
        public override string ContentType { get; set; }
        public override IResponseCookies Cookies { get; } = new ConservedReqResCookies();
        public override bool HasStarted => hasStarted;
    }
}
