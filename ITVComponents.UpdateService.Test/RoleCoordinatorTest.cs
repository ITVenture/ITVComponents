using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ITVComponents.GenericService.Sessions;
using ITVComponents.InterProcessCommunication.NamedPipe.Security;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Agents;
using ITVComponents.UpdateService.Sessions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.UpdateService.Test
{
    /// <summary>
    /// One active instance, any number waiting - and the handover between sessions (issue, 6 and 7).
    /// </summary>
    [TestClass]
    public class RoleCoordinatorTest
    {
        private Clock clock = null!;
        private Sessions sessions = null!;
        private Launcher launcher = null!;
        private AgentRegistry registry = null!;
        private RoleCoordinator roles = null!;
        private bool held;
        private readonly List<RoleChange> changes = new List<RoleChange>();

        [TestInitialize]
        public void Setup()
        {
            clock = new Clock();
            sessions = new Sessions { Console = 1 };
            launcher = new Launcher();
            registry = new AgentRegistry(new UpdateServiceOptions { ProductId = "Shop.Agent", RequireApplicationPath = false });
            roles = new RoleCoordinator(registry, sessions, launcher, () => held, new RoleOptions(), clock);
            roles.RoleChanged += c => changes.Add(c);
        }

        [TestMethod]
        public void The_First_Instance_Is_Active_A_Further_One_Waits()
        {
            var a = Register(session: 1);
            var b = Register(session: 2);

            Assert.AreEqual(AgentRole.Active, roles.RoleOf(a));
            Assert.AreEqual(AgentRole.Waiting, roles.RoleOf(b));
        }

        [TestMethod]
        public void A_Second_Instance_In_The_Same_Session_Is_Refused()
        {
            Register(session: 1);

            var second = registry.Register(Identity(1, 999), "Shop.Agent", "1.0", false, out var reason, out var running);

            Assert.IsNull(second);
            Assert.IsNotNull(running);
            StringAssert.Contains(reason, "already runs in this session");
        }

        [TestMethod]
        public void Locking_Alone_Is_No_Reason_To_Hand_Over()
        {
            var a = Register(session: 1);
            Register(session: 2);
            roles.SessionChanged(ServiceSessionChange.SessionLock, 1);

            Assert.AreEqual(AgentRole.Active, roles.RoleOf(a));
            Assert.IsFalse(changes.Any(c => c.Role == AgentRole.Waiting));
        }

        [TestMethod]
        public void Another_Session_Becoming_Interactive_Takes_Over_After_The_Release()
        {
            var a = Register(session: 1);
            var b = Register(session: 2);
            changes.Clear();

            roles.SessionChanged(ServiceSessionChange.SessionLock, 1);
            roles.SessionChanged(ServiceSessionChange.SessionUnlock, 2);

            Assert.AreEqual(1, changes.Count, "first only the release");
            Assert.AreEqual(a.ProcessId, changes[0].ProcessId);
            Assert.AreEqual(AgentRole.Waiting, changes[0].Role);
            Assert.AreEqual(AgentRole.Waiting, roles.RoleOf(b), "b waits until a has released");

            roles.Released(a);

            Assert.AreEqual(AgentRole.Active, changes.Last().Role);
            Assert.AreEqual(b.ProcessId, changes.Last().ProcessId);
            Assert.AreEqual(AgentRole.Active, roles.RoleOf(b));
            Assert.AreEqual(AgentRole.Waiting, roles.RoleOf(a));
        }

        [TestMethod]
        public void A_Release_That_Takes_Too_Long_Is_Handed_Over_Anyway()
        {
            Register(session: 1);
            var b = Register(session: 2);
            roles.SessionChanged(ServiceSessionChange.SessionLock, 1);
            roles.SessionChanged(ServiceSessionChange.SessionUnlock, 2);

            clock.Advance(TimeSpan.FromSeconds(16));
            roles.Tick();

            Assert.AreEqual(AgentRole.Active, roles.RoleOf(b));
        }

        [TestMethod]
        public void When_Both_Sessions_Are_Interactive_The_Console_Wins()
        {
            var console = Register(session: 1);
            Register(session: 2);
            changes.Clear();

            roles.SessionChanged(ServiceSessionChange.RemoteConnect, 2);

            Assert.AreEqual(AgentRole.Active, roles.RoleOf(console));
            Assert.AreEqual(0, changes.Count);
        }

        [TestMethod]
        public void It_Switches_Back_When_The_Former_Session_Becomes_Interactive_Again()
        {
            var a = Register(session: 1);
            var b = Register(session: 2);
            roles.SessionChanged(ServiceSessionChange.SessionLock, 1);
            roles.SessionChanged(ServiceSessionChange.SessionUnlock, 2);
            roles.Released(a);
            Assert.AreEqual(AgentRole.Active, roles.RoleOf(b));

            roles.SessionChanged(ServiceSessionChange.SessionLock, 2);
            roles.SessionChanged(ServiceSessionChange.SessionUnlock, 1);
            roles.Released(b);

            Assert.AreEqual(AgentRole.Active, roles.RoleOf(a));
        }

        [TestMethod]
        public void When_The_Active_Instance_Ends_A_Waiting_One_Takes_Over()
        {
            var a = Register(session: 1);
            var b = Register(session: 2);

            registry.Remove(a.ConnectionId);

            Assert.AreEqual(AgentRole.Active, roles.RoleOf(b));
            Assert.AreEqual(b.ProcessId, changes.Last().ProcessId);
        }

        [TestMethod]
        public void After_A_Restart_Of_The_Service_The_Instance_That_Was_Active_Stays_Active()
        {
            var waiting = Register(session: 2);
            changes.Clear();

            // The service restarted; the instance of session 2 registered first and became active. Now the console
            // instance comes back saying it was active - the console wins anyway, through an ordered handover.
            var formerlyActive = Register(session: 1, claimsActive: true);

            Assert.AreEqual(AgentRole.Waiting, changes.Single().Role);
            Assert.AreEqual(waiting.ProcessId, changes.Single().ProcessId);
            roles.Released(waiting);
            Assert.AreEqual(AgentRole.Active, roles.RoleOf(formerlyActive));
        }

        [TestMethod]
        public void A_Session_Becoming_Interactive_Without_An_Instance_Gets_One_After_The_Grace_Period()
        {
            Register(session: 1);
            roles.SessionChanged(ServiceSessionChange.SessionLogon, 3);

            roles.Tick();
            Assert.AreEqual(0, launcher.Launched.Count, "the autostart gets the first move");

            clock.Advance(TimeSpan.FromSeconds(61));
            roles.Tick();
            CollectionAssert.AreEqual(new[] { 3 }, launcher.Launched);
        }

        [TestMethod]
        public void No_Instance_Is_Started_While_An_Update_Keeps_The_Application_Down()
        {
            held = true;
            roles.SessionChanged(ServiceSessionChange.SessionUnlock, 2);

            clock.Advance(TimeSpan.FromSeconds(16));
            roles.Tick();

            Assert.AreEqual(0, launcher.Launched.Count);
        }

        [TestMethod]
        public void An_Instance_That_Starts_In_Time_Is_Not_Started_Twice()
        {
            roles.SessionChanged(ServiceSessionChange.SessionLogon, 2);
            Register(session: 2);

            clock.Advance(TimeSpan.FromSeconds(61));
            roles.Tick();

            Assert.AreEqual(0, launcher.Launched.Count);
        }

        private RegisteredAgent Register(int session, bool claimsActive = false)
        {
            return registry.Register(Identity(session, 1000 + session), "Shop.Agent", "1.0", claimsActive, out _, out _)!;
        }

        private static ClaimsIdentity Identity(int session, int process)
        {
            var identity = new ClaimsIdentity(PipeClaimTypes.AuthenticationType, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.Name, $@"SHOP\user{session}"));
            identity.AddClaim(new Claim(PipeClaimTypes.ConnectionId, Guid.NewGuid().ToString()));
            identity.AddClaim(new Claim(PipeClaimTypes.SessionId, session.ToString()));
            identity.AddClaim(new Claim(PipeClaimTypes.ProcessId, process.ToString()));
            return identity;
        }

        private sealed class Clock : TimeProvider
        {
            private DateTimeOffset now = new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => now;

            public void Advance(TimeSpan by) => now += by;
        }

        /// <summary>
        /// As with fast user switching: only the console session is connected, the others are disconnected.
        /// </summary>
        private sealed class Sessions : ISessionInfo
        {
            public int Console { get; set; }

            public int ConsoleSessionId() => Console;

            public bool IsConnected(int sessionId) => sessionId == Console;
        }

        private sealed class Launcher : IApplicationControl
        {
            public List<int> Launched { get; } = new List<int>();

            public Version InstalledVersion() => new Version(1, 0);

            public void Launch(int sessionId) => Launched.Add(sessionId);

            public void Terminate(RegisteredAgent agent)
            {
            }
        }
    }
}
