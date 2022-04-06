using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Logging
{
    /// <summary>
    /// Decorator class for proper DependencyInjection when generic ILoggers are injected
    /// </summary>
    /// <typeparam name="TTopic">the log-topic that was requested</typeparam>
    internal class CollectingLoggerDecorator<TTopic>:ILogger<TTopic>
    {
        private ILogger[] decoratedLoggers;

        private readonly IGlobalLogConfiguration configuration;
        private string myTopic;

        /// <summary>
        /// Initializes a new instance of the CollectingLoggerDecorator class
        /// </summary>
        /// <param name="provider"></param>
        public CollectingLoggerDecorator(IEnumerable<ILoggerProvider> providers, IGlobalLogConfiguration configuration)
        {
            this.configuration = configuration;
            myTopic = typeof(TTopic).FullName;
            decoratedLoggers = providers.Select(n => n.CreateLogger(myTopic)).ToArray();
        }

        /// <summary>Begins a logical operation scope.</summary>
        /// <param name="state">The identifier for the scope.</param>
        /// <typeparam name="TState">The type of the state to begin scope for.</typeparam>
        /// <returns>An <see cref="T:System.IDisposable" /> that ends the logical operation scope on dispose.</returns>
        public IDisposable BeginScope<TState>(TState state)
        {
            return new ToolkitLogScope<TState>(decoratedLoggers.Select(n => n.BeginScope(state)));
        }

        /// <summary>
        /// Checks if the given <paramref name="logLevel" /> is enabled.
        /// </summary>
        /// <param name="logLevel">level to be checked.</param>
        /// <returns><c>true</c> if enabled.</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return configuration.IsEnabled(logLevel, myTopic);
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
            foreach (var decorated in decoratedLoggers)
            {
                if (decorated is not CollectingLogger)
                {
                    if (configuration.IsEnabled(logLevel, myTopic))
                    {
                        decorated.Log(logLevel, eventId, state, exception, formatter);
                    }
                }
                else
                {
                    decorated.Log(logLevel, eventId, state, exception, formatter);
                }
            }
        }
    }
}
