using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.GenericService.Sessions;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Agents;

namespace ITVComponents.UpdateService.Sessions
{
    /// <summary>
    /// Decides which instance of the application is active when several sessions run it - only the service sees all
    /// sessions - and hands over between them.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>One instance is active, any number wait.</item>
    /// <item>Locking alone is no reason: a locked till without another session stays connected.</item>
    /// <item>When another session becomes interactive (logon, unlock, console or remote connected), its instance takes
    /// over. If several sessions are interactive, the console wins.</item>
    /// <item>A handover is ordered: the active instance releases first (hub connection, devices) and confirms; only then
    /// the next one takes over - or when the release takes longer than <see cref="RoleOptions.ReleaseTimeout"/>. The
    /// backend admits only one active instance per client app.</item>
    /// <item>When a session becomes interactive without an instance, the service starts one there - after a grace period
    /// that leaves the autostart (<c>HKLM\…\Run</c>) the first move at a logon.</item>
    /// </list>
    /// </remarks>
    public sealed class RoleCoordinator
    {
        private readonly AgentRegistry registry;
        private readonly ISessionInfo sessions;
        private readonly IApplicationControl application;
        private readonly Func<bool> applicationHeld;
        private readonly RoleOptions options;
        private readonly TimeProvider clock;
        private readonly object sync = new object();
        private readonly Dictionary<int, SessionState> states = new Dictionary<int, SessionState>();
        private readonly Dictionary<int, DateTimeOffset> launches = new Dictionary<int, DateTimeOffset>();
        private string? active;
        private Handover? handover;

        /// <param name="registry">the registered instances</param>
        /// <param name="sessions">the sessions of the machine</param>
        /// <param name="application">starts an instance in a session</param>
        /// <param name="applicationHeld">true while the update service keeps the application down (an update is under way);
        /// no instance is started then</param>
        /// <param name="options">the timings</param>
        /// <param name="clock">the time</param>
        public RoleCoordinator(AgentRegistry registry, ISessionInfo sessions, IApplicationControl application, Func<bool> applicationHeld, RoleOptions? options = null, TimeProvider? clock = null)
        {
            this.registry = registry;
            this.sessions = sessions;
            this.application = application;
            this.applicationHeld = applicationHeld;
            this.options = options ?? new RoleOptions();
            this.clock = clock ?? TimeProvider.System;
            registry.AgentRegistered += (_, agent) => AgentRegistered(agent);
            registry.AgentLeft += (_, agent) => AgentLeft(agent);
        }

        /// <summary>
        /// Is raised for every instance that is to take over or to release.
        /// </summary>
        public event Action<RoleChange>? RoleChanged;

        /// <summary>
        /// Gets the role of an instance.
        /// </summary>
        public AgentRole RoleOf(RegisteredAgent agent)
        {
            lock (sync)
            {
                return active == agent.ConnectionId && handover == null ? AgentRole.Active : AgentRole.Waiting;
            }
        }

        /// <summary>
        /// Gets the active instance, or null.
        /// </summary>
        public RegisteredAgent? Active
        {
            get
            {
                lock (sync)
                {
                    return registry.Active.FirstOrDefault(a => a.ConnectionId == active);
                }
            }
        }

        /// <summary>
        /// Takes note of a session change reported by Windows.
        /// </summary>
        public void SessionChanged(ServiceSessionChange change, int sessionId)
        {
            var changes = new List<RoleChange>();
            lock (sync)
            {
                var now = clock.GetUtcNow();
                var state = StateOf(sessionId);
                switch (change)
                {
                    case ServiceSessionChange.SessionLogon:
                        state.Connected = true;
                        state.Locked = false;
                        state.LastInteractive = now;
                        launches[sessionId] = now + options.LogonLaunchGrace;
                        break;
                    case ServiceSessionChange.SessionUnlock:
                        // Only a connected session can be unlocked.
                        state.Connected = true;
                        state.Locked = false;
                        state.LastInteractive = now;
                        launches[sessionId] = now + options.ResumeLaunchGrace;
                        break;
                    case ServiceSessionChange.ConsoleConnect:
                    case ServiceSessionChange.RemoteConnect:
                        state.Connected = true;
                        state.LastInteractive = now;
                        launches[sessionId] = now + options.ResumeLaunchGrace;
                        break;
                    case ServiceSessionChange.SessionLock:
                        state.Locked = true;
                        break;
                    case ServiceSessionChange.ConsoleDisconnect:
                    case ServiceSessionChange.RemoteDisconnect:
                        state.Connected = false;
                        break;
                    case ServiceSessionChange.SessionLogoff:
                    case ServiceSessionChange.SessionTerminate:
                        states.Remove(sessionId);
                        launches.Remove(sessionId);
                        break;
                }

                LogEnvironment.LogDebugEvent($"Session {sessionId}: {change}.", LogSeverity.Report);
                Recompute(changes);
            }

            Raise(changes);
        }

        /// <summary>
        /// The active instance has released after being asked to.
        /// </summary>
        public void Released(RegisteredAgent agent)
        {
            var changes = new List<RoleChange>();
            lock (sync)
            {
                if (handover == null || handover.From != agent.ConnectionId)
                {
                    LogEnvironment.LogDebugEvent($"{agent} reports released, but no handover waits for it.", LogSeverity.Report);
                    return;
                }

                LogEnvironment.LogEvent($"{agent} has released.", LogSeverity.Report);
                CompleteHandover(changes);
            }

            Raise(changes);
        }

        /// <summary>
        /// Handles what is due: a release that takes too long, an instance to start. Called every second.
        /// </summary>
        public void Tick()
        {
            var changes = new List<RoleChange>();
            var toLaunch = new List<int>();
            lock (sync)
            {
                var now = clock.GetUtcNow();
                if (handover != null && now >= handover.Deadline)
                {
                    LogEnvironment.LogEvent($"The active instance did not confirm its release within {options.ReleaseTimeout}; handing over anyway.", LogSeverity.Warning);
                    CompleteHandover(changes);
                }

                foreach (var due in launches.Where(l => l.Value <= now).Select(l => l.Key).ToArray())
                {
                    launches.Remove(due);
                    var running = registry.Active.Any(a => a.SessionId == due);
                    if (!running && states.TryGetValue(due, out var state) && state.Interactive)
                    {
                        toLaunch.Add(due);
                    }
                }
            }

            Raise(changes);
            foreach (var sessionId in toLaunch)
            {
                if (applicationHeld())
                {
                    LogEnvironment.LogDebugEvent($"Session {sessionId} has no instance, but an update keeps the application down; it is started after the update.", LogSeverity.Report);
                    continue;
                }

                try
                {
                    LogEnvironment.LogEvent($"Session {sessionId} became interactive without an instance of the application; starting one.", LogSeverity.Report);
                    application.Launch(sessionId);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"The application could not be started in session {sessionId}: {ex.OutlineException()}", LogSeverity.Error);
                }
            }
        }

        private void AgentRegistered(RegisteredAgent agent)
        {
            var changes = new List<RoleChange>();
            lock (sync)
            {
                var state = StateOf(agent.SessionId);
                if (state.LastInteractive == null)
                {
                    state.LastInteractive = clock.GetUtcNow();
                }

                launches.Remove(agent.SessionId);

                // After a restart of the service nobody is known as active: the instance that says it is keeps it, so that
                // no second instance takes over while the first still holds the devices.
                if (active == null && handover == null && agent.ClaimedActive)
                {
                    active = agent.ConnectionId;
                    LogEnvironment.LogEvent($"{agent} stays active (it was active before).", LogSeverity.Report);
                }

                Recompute(changes);
            }

            Raise(changes);
        }

        private void AgentLeft(RegisteredAgent agent)
        {
            var changes = new List<RoleChange>();
            lock (sync)
            {
                if (handover != null && handover.From == agent.ConnectionId)
                {
                    CompleteHandover(changes);
                }
                else if (active == agent.ConnectionId)
                {
                    active = null;
                    LogEnvironment.LogEvent($"The active instance {agent} is gone.", LogSeverity.Report);
                }

                Recompute(changes);
            }

            Raise(changes);
        }

        private void Recompute(List<RoleChange> changes)
        {
            var agents = registry.Active;
            var desired = Choose(agents);
            if (desired == null)
            {
                return;
            }

            if (handover != null)
            {
                // A handover is under way: only its target may change.
                handover.To = desired.ConnectionId;
                return;
            }

            if (active == desired.ConnectionId)
            {
                return;
            }

            var current = agents.FirstOrDefault(a => a.ConnectionId == active);
            if (current != null)
            {
                handover = new Handover(current.ConnectionId, desired.ConnectionId, clock.GetUtcNow() + options.ReleaseTimeout);
                LogEnvironment.LogEvent($"Session {desired.SessionId} takes over from session {current.SessionId}; {current} is asked to release.", LogSeverity.Report);
                changes.Add(new RoleChange { ProcessId = current.ProcessId, SessionId = current.SessionId, Role = AgentRole.Waiting });
                return;
            }

            active = desired.ConnectionId;
            LogEnvironment.LogEvent($"{desired} is active.", LogSeverity.Report);
            changes.Add(new RoleChange { ProcessId = desired.ProcessId, SessionId = desired.SessionId, Role = AgentRole.Active });
        }

        private void CompleteHandover(List<RoleChange> changes)
        {
            var target = registry.Active.FirstOrDefault(a => a.ConnectionId == handover!.To);
            handover = null;
            active = null;
            if (target != null)
            {
                active = target.ConnectionId;
                LogEnvironment.LogEvent($"{target} takes over.", LogSeverity.Report);
                changes.Add(new RoleChange { ProcessId = target.ProcessId, SessionId = target.SessionId, Role = AgentRole.Active });
                return;
            }

            Recompute(changes);
        }

        private RegisteredAgent? Choose(IReadOnlyList<RegisteredAgent> agents)
        {
            if (agents.Count == 0)
            {
                return null;
            }

            var interactive = agents.Where(a => StateOf(a.SessionId).Interactive).ToList();
            if (interactive.Count > 0)
            {
                var console = sessions.ConsoleSessionId();
                return interactive.FirstOrDefault(a => a.SessionId == console)
                       ?? interactive.OrderByDescending(a => StateOf(a.SessionId).LastInteractive).ThenBy(a => a.RegisteredUtc).First();
            }

            // No session is interactive (all locked or disconnected): nobody has a reason to take over.
            return agents.FirstOrDefault(a => a.ConnectionId == active)
                   ?? agents.OrderByDescending(a => StateOf(a.SessionId).LastInteractive).ThenBy(a => a.RegisteredUtc).First();
        }

        private SessionState StateOf(int sessionId)
        {
            if (!states.TryGetValue(sessionId, out var state))
            {
                state = new SessionState { Connected = sessions.IsConnected(sessionId) };
                states[sessionId] = state;
            }

            return state;
        }

        private void Raise(List<RoleChange> changes)
        {
            foreach (var change in changes)
            {
                var handlers = RoleChanged;
                if (handlers == null)
                {
                    continue;
                }

                foreach (Action<RoleChange> handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler(change);
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent($"A listener on the role change of process {change.ProcessId} failed: {ex.OutlineException()}", LogSeverity.Error);
                    }
                }
            }
        }

        private sealed class SessionState
        {
            public bool Connected { get; set; }

            public bool Locked { get; set; }

            public DateTimeOffset? LastInteractive { get; set; }

            public bool Interactive => Connected && !Locked;
        }

        private sealed class Handover
        {
            public Handover(string from, string to, DateTimeOffset deadline)
            {
                From = from;
                To = to;
                Deadline = deadline;
            }

            public string From { get; }

            public string To { get; set; }

            public DateTimeOffset Deadline { get; }
        }
    }

    /// <summary>
    /// The timings of a handover.
    /// </summary>
    public sealed class RoleOptions
    {
        /// <summary>Gets or sets how long the active instance has to release. Default: 15 seconds.</summary>
        public TimeSpan ReleaseTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>Gets or sets how long the autostart gets after a logon before the service starts an instance. Default: 60 seconds.</summary>
        public TimeSpan LogonLaunchGrace { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>Gets or sets how long a waiting instance gets after an unlock or connect before the service starts one. Default: 15 seconds.</summary>
        public TimeSpan ResumeLaunchGrace { get; set; } = TimeSpan.FromSeconds(15);
    }
}
