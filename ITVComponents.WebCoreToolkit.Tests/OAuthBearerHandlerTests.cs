using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.ExternalServiceConnect.Impl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Der Wiederholungsweg des <see cref="OAuthBearerHandler"/>.
    /// </summary>
    /// <remarks>
    /// Der Handler behaelt das Token ueber Aufrufe hinweg. Laeuft es ab, antwortet der Anbieter mit 401 - und
    /// genau dann schickte der Handler DIESELBE Anfrage ein zweites Mal, was mit "already sent" warf. Jeder
    /// Client, der laenger lebte als ein Token, brach so beim ersten Aufruf nach dem Ablauf.
    /// </remarks>
    [TestClass]
    public class OAuthBearerHandlerTests
    {
        [TestMethod]
        public async Task ExpiredBufferedToken_IsReplacedAndRequestResentWithBody()
        {
            var tokens = new QueueTokenService("t1", "t2");
            var inner = new RecordingHandler(auth => auth == "Bearer t1" && tokensUsed > 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK);
            using var client = new HttpClient(new OAuthBearerHandler(tokens, "svc") { InnerHandler = inner });

            tokensUsed = 1;
            var first = await client.PostAsync("https://example.test/a", new StringContent("one", Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);

            tokensUsed = 2;
            var second = await client.PostAsync("https://example.test/b", new StringContent("two", Encoding.UTF8, "application/json"));

            Assert.AreEqual(HttpStatusCode.OK, second.StatusCode);
            Assert.AreEqual(3, inner.Calls.Count);
            Assert.AreEqual("Bearer t2", inner.Calls[2].Auth);
            Assert.AreEqual("two", inner.Calls[2].Body, "the copy must carry the original body");
            Assert.AreEqual("application/json; charset=utf-8", inner.Calls[2].ContentType);
        }

        [TestMethod]
        public async Task SameTokenAfter401_IsNotRetried()
        {
            var tokens = new QueueTokenService("t1", "t1", "t1");
            var inner = new RecordingHandler(_ => inner2Status);
            using var client = new HttpClient(new OAuthBearerHandler(tokens, "svc") { InnerHandler = inner });

            inner2Status = HttpStatusCode.OK;
            await client.GetAsync("https://example.test/a");
            inner2Status = HttpStatusCode.Unauthorized;
            var second = await client.GetAsync("https://example.test/b");

            Assert.AreEqual(HttpStatusCode.Unauthorized, second.StatusCode);
            Assert.AreEqual(2, inner.Calls.Count, "a retry with the token that was just rejected gains nothing");
        }

        private int tokensUsed;
        private HttpStatusCode inner2Status;

        private sealed class QueueTokenService : IOAuthTokenService
        {
            private readonly Queue<string> tokens;

            public QueueTokenService(params string[] tokens)
            {
                this.tokens = new Queue<string>(tokens);
            }

            public string GetValidAccessToken(string connectionName) => tokens.Dequeue();

            public Task<string> GetValidAccessTokenAsync(string connectionName) => Task.FromResult(tokens.Dequeue());

            public Task RevokeAsync(string connectionName) => Task.CompletedTask;
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly System.Func<string, HttpStatusCode> answer;

            public RecordingHandler(System.Func<string, HttpStatusCode> answer)
            {
                this.answer = answer;
            }

            public List<(string Auth, string Body, string ContentType)> Calls { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var auth = request.Headers.Authorization?.ToString();
                var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : null;
                Calls.Add((auth, body, request.Content?.Headers.ContentType?.ToString()));
                return new HttpResponseMessage(answer(auth)) { RequestMessage = request };
            }
        }
    }
}
