using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using ITVComponents.InterProcessCommunication.InMemory.Client;
using ITVComponents.InterProcessCommunication.InMemory.Server;
using ITVComponents.InterProcessCommunication.MessagingShared;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.NamedPipe.Client;
using ITVComponents.InterProcessCommunication.NamedPipe.Hub;
using ITVComponents.InterProcessCommunication.NamedPipe.Security;
using ITVComponents.InterProcessCommunication.NamedPipe.Server;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Test
{
    /// <summary>
    /// Who may do what at a pipe hub, the groups of a caller, and the pipe together with another transport on the same hub.
    /// </summary>
    [TestClass]
    public class NamedPipeAuthorizationTest
    {
        private const string ServiceName = "updates";
        private const string ObjectName = "facade";
        private static readonly string AuthenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null).Value;

        private readonly List<IDisposable> cleanup = new List<IDisposable>();
        private MessageServiceHub hub = null!;

        [TestInitialize]
        public void Setup()
        {
            hub = new MessageServiceHub();
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var item in Enumerable.Reverse(cleanup))
            {
                item.Dispose();
            }

            hub.Dispose();
        }

        [TestMethod]
        public void An_Unknown_Caller_Is_Disconnected()
        {
            var pipeName = EndPoint(new FakeAuthorizer { Known = false });
            LocalServer();

            var client = Track(new NamedPipeClient(pipeName, ServiceName, false));
            client.Initialize();

            Assert.ThrowsExactly<InterProcessException>(() => client.CallRemoteMethod(ObjectName, nameof(NamedPipeHubTest.Facade.WhoAmI), Array.Empty<object>()));
        }

        [TestMethod]
        public void A_Call_Needs_A_Permission_For_The_Service()
        {
            var authorizer = new FakeAuthorizer();
            var pipeName = EndPoint(authorizer);
            LocalServer();
            var client = Track(new NamedPipeClient(pipeName, ServiceName, false));
            client.Initialize();

            Assert.ThrowsExactly<InterProcessException>(() => client.CallRemoteMethod(ObjectName, nameof(NamedPipeHubTest.Facade.WhoAmI), Array.Empty<object>()));

            authorizer.Granted.Add(ServiceName);
            Assert.IsNotNull(client.CallRemoteMethod(ObjectName, nameof(NamedPipeHubTest.Facade.WhoAmI), Array.Empty<object>()));
            CollectionAssert.Contains(authorizer.Asked, $"{PipeHubPermissions.ConnectAnyService}|{ServiceName}");
        }

        [TestMethod]
        public void The_Identity_Carries_The_Groups_Of_The_Caller()
        {
            var pipeName = EndPoint(null);
            var groups = new GroupProbe();
            var server = Track(new NamedPipeServer(hub, new Dictionary<string, object> { { ObjectName, groups } }, ServiceName));
            server.Initialize();
            var client = Track(new NamedPipeClient(pipeName, ServiceName, false));
            client.Initialize();

            var sids = (string)client.CallRemoteMethod(ObjectName, nameof(GroupProbe.GroupSids), Array.Empty<object>());

            CollectionAssert.Contains(sids.Split(';'), AuthenticatedUsers, "every logged-on account is in Authenticated Users");
            var labels = new PipeUserNameMapper().GetUserLabels(groups.LastCaller!);
            CollectionAssert.Contains(labels, AuthenticatedUsers, "the security repository can be configured with the group's SID");
            CollectionAssert.Contains(labels, WindowsIdentity.GetCurrent().Name);
        }

        [TestMethod]
        public void A_Pipe_Client_Calls_A_Service_Of_Another_Transport()
        {
            var pipeName = EndPoint(null);
            var facade = new NamedPipeHubTest.Facade();
            var inMemory = Track(new InMemoryServer((IServiceHubProvider)hub, new Dictionary<string, object> { { ObjectName, facade } }, ServiceName));
            inMemory.Initialize();
            var client = Track(new NamedPipeClient(pipeName, ServiceName, true));
            client.Initialize();
            using var received = new ManualResetEventSlim();
            client.SubscribeEvent(ObjectName, nameof(NamedPipeHubTest.Facade.Shout), new Action<string>(_ => received.Set()));

            Assert.AreEqual("String", client.CallRemoteMethod(ObjectName, nameof(NamedPipeHubTest.Facade.TypeOf), new object[] { "x" }));
            facade.RaiseShout("from the in-memory service");
            Assert.IsTrue(received.Wait(TimeSpan.FromSeconds(10)), "the event of the in-memory service must reach the pipe client");
        }

        [TestMethod]
        public void A_Service_Connected_Through_The_Pipe_Serves_Another_Transport_With_Events()
        {
            var pipeName = EndPoint(null, allowRemoteServices: true);
            var facade = new NamedPipeHubTest.Facade();
            var remote = Track(new NamedPipeServer(new NamedPipeHubClientOptions { PipeName = pipeName }, new Dictionary<string, object> { { ObjectName, facade } }, ServiceName, false, false, null));
            remote.Initialize();
            var local = Track(new InMemoryClient((IServiceHubProvider)hub, ServiceName, true));
            local.Initialize();
            var pipeClient = Track(new NamedPipeClient(pipeName, ServiceName, true));
            pipeClient.Initialize();
            using var localReceived = new ManualResetEventSlim();
            using var pipeReceived = new ManualResetEventSlim();
            local.SubscribeEvent(ObjectName, nameof(NamedPipeHubTest.Facade.Shout), new Action<string>(_ => localReceived.Set()));
            pipeClient.SubscribeEvent(ObjectName, nameof(NamedPipeHubTest.Facade.Shout), new Action<string>(_ => pipeReceived.Set()));

            Assert.AreEqual("String", local.CallRemoteMethod(ObjectName, nameof(NamedPipeHubTest.Facade.TypeOf), new object[] { "x" }));
            facade.RaiseShout("from the pipe service");

            Assert.IsTrue(localReceived.Wait(TimeSpan.FromSeconds(10)), "the in-process client must receive the event");
            Assert.IsTrue(pipeReceived.Wait(TimeSpan.FromSeconds(10)), "the pipe client must receive the event of the service it subscribed to");
        }

        private string EndPoint(IPipeHubAuthorizer? authorizer, bool allowRemoteServices = false)
        {
            var pipeName = $"itv-pipe-auth-{Guid.NewGuid():N}";
            var endPoint = Track(new NamedPipeHubEndPoint(new NamedPipeHubOptions
            {
                PipeName = pipeName,
                Authorizer = authorizer,
                AllowRemoteServices = allowRemoteServices
            }, hub));
            endPoint.Initialize();
            return pipeName;
        }

        private void LocalServer()
        {
            var server = Track(new NamedPipeServer(hub, new Dictionary<string, object> { { ObjectName, new NamedPipeHubTest.Facade() } }, ServiceName));
            server.Initialize();
        }

        private T Track<T>(T item) where T : IDisposable
        {
            cleanup.Add(item);
            return item;
        }

        public sealed class GroupProbe
        {
            public ClaimsIdentity? LastCaller { get; private set; }

            public string GroupSids(IIdentity caller)
            {
                LastCaller = (ClaimsIdentity)caller;
                return string.Join(";", LastCaller.FindAll(ClaimTypes.GroupSid).Select(c => c.Value));
            }
        }

        private sealed class FakeAuthorizer : IPipeHubAuthorizer
        {
            public bool Known { get; set; } = true;

            public List<string> Granted { get; } = new List<string>();

            public List<string> Asked { get; } = new List<string>();

            public bool IsAuthenticated(ClaimsIdentity caller) => Known;

            public bool HasAnyPermission(ClaimsIdentity caller, params string[] permissions)
            {
                lock (Asked)
                {
                    Asked.Add(string.Join("|", permissions));
                    return Known && permissions.Any(p => Granted.Contains(p));
                }
            }
        }
    }
}
