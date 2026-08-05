using System;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.AspExtensions.PageHandler
{
    internal sealed class PageHandlerLease<THandlerInterface> : IPageHandlerLease<THandlerInterface>
    {
        private readonly AsyncServiceScope scope;
        private bool disposed;

        public PageHandlerLease(AsyncServiceScope scope, THandlerInterface handler)
        {
            this.scope = scope;
            Handler = handler;
        }

        public THandlerInterface Handler { get; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DisposeHandler();
            scope.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DisposeHandler();
            await scope.DisposeAsync();
        }

        /// <summary>
        /// Der Handler entsteht ueber ActivatorUtilities.CreateInstance, und die meldet die erzeugte Instanz
        /// NICHT beim Scope zur Entsorgung an - der Scope raeumt nur weg, was er selbst aufgeloest hat. Ohne
        /// diesen Aufruf bliebe ein IDisposable-Handler pro Vorgang liegen.
        /// </summary>
        private void DisposeHandler()
        {
            if (Handler is not IDisposable disposable)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                // Der Scope muss trotzdem weg, sonst haengt an einem kaputten Handler die ganze Aufloesung.
                // Verschweigen darf man es aber nicht: ohne Meldung sucht man den Leak spaeter im Falschen.
                LogEnvironment.LogEvent(
                    $"Failed to dispose page-handler {Handler.GetType().FullName}: {ex.OutlineException()}",
                    LogSeverity.Error);
            }
        }
    }
}
