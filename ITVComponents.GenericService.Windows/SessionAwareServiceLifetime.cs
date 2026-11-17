using System.Runtime.Versioning;
using System.ServiceProcess;
using ITVComponents.GenericService.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.GenericService.Windows
{
    /// <summary>
    /// The Windows service lifetime, plus the session notifications of the service control manager
    /// (<c>SERVICE_CONTROL_SESSIONCHANGE</c>), passed on to <see cref="ServiceSessionEvents"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class SessionAwareServiceLifetime : WindowsServiceLifetime
    {
        private readonly ServiceSessionEvents sessionEvents;

        public SessionAwareServiceLifetime(IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory,
            IOptions<HostOptions> optionsAccessor, IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor, ServiceSessionEvents sessionEvents)
            : base(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptionsAccessor)
        {
            this.sessionEvents = sessionEvents;

            // Must be set before the service is started: it decides whether the service control manager sends the
            // notifications at all.
            CanHandleSessionChangeEvent = true;
            sessionEvents.Activate();
        }

        /// <inheritdoc />
        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            base.OnSessionChange(changeDescription);
            sessionEvents.Raise((ServiceSessionChange)(int)changeDescription.Reason, changeDescription.SessionId);
        }
    }
}
