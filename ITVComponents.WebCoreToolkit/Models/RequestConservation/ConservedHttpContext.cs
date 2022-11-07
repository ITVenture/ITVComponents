using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace ITVComponents.WebCoreToolkit.Models.RequestConservation
{
    internal class ConservedHttpContext:HttpContext
    {
        private IFeatureCollection features;
        private IServiceProvider requestServices;

        public ConservedHttpContext(HttpContext original)
        {
            Request = original.Request.Conserve(this);
            Response = original.Response.Conserve(this);
            Connection = original.Connection.Conserve();
            User = original.User;
            Items = new Dictionary<object, object>(original.Items);
            TraceIdentifier = original.TraceIdentifier;
            try
            {
                Session = original.Session.Conserve();
            }
            catch
            {
            }
        }

        public override void Abort()
        {
            throw new NotImplementedException();
        }

        public sealed override IFeatureCollection Features => features;
        public sealed override HttpRequest Request { get; }
        public sealed override HttpResponse Response { get; }
        public sealed override ConnectionInfo Connection { get; }
        public sealed override WebSocketManager WebSockets { get; }
        public sealed override ClaimsPrincipal User { get; set; }
        public sealed override IDictionary<object, object> Items { get; set; }

        public sealed override IServiceProvider RequestServices
        {
            get => requestServices;
            set => requestServices = value;
        }

        public sealed override CancellationToken RequestAborted { get; set; }
        public sealed override string TraceIdentifier { get; set; }
        public sealed override ISession Session { get; set; }
    }
}
