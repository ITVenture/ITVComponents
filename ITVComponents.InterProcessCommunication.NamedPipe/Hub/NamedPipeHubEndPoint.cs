using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.NamedPipe.Channels;
using ITVComponents.InterProcessCommunication.NamedPipe.Security;
using ITVComponents.Logging;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Hub
{
    /// <summary>
    /// Makes a local service hub reachable over a named pipe - for processes on the same machine only, with the
    /// access given by an ACL and the identity of each caller (user, session, process) taken from the operating
    /// system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counterpart of the in-memory <c>ServiceHubChannel</c>: the services live in the hub's process and
    /// register with the <see cref="IServiceHubProvider"/> directly; clients connect through the pipe
    /// (<see cref="Client.NamedPipeClient"/>). The objects are exchanged one layer up, by the message server and
    /// client, as with the other transports.
    /// </para>
    /// <para>
    /// The hub creates the first instance of the pipe exclusively. If someone else holds the name already, the hub
    /// does not start - it would otherwise share the name with a stranger who receives some of the connections.
    /// </para>
    /// <para>
    /// A service that relies on the identity must not trust what the client sends along with its request: set
    /// <c>TrustClientIdentity = false</c> on the message server (<see cref="Server.NamedPipeServer"/> does).
    /// </para>
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public class NamedPipeHubEndPoint : IPlugin, IDeferredInit
    {
        private readonly IServiceHubProvider hubProvider;
        private readonly ConcurrentDictionary<string, PipeHubConnection> connections = new ConcurrentDictionary<string, PipeHubConnection>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, string> backChannels = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        private readonly object initLock = new object();
        private CancellationTokenSource stopping;
        private PipeSecurity pipeSecurity;
        private long nextConnection;
        private bool disposed;

        /// <summary>
        /// Initializes a new open endpoint: every authenticated local account may connect and call every service.
        /// </summary>
        /// <param name="pipeName">the name of the pipe</param>
        /// <param name="hubProvider">the hub whose services become reachable</param>
        public NamedPipeHubEndPoint(string pipeName, IServiceHubProvider hubProvider)
            : this(new NamedPipeHubOptions { PipeName = pipeName }, hubProvider)
        {
        }

        /// <summary>
        /// Initializes a new endpoint.
        /// </summary>
        /// <param name="pipeName">the name of the pipe</param>
        /// <param name="hubProvider">the hub whose services become reachable</param>
        /// <param name="allowedClients">the accounts or groups the pipe lets in (SIDs or names, separated by semicolons); empty means every authenticated account</param>
        /// <param name="allowRemoteServices">whether pipe clients may register services of their own</param>
        /// <param name="authorize">whether to check every client against the users, roles and permissions in the service's configuration (<see cref="SecurityRepositoryPipeAuthorizer"/>)</param>
        public NamedPipeHubEndPoint(string pipeName, IServiceHubProvider hubProvider, string allowedClients, bool allowRemoteServices, bool authorize)
            : this(pipeName, hubProvider, allowedClients, allowRemoteServices, authorize ? new SecurityRepositoryPipeAuthorizer() : null)
        {
        }

        /// <summary>
        /// Initializes a new endpoint.
        /// </summary>
        /// <param name="pipeName">the name of the pipe</param>
        /// <param name="hubProvider">the hub whose services become reachable</param>
        /// <param name="allowedClients">the accounts or groups the pipe lets in (SIDs or names, separated by semicolons); empty means every authenticated account</param>
        /// <param name="allowRemoteServices">whether pipe clients may register services of their own</param>
        /// <param name="authorizer">decides who may do what; null means open</param>
        public NamedPipeHubEndPoint(string pipeName, IServiceHubProvider hubProvider, string allowedClients, bool allowRemoteServices, IPipeHubAuthorizer authorizer)
            : this(new NamedPipeHubOptions
            {
                PipeName = pipeName,
                AllowedClients = PipeAccessControl.ParseSids(allowedClients).ToList(),
                AllowRemoteServices = allowRemoteServices,
                Authorizer = authorizer
            }, hubProvider)
        {
        }

        /// <summary>
        /// Initializes a new endpoint.
        /// </summary>
        /// <param name="options">the settings</param>
        /// <param name="hubProvider">the hub whose services become reachable</param>
        public NamedPipeHubEndPoint(NamedPipeHubOptions options, IServiceHubProvider hubProvider)
        {
            if (string.IsNullOrWhiteSpace(options?.PipeName))
            {
                throw new ArgumentException("The endpoint needs the name of its pipe.", nameof(options));
            }

            Options = options;
            this.hubProvider = hubProvider ?? throw new ArgumentNullException(nameof(hubProvider));
        }

        /// <summary>
        /// Gets the settings of this endpoint.
        /// </summary>
        public NamedPipeHubOptions Options { get; }

        /// <inheritdoc />
        public string UniqueName { get; set; }

        /// <inheritdoc />
        public bool Initialized { get; private set; }

        /// <inheritdoc />
        public bool ForceImmediateInitialization => false;

        /// <summary>
        /// Gets the clients connected right now.
        /// </summary>
        public IReadOnlyList<PipeClientIdentity> ConnectedClients => connections.Values.Select(c => c.Client).ToArray();

        /// <summary>
        /// Is raised when a client has connected and was identified.
        /// </summary>
        public event EventHandler<PipeClientEventArgs> ClientConnected;

        /// <summary>
        /// Is raised when a client has disconnected; what it had registered is dropped by then.
        /// </summary>
        public event EventHandler<PipeClientEventArgs> ClientDisconnected;

        /// <inheritdoc />
        public event EventHandler Disposed;

        /// <summary>
        /// Opens the pipe and starts accepting clients.
        /// </summary>
        /// <exception cref="IOException">when the pipe exists already - someone else holds the name</exception>
        public void Initialize()
        {
            lock (initLock)
            {
                if (Initialized)
                {
                    return;
                }

                pipeSecurity = PipeAccessControl.Build(Options.AllowedClients);
                NamedPipeServerStream first;
                try
                {
                    first = CreateInstance(true);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"The hub pipe {Options.PipeName} could not be opened - most likely another process holds the name. The hub does not start: {ex.OutlineException()}", LogSeverity.Error);
                    throw;
                }

                stopping = new CancellationTokenSource();
                Initialized = true;
                var token = stopping.Token;
                Task.Run(() => AcceptLoop(first, token));
                LogEnvironment.LogEvent($"The hub listens on pipe {Options.PipeName}.", LogSeverity.Report);
            }
        }

        /// <summary>
        /// Stops accepting clients and closes every connection.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            stopping?.Cancel();
            foreach (var connection in connections.Values)
            {
                connection.Close();
            }

            stopping?.Dispose();
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Indicates whether a service is the back channel a pipe client registered for its events, and for which
        /// service it answers.
        /// </summary>
        internal bool IsBackChannelOfAPipeClient(string serviceName, out string answersFor)
        {
            answersFor = null;
            return !string.IsNullOrEmpty(serviceName) && backChannels.TryGetValue(serviceName, out answersFor);
        }

        internal void AddBackChannel(string serviceName, string answersFor)
        {
            backChannels[serviceName] = answersFor;
        }

        internal void RemoveBackChannel(string serviceName)
        {
            backChannels.TryRemove(serviceName, out _);
        }

        private NamedPipeServerStream CreateInstance(bool first)
        {
            var pipeOptions = PipeOptions.Asynchronous | (first ? PipeOptions.FirstPipeInstance : PipeOptions.None);
            return NamedPipeServerStreamAcl.Create(Options.PipeName, PipeDirection.InOut, Options.MaxConnections, PipeTransmissionMode.Byte, pipeOptions, 0, 0, pipeSecurity);
        }

        private async Task AcceptLoop(NamedPipeServerStream first, CancellationToken token)
        {
            var listening = first;
            var busyReported = false;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    listening ??= CreateInstance(false);
                    busyReported = false;
                    await listening.WaitForConnectionAsync(token).ConfigureAwait(false);
                    var connected = listening;
                    listening = null;
                    _ = Task.Run(() => Accept(connected));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex) when (listening == null)
                {
                    // All instances are taken: wait for a connection to end.
                    if (!busyReported)
                    {
                        LogEnvironment.LogEvent($"The hub pipe {Options.PipeName} has no free instance ({Options.MaxConnections} connections); new clients wait: {ex.Message}", LogSeverity.Warning);
                        busyReported = true;
                    }

                    await DelayUnlessStopping(token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Accepting a client on hub pipe {Options.PipeName} failed: {ex.OutlineException()}", LogSeverity.Error);
                    DisposeQuietly(listening);
                    listening = null;
                    await DelayUnlessStopping(token).ConfigureAwait(false);
                }
            }

            DisposeQuietly(listening);
            LogEnvironment.LogDebugEvent($"The hub stopped listening on pipe {Options.PipeName}.", LogSeverity.Report);
        }

        private static async Task DelayUnlessStopping(CancellationToken token)
        {
            try
            {
                await Task.Delay(1000, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                LogEnvironment.LogDebugEvent("Waiting for a free pipe instance ended because the hub stops.", LogSeverity.Report);
            }
        }

        private void Accept(NamedPipeServerStream pipe)
        {
            var connectionId = Interlocked.Increment(ref nextConnection).ToString(System.Globalization.CultureInfo.InvariantCulture);
            PipeClientIdentity client;
            try
            {
                client = PipeClientIdentity.Read(pipe, connectionId);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"A client of hub pipe {Options.PipeName} could not be identified and is refused: {ex.OutlineException()}", LogSeverity.Warning);
                DisposeQuietly(pipe);
                return;
            }

            if (Options.Authorizer != null)
            {
                bool known;
                try
                {
                    known = Options.Authorizer.IsAuthenticated(client.ToClaimsIdentity());
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Pipe client {client} could not be checked against the hub's authorization and is refused: {ex.OutlineException()}", LogSeverity.Error);
                    known = false;
                }

                if (!known)
                {
                    LogEnvironment.LogEvent($"Pipe client {client} is not known to the hub's authorization and is refused.", LogSeverity.Warning);
                    DisposeQuietly(pipe);
                    return;
                }
            }

            var channel = new PipeMessageChannel(pipe, Options.MaxMessageSize, $"{Options.PipeName}#{connectionId}");
            var connection = new PipeHubConnection(channel, client, this, hubProvider);
            connection.Ended += ConnectionEnded;
            connections[connectionId] = connection;
            LogEnvironment.LogDebugEvent($"Pipe client {client} connected to {Options.PipeName}.", LogSeverity.Report);
            Raise(ClientConnected, client);
            connection.Start();
            if (disposed)
            {
                connection.Close();
            }
        }

        private void ConnectionEnded(object sender, EventArgs e)
        {
            var connection = (PipeHubConnection)sender;
            connection.Ended -= ConnectionEnded;
            connections.TryRemove(connection.Client.ConnectionId, out _);
            LogEnvironment.LogDebugEvent($"Pipe client {connection.Client} disconnected from {Options.PipeName}.", LogSeverity.Report);
            Raise(ClientDisconnected, connection.Client);
        }

        private void Raise(EventHandler<PipeClientEventArgs> handler, PipeClientIdentity client)
        {
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(this, new PipeClientEventArgs(client));
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"A listener on pipe client {client} failed: {ex.OutlineException()}", LogSeverity.Error);
            }
        }

        private static void DisposeQuietly(IDisposable disposable)
        {
            if (disposable == null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Closing a pipe instance failed: {ex.OutlineException()}", LogSeverity.Warning);
            }
        }
    }
}
