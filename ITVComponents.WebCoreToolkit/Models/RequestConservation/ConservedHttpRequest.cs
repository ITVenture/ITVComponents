using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.Models.RequestConservation
{
    internal class ConservedHttpRequest:HttpRequest
    {
        public ConservedHttpRequest(HttpRequest original, ConservedHttpContext parent)
        {
            HttpContext = parent;
            Method = original.Method;
            Scheme = original.Scheme;
            IsHttps = original.IsHttps;
            Host = new HostString(original.Host.Value);
            PathBase = new PathString(original.PathBase.Value);
            Path = new PathString(original.Path.Value);
            QueryString = new QueryString(original.QueryString.Value);
            Query = new QueryCollection(new Dictionary<string, StringValues>(original.Query));
            Protocol = original.Protocol;
            Headers = new HeaderDictionary(new Dictionary<string, StringValues>(original.Headers));
            Cookies = new ConservedReqResCookies(original.Cookies);
            ContentLength = 0;
            ContentType = null;
            Body = null;
            HasFormContentType = false;
            Form = null;
        }

        public override Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public sealed override HttpContext HttpContext { get; }
        public sealed override string Method { get; set; }
        public sealed override string Scheme { get; set; }
        public sealed override bool IsHttps { get; set; }
        public sealed override HostString Host { get; set; }
        public sealed override PathString PathBase { get; set; }
        public sealed override PathString Path { get; set; }
        public sealed override QueryString QueryString { get; set; }
        public sealed override IQueryCollection Query { get; set; }
        public sealed override string Protocol { get; set; }
        public sealed override IHeaderDictionary Headers { get; }
        public sealed override IRequestCookieCollection Cookies { get; set; }
        public sealed override long? ContentLength { get; set; }
        public sealed override string ContentType { get; set; }
        public sealed override Stream Body { get; set; }
        public sealed override bool HasFormContentType { get; }
        public sealed override IFormCollection Form { get; set; }
    }
}
