using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Logging
{
    internal class CollectingLogger:ILogger
    {
        private readonly ILogCollectorService collectorService;
        private readonly string category;

        public CollectingLogger(ILogCollectorService collectorService, string category)
        {
            this.collectorService = collectorService;
            this.category = category;
        }

        /// <summary>Begins a logical operation scope.</summary>
        /// <param name="state">The identifier for the scope.</param>
        /// <typeparam name="TState">The type of the state to begin scope for.</typeparam>
        /// <returns>An <see cref="T:System.IDisposable" /> that ends the logical operation scope on dispose.</returns>
        public IDisposable BeginScope<TState>(TState state)
        {
            return new ScopeLock<TState>(this, state);
        }

        /// <summary>
        /// Checks if the given <paramref name="logLevel" /> is enabled.
        /// </summary>
        /// <param name="logLevel">level to be checked.</param>
        /// <returns><c>true</c> if enabled.</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return collectorService.IsEnabled(logLevel, category);
        }

        /// <summary>Writes a log entry.</summary>
        /// <param name="logLevel">Entry will be written on this level.</param>
        /// <param name="eventId">Id of the event.</param>
        /// <param name="state">The entry to be written. Can be also an object.</param>
        /// <param name="exception">The exception related to this entry.</param>
        /// <param name="formatter">Function to create a <see cref="T:System.String" /> message of the <paramref name="state" /> and <paramref name="exception" />.</param>
        /// <typeparam name="TState">The type of the object to be written.</typeparam>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel)){
                var msg = $@"{state}
{exception?.OutlineException()}";
                var basic = $"{eventId.Id}: {eventId.Name}";
                collectorService.AddEvent(logLevel, category, basic, msg);
            }
        }

        private class ScopeLock<TState>:IDisposable
        {
            private CollectingLogger parent;
            private readonly TState state;

            public ScopeLock(CollectingLogger parent, TState state)
            {
                this.parent = parent;
                this.state = state;
                parent.Log(LogLevel.Debug, new EventId(0,$"Begin Scope for: {state}"),state,null,(state1, exception) => "");
            }

            public void Dispose()
            {
                parent.Log(LogLevel.Debug, new EventId(0,$"End Scope for: {state}"),state,null,(state1, exception) => "");
            }
        }
    }
}
