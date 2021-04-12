using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.Plugins;

namespace ITVComponents.ParallelProcessing
{
    public abstract class WatchDog:ICriticalComponent, IPlugin
    {
        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Checks whether the given taskProcessor is alive and takes appropriate actions if the processor is corrupted
        /// </summary>
        /// <param name="processor"></param>
        public void WatchProcessor(TaskProcessor processor)
        {
            if (processor.State == TaskProcessor.ProcessorState.Running)
            {
                WatchProcessorInstance(processor);
            }
            else
            {
                LogEnvironment.LogDebugEvent($"Skipping Worker in {processor.State} state.", LogSeverity.Warning);
            }
        }



        /// <summary>
        ///   Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Checks whether the given taskProcessor is alive and takes appropriate actions if the processor is corrupted
        /// </summary>
        /// <param name="processor"></param>
        protected abstract void WatchProcessorInstance(TaskProcessor processor);

        /// <summary>
        /// Kills the given taskProcessor instance
        /// </summary>
        /// <param name="processor">the processor to kill</param>
        /// <param name="createNew">indicates whether to re-create the processor</param>
        protected void KillProcessor(TaskProcessor processor, bool createNew)
        {
            LogEnvironment.LogEvent($"Killing a Worker-Thread of ParallelTaskProcessor {processor.Parent.Identifier}. {(!createNew?"Consider restarting the application soon. This action will cause the ParallelTaskProcessor to run with reduced capacity.":"")}", LogSeverity.Warning);
            if (!(processor.Worker.CurrentTask?.ExecutingUnsafe ?? false))
            {
                processor.KillThread();
                processor.Worker.Quit();
                var parent = processor.Parent;
                parent.UnRegisterProcessor(processor);
                if (createNew)
                {
                    LogEnvironment.LogDebugEvent($"Creating a new Worker-Thread for ParallelTaskProcessor {processor.Parent.Identifier}", LogSeverity.Warning);
                    parent.CreateProcessor(processor.LowestPriority);
                }
            }
            else
            {
                LogEnvironment.LogDebugEvent($"The WorkerThread of ParallelTaskProcessor {processor.Parent.Identifier} is executing unsafe code. Processor can not be killed.", LogSeverity.Error);
                throw new InvalidOperationException($"The WorkerThread of ParallelTaskProcessor {processor.Parent.Identifier} is executing unsafe code. Processor can not be killed.");
            }
        }

        /// <summary>
        /// Kills the thread that is used by the processor and starts a new one
        /// </summary>
        /// <param name="processor">the processor on which to stop the worker-thread</param>
        /// <param name="resetWorker">indicates whether to re-set the used worker</param>
        protected void RestartProcessor(TaskProcessor processor, bool resetWorker)
        {
            LogEnvironment.LogEvent($"Restarting WorkerThread of ParallelTaskProcessor {processor.Parent.Identifier}", LogSeverity.Warning);
            if (!(processor.Worker.CurrentTask?.ExecutingUnsafe ?? false))
            {
                processor.KillThread();
                if (resetWorker)
                {
                    LogEnvironment.LogDebugEvent($"Resetting Worker-Instsance of ParallelTaskProcessor {processor.Parent.Identifier}", LogSeverity.Warning);
                    processor.Worker.Reset();
                }

                processor.StartupThread();
            }
            else
            {
                LogEnvironment.LogDebugEvent($"The WorkerThread of ParallelTaskProcessor {processor.Parent.Identifier} is executing unsafe code. Processor can not be restarted.", LogSeverity.Error);
                throw new InvalidOperationException($"The WorkerThread of ParallelTaskProcessor {processor.Parent.Identifier} is executing unsafe code. Processor can not be restarted.");
            }
        }

        /// <summary>
        /// If supported by the application, this method will cause an immediate exit
        /// </summary>
        protected void Critical()
        {
            LogEnvironment.LogEvent("Critical: Application is about to crash!", LogSeverity.Error);
            OnCriticalError(new CriticalErrorEventArgs(new ComponentException("A critical error has occurred", true)));
        }

        /// <summary>
        /// Raises the critical error event
        /// </summary>
        /// <param name="e">the arguments for this event</param>
        protected virtual void OnCriticalError(CriticalErrorEventArgs e)
        {
            CriticalError?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// When raised, a supporting component can take appropriate actions to shutdown an application
        /// </summary>
        public event CriticalErrorEventHandler CriticalError;

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
