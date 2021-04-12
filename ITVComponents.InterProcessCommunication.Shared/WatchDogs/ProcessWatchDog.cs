using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.Logging;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.Shared.WatchDogs
{
    public class ProcessWatchDog:IPlugin, IIpcWatchDog, IWatchDogService
    {
        /// <summary>
        /// Holds all processes that are managed by this ProcessWatchDog
        /// </summary>
        private ConcurrentDictionary<string, ConcurrentDictionary<int, ProcessStatus>> processStates = new ConcurrentDictionary<string, ConcurrentDictionary<int, ProcessStatus>>();

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Sets the Status of a specific process on a machine
        /// </summary>
        /// <param name="machineName">the machine on which the process is being executed</param>
        /// <param name="processName">the name of the process</param>
        /// <param name="processId">the id of the process</param>
        /// <param name="rebootRequired">indicates whether this process requires a reboot</param>
        /// <returns>indicates whether the healt-status was accepted by the watch-dog</returns>
        public bool SetProcessStatus(string machineName, string processName, int processId, bool rebootRequired)
        {
            return SetProcessStatus(machineName, processName, processId, rebootRequired, out _);
        }

        /// <summary>
        /// Manages all processes that have registered on this watchDog instance
        /// </summary>
        public void ManageProcesses()
        {
            var machines = processStates.ToArray();
            foreach (var m in machines)
            {
                var procs = m.Value.ToArray();
                foreach (var proc in procs)
                {
                    bool processAlive;
                    if ((processAlive = ProcessAlive(m.Key, proc.Key, proc.Value.ProcessName, out var procInst)) && proc.Value.RebootRequired)
                    {
                        LogEnvironment.LogEvent($"Process {proc.Key}({proc.Value.ProcessName}) on {m.Key} requires a restart...", LogSeverity.Warning);
                        StopProcess(procInst, m.Value);
                    }
                    else if (!processAlive)
                    {
                        m.Value.TryRemove(proc.Key, out _);
                        ProcessKilled(proc.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Sets the Status of a specific process on a machine
        /// </summary>
        /// <param name="machineName">the machine on which the process is being executed</param>
        /// <param name="processName">the name of the process</param>
        /// <param name="processId">the id of the process</param>
        /// <param name="rebootRequired">indicates whether this process requires a reboot</param>
        /// <param name="status">the statusObject describing the status of the given process</param>
        /// <returns>indicates whether the healt-status was accepted by the watch-dog</returns>
        protected bool SetProcessStatus(string machineName, string processName, int processId, bool rebootRequired, out ProcessStatus status)
        {
            var procs = processStates.GetOrAdd(machineName, s => new ConcurrentDictionary<int, ProcessStatus>());
            status = procs.GetOrAdd(processId, i => new ProcessStatus
            {
                ProcessName = processName
            });

            lock (status)
            {
                return status.RebootRequired = (status.RebootRequired || rebootRequired);

            }
        }

        /// <summary>
        /// Gets a value indicating whether the given process exists
        /// </summary>
        /// <param name="machineName">the name of the machine executing the requested process</param>
        /// <param name="processId">the id of the process</param>
        /// <param name="processName">the name of the requested process</param>
        /// <param name="process">the instance of the existing process</param>
        /// <returns>a value indicating whether the given process exists and is alive</returns>
        protected virtual bool ProcessAlive(string machineName, int processId, string processName, out Process process)
        {
            bool retVal = true;
            process = null;
            try
            {
                var proc = Process.GetProcessById(processId, machineName);
                retVal = !proc.HasExited;
                if (retVal)
                    process = proc;
            }
            catch (ArgumentException ex)
            {
                OnError("CheckProcessAlive", processId, processName, machineName, true, ex);
                retVal = false;
            }
            catch (Exception ex)
            {
                OnError("CheckProcessAlive", processId, processName, machineName, false, ex);
                retVal = false;
            }

            return retVal;
        }

        /// <summary>
        /// Stops a process and removes it from the list of known processes
        /// </summary>
        /// <param name="process">the id of the process to stop</param>
        /// <param name="statusValues">a list of known processes</param>
        protected virtual void StopProcess(Process process, ConcurrentDictionary<int, ProcessStatus> statusValues)
        {

            int id = process.Id;
            string name = process.ProcessName;
            string machine = process.MachineName;
            try
            {
                process.Kill();
                statusValues.TryRemove(id, out var proc);
                ProcessKilled(proc);
            }
            catch (Exception ex)
            {
                OnError("EndProcess", id, name, machine, false, ex);
            }
        }

        /// <summary>
        /// Enables a derived class to take further actions after a process was killed
        /// </summary>
        /// <param name="status">the process-status containing information about the killed process</param>
        protected virtual void ProcessKilled(ProcessStatus status)
        {
        }

        /// <summary>
        ///   Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Is called when an error occurrs
        /// </summary>
        /// <param name="action">the action that was called</param>
        /// <param name="processId">the id of the process</param>
        /// <param name="name">the name of the process</param>
        /// <param name="machine">the machine on which the process is executed</param>
        /// <param name="expected">Indicates whether the occurred error was expected</param>
        /// <param name="ex">the exception that was catched</param>
        protected virtual void OnError(string action, int processId, string name, string machine, bool expected, Exception ex)
        {
            if (!expected)
            {
                LogEnvironment.LogEvent(
                    $"Task {action} failed for {processId}({name}) on {machine}. Error: {ex.OutlineException()}", LogSeverity.Error);
            }
            else
            {
                LogEnvironment.LogDebugEvent(
                    $"Task {action} catched an expected error for {processId}({name}) on {machine}. Error: {ex.OutlineException()}", LogSeverity.Report);
            }
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
