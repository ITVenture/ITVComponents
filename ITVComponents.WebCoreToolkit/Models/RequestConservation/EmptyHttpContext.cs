using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace ITVComponents.WebCoreToolkit.Models.RequestConservation
{
    internal class EmptyHttpContext:HttpContext
    {
        public EmptyHttpContext()
        {
            Request = new EmptyRequest(this){RouteValues = new RouteValueDictionary()};
            Response = new EmptyResponse(this);
        }

        public override void Abort()
        {
        }

        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override HttpRequest Request { get; } 
        public override HttpResponse Response { get; }
        public override ConnectionInfo Connection { get; } = new EmptyConnectionInfo();
        public override WebSocketManager WebSockets { get; }
        public override ClaimsPrincipal User { get; set; } = new ClaimsPrincipal();
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
        public override IServiceProvider RequestServices { get; set; }
        public override CancellationToken RequestAborted { get; set; } = CancellationToken.None;
        public override string TraceIdentifier { get; set; } = "--EMPTY--";
        public override ISession Session { get; set; } = new ConservedSession();
    }
}
