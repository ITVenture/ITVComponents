using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using ITVComponents.InterProcessCommunication.MessagingShared;
using ITVComponents.InterProcessCommunication.MessagingShared.Client;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Factory;
using ITVComponents.InterProcessCommunication.MessagingShared.Messages;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.InterProcessCommunication.NamedPipe.Client;
using ITVComponents.InterProcessCommunication.NamedPipe.Hub;
using ITVComponents.InterProcessCommunication.NamedPipe.Security;
using ITVComponents.InterProcessCommunication.NamedPipe.Server;
using ITVComponents.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Test
{
    /// <summary>
    /// The pipe hub over a real named pipe, in one process: the service registers in the hub's process, the client
    /// connects through the pipe - as an application connects to a Windows service.
    /// </summary>
    [TestClass]
    public class NamedPipeHubTest
    {
        private const string ServiceName = "updates";
        private const string ObjectName = "facade";

        private string pipeName = null!;
        private MessageServiceHub hub = null!;
        private NamedPipeHubEndPoint endPoint = null!;
        private Facade facade = null!;
        private NamedPipeServer server = null!;
        private readonly List<IDisposable> cleanup = new List<IDisposable>();

        [TestInitialize]
        public void Setup()
        {
            pipeName = $"itv-pipe-test-{Guid.NewGuid():N}";
            hub = new MessageServiceHub();
            facade = new Facade();
            server = new NamedPipeServer(hub, new Dictionary<string, object> { { ObjectName, facade } }, ServiceName);
            server.Initialize();
            endPoint = new NamedPipeHubEndPoint(pipeName, hub);
            endPoint.Initialize();
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var item in Enumerable.Reverse(cleanup))
            {
                item.Dispose();
            }

            endPoint.Dispose();
            server.Dispose();
            hub.Dispose();
        }

        [TestMethod]
        public void A_Call_Runs_Under_The_Identity_The_Pipe_Reports()
        {
            var client = Client(new NamedPipeHubClientOptions { PipeName = pipeName }, false);

            var me = (string)client.CallRemoteMethod(ObjectName, nameof(Facade.WhoAmI), Array.Empty<object>());

            using var process = Process.GetCurrentProcess();
            Assert.AreEqual($"{WindowsIdentity.GetCurrent().User!.Value}|{process.SessionId}|{process.Id}|{PipeClaimTypes.AuthenticationType}", me);
        }

        [TestMethod]
        public void An_Identity_The_Client_Claims_Is_Ignored()
        {
            var client = Track(new ForgingClient(new NamedPipeHubConnectionFactory(new NamedPipeHubClientOptions { PipeName = pipeName }, ServiceName, false), ServiceName));
            client.Initialize();

            var me = (string)client.CallRemoteMethod(ObjectName, nameof(Facade.WhoAmI), Array.Empty<object>());

            StringAssert.StartsWith(me, WindowsIdentity.GetCurrent().User!.Value + "|", "the forged identity must not reach the service");
        }

        [TestMethod]
        public void Events_Reach_The_Client_That_Subscribed()
        {
            var client = Client(new NamedPipeHubClientOptions { PipeName = pipeName }, true);
            using var received = new ManualResetEventSlim();
            string? text = null;
            client.SubscribeEvent(ObjectName, nameof(Facade.Shout), new Action<string>(s =>
            {
                text = s;
                received.Set();
            }));

            facade.RaiseShout("stop now");

            Assert.IsTrue(received.Wait(TimeSpan.FromSeconds(10)), "the event did not arrive");
            Assert.AreEqual("stop now", text);
        }

        [TestMethod]
        public void A_Pipe_Client_Cannot_Register_A_Service()
        {
            IHubConnection squatter = Track(new NamedPipeServiceHubConsumer(new NamedPipeHubClientOptions { PipeName = pipeName }, "another-service", null, null));

            squatter.Initialize();

            Assert.IsFalse(squatter.Operational);
            Assert.IsFalse(hub.Broker.DiscoverService(new MessagingShared.Hub.Protocol.ServiceDiscoverMessage { TargetService = "another-service" }).Ok);
        }

        [TestMethod]
        public void The_Back_Channel_Of_Another_Client_Cannot_Be_Reached()
        {
            var backChannel = Guid.NewGuid().ToString();
            IHubConnection listener = Track(new NamedPipeServiceHubConsumer(new NamedPipeHubClientOptions { PipeName = pipeName }, backChannel, ServiceName, null));
            listener.Initialize();
            Assert.IsTrue(listener.Operational);
            IHubConnection intruder = Track(new NamedPipeServiceHubConsumer(new NamedPipeHubClientOptions { PipeName = pipeName }, null, ServiceName, null));
            intruder.Initialize();

            Assert.IsFalse(intruder.DiscoverService(backChannel));
            var answer = intruder.InvokeService(backChannel, "{}");
            StringAssert.Contains(answer, "back channel");
        }

        [TestMethod]
        public void Events_Cannot_Be_Cancelled_For_Another_Client()
        {
            var victim = Client(new NamedPipeHubClientOptions { PipeName = pipeName }, true);
            using var received = new ManualResetEventSlim();
            victim.SubscribeEvent(ObjectName, nameof(Facade.Shout), new Action<string>(_ => received.Set()));
            var victimChannel = BackChannelOf(victim);

            IHubConnection intruder = Track(new NamedPipeServiceHubConsumer(new NamedPipeHubClientOptions { PipeName = pipeName }, null, ServiceName, null));
            intruder.Initialize();
            var answer = intruder.InvokeService(ServiceName, JsonHelper.ToJson(new UnRegisterEventRequestMessage
            {
                TargetObject = ObjectName,
                EventName = nameof(Facade.Shout),
                RespondChannel = victimChannel
            }, SerializationTypingMode.NativePolymorphism, typeof(IRequestMessage), true));
            StringAssert.Contains(answer, "does not belong to the caller");

            facade.RaiseShout("still subscribed");
            Assert.IsTrue(received.Wait(TimeSpan.FromSeconds(10)), "the victim must still receive its events");
        }

        [TestMethod]
        public void A_Pipe_Not_Owned_By_The_Expected_Account_Is_Refused()
        {
            IHubConnection wrongOwner = Track(new NamedPipeServiceHubConsumer(new NamedPipeHubClientOptions
            {
                PipeName = pipeName,
                ExpectedServerOwner = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null)
            }, null, ServiceName, null));
            wrongOwner.Initialize();
            Assert.IsFalse(wrongOwner.Operational, "the test runs as a user, so the hub's pipe is not owned by SYSTEM");

            IHubConnection rightOwner = Track(new NamedPipeServiceHubConsumer(new NamedPipeHubClientOptions
            {
                PipeName = pipeName,
                ExpectedServerOwner = WindowsIdentity.GetCurrent().User
            }, null, ServiceName, null));
            rightOwner.Initialize();
            Assert.IsTrue(rightOwner.Operational);
        }

        [TestMethod]
        public void A_Second_Hub_Cannot_Take_The_Same_Pipe()
        {
            using var otherHub = new MessageServiceHub();
            using var squatter = new NamedPipeHubEndPoint(pipeName, otherHub);

            Assert.Throws<Exception>(() => squatter.Initialize());
        }

        [TestMethod]
        public void An_Argument_Of_An_Unregistered_Type_Arrives_As_Null()
        {
            var client = Client(new NamedPipeHubClientOptions { PipeName = pipeName }, false);

            Assert.AreEqual("String", client.CallRemoteMethod(ObjectName, nameof(Facade.TypeOf), new object[] { "text" }));
            Assert.AreEqual("null", client.CallRemoteMethod(ObjectName, nameof(Facade.TypeOf), new object[] { new Unregistered { Value = "x" } }));
        }

        [TestMethod]
        public void The_Hub_Reports_Connecting_And_Disconnecting_Clients()
        {
            using var connected = new ManualResetEventSlim();
            using var disconnected = new ManualResetEventSlim();
            PipeClientIdentity? seen = null;
            endPoint.ClientConnected += (_, e) =>
            {
                seen = e.Client;
                connected.Set();
            };
            endPoint.ClientDisconnected += (_, _) => disconnected.Set();

            var client = new NamedPipeClient(pipeName, ServiceName, false);
            client.Initialize();
            client.CallRemoteMethod(ObjectName, nameof(Facade.WhoAmI), Array.Empty<object>());
            Assert.IsTrue(connected.Wait(TimeSpan.FromSeconds(10)));
            using (var process = Process.GetCurrentProcess())
            {
                Assert.AreEqual(process.Id, seen!.ProcessId);
                Assert.AreEqual(process.SessionId, seen.SessionId);
                Assert.AreEqual(process.MainModule!.FileName, seen.ProcessPath, true);
            }

            client.Dispose();
            Assert.IsTrue(disconnected.Wait(TimeSpan.FromSeconds(10)));
            Assert.AreEqual(0, endPoint.ConnectedClients.Count);
        }

        private NamedPipeClient Client(NamedPipeHubClientOptions options, bool useEvents)
        {
            var client = Track(new NamedPipeClient(options, ServiceName, useEvents));
            client.Initialize();
            return client;
        }

        private static string BackChannelOf(MessageClient client)
        {
            var field = typeof(MessageClient).GetField("connection", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return ((IHubConnection)field.GetValue(client)!).ServiceName;
        }

        private T Track<T>(T item) where T : IDisposable
        {
            cleanup.Add(item);
            return item;
        }

        /// <summary>
        /// What a client may do - and nothing else.
        /// </summary>
        public sealed class Facade
        {
            public event Action<string>? Shout;

            public string WhoAmI(IIdentity caller)
            {
                var claims = (ClaimsIdentity)caller;
                return string.Join("|",
                    claims.FindFirst(ClaimTypes.PrimarySid)?.Value,
                    claims.FindFirst(PipeClaimTypes.SessionId)?.Value,
                    claims.FindFirst(PipeClaimTypes.ProcessId)?.Value,
                    claims.AuthenticationType);
            }

            public string TypeOf(object? value)
            {
                return value?.GetType().Name ?? "null";
            }

            public void RaiseShout(string text)
            {
                Shout?.Invoke(text);
            }
        }

        public sealed class Unregistered
        {
            public string? Value { get; set; }
        }

        /// <summary>
        /// A client that claims to be someone else.
        /// </summary>
        private sealed class ForgingClient : MessageClient
        {
            public ForgingClient(IHubConnectionFactory factory, string targetService)
                : base(factory, targetService, new ForgedIdentity(), false)
            {
            }
        }

        private sealed class ForgedIdentity : IIdentityProvider
        {
            public TransferIdentity CurrentIdentity => new TransferIdentity
            {
                AuthenticationType = PipeClaimTypes.AuthenticationType,
                IsAuthenticated = true,
                NameType = ClaimTypes.Name,
                RoleType = ClaimTypes.Role,
                Claims = new List<TransferClaim>
                {
                    new TransferClaim { Type = ClaimTypes.Name, Value = @"EVIL\mallory" },
                    new TransferClaim { Type = ClaimTypes.PrimarySid, Value = "S-1-5-18" },
                    new TransferClaim { Type = PipeClaimTypes.SessionId, Value = "0" }
                }
            };
        }
    }
}
