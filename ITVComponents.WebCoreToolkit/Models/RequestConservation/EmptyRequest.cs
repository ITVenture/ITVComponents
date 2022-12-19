using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.Models.RequestConservation
{
    internal class EmptyRequest: HttpRequest
    {
        public EmptyRequest(EmptyHttpContext parent)
        {
            HttpContext = parent;
            Cookies = new ConservedReqResCookies();
        }
        public override Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.FromResult<IFormCollection>(new FormCollection(new Dictionary<string, StringValues>()));
        }

        public override HttpContext HttpContext { get; }
        public override string Method { get; set; } = "NONE";
        public override string Scheme { get; set; } = "NONE";
        public override bool IsHttps { get; set; } = false;
        public override HostString Host { get; set; }
        public override PathString PathBase { get; set; }
        public override PathString Path { get; set; }
        public override QueryString QueryString { get; set; }
        public override IQueryCollection Query { get; set; }
        public override string Protocol { get; set; }
        public override IHeaderDictionary Headers { get; }
        public override IRequestCookieCollection Cookies { get; set; }
        public override long? ContentLength { get; set; }
        public override string ContentType { get; set; }
        public override Stream Body { get; set; }
        public override bool HasFormContentType { get; }
        public override IFormCollection Form { get; set; }
    }
}
