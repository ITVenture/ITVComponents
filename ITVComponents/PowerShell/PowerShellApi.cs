using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Runtime.CompilerServices;
using ITVComponents.Logging;
using PS = System.Management.Automation.PowerShell;
namespace ITVComponents.PowerShell
{
    /// <summary>
    /// Provides a PowerShell - Wrapper that is capable for running powershell scripts or single commands
    /// </summary>
    public class PowerShellApi:IDisposable
    {
        /// <summary>
        /// the connected runspace
        /// </summary>
        private  Runspace runspace = null;

        /// <summary>
        /// Initializes a new instance of the PowerShellApi class with default settings
        /// </summary>
        public PowerShellApi()
        {
            runspace = RunspaceFactory.CreateRunspace();
            runspace.Open();
        }

        /// <summary>
        /// Initializes a new instance of the PowerShellApi with a custom runspace
        /// </summary>
        /// <param name="targetRunspace"></param>
        public PowerShellApi(Runspace targetRunspace)
        {
            runspace = targetRunspace;
            targetRunspace.Open();
        }
        /// <summary>
        /// Runs a Powershell command using an array. the Command is executed once for every entry in the Arguments-Array
        /// </summary>
        /// <typeparam name="T">the exepected result for each run</typeparam>
        /// <param name="command">the command to run</param>
        /// <param name="arguments">an array of parameters that must be passed to the command on each run</param>
        /// <returns>an array containing a result for each run of the command</returns>
        public T[] RunCommandFor<T>(string command, Dictionary<string,object>[] arguments)
        {
            using (var pip = runspace.CreatePipeline())
            {
                foreach (var arg in arguments)
                {
                    var cmd = new Command(command);
                    var parameters = (from t in arg select new CommandParameter(t.Key, t.Value)).ToArray();
                    foreach (var commandParameter in parameters)
                    {
                        cmd.Parameters.Add(commandParameter);
                    }

                    pip.Commands.Add(cmd);
                }

                var results = pip.Invoke();
                if (GetPipeErrors<T>(pip, results, out var x))
                {
                    throw x;
                }

                return TryGetList<T>(results);
            }
        }

        /// <summary>
        /// Runs a Powershell command with the given arguments
        /// </summary>
        /// <typeparam name="T">the exepected result-type of the command</typeparam>
        /// <param name="command">the command to run</param>
        /// <param name="arguments">the run-arguments</param>
        /// <returns>the result converted to the given type</returns>
        public T RunCommand<T>(string command, Dictionary<string, object> arguments)
        {
            using (var pip = runspace.CreatePipeline())
            {
                var cmd = new Command(command);
                var parameters = (from t in arguments select new CommandParameter(t.Key, t.Value)).ToArray();
                foreach (var commandParameter in parameters)
                {
                    cmd.Parameters.Add(commandParameter);
                }

                pip.Commands.Add(cmd);
                var results = pip.Invoke();
                LogEnvironment.LogDebugEvent(null, $"Received {results.Count} results.", (int) LogSeverity.Report, null);
                LogEnvironment.LogDebugEvent(null, $"Output from Powershell-Command: {pip.Output.ReadToEnd()}", (int) LogSeverity.Report, null);
                if (GetPipeErrors<T>(pip, results, out var x))
                {
                    throw x;
                }

                return TryGetList<T>(results).FirstOrDefault();
            }
        }

        /// <summary>
        /// Runs a Powershell command with the given arguments
        /// </summary>
        /// <typeparam name="T">the exepected result-type of the command</typeparam>
        /// <param name="command">the command to run</param>
        /// <param name="arguments">the run-arguments</param>
        /// <returns>the result converted to the given type</returns>
        public IList<T> GetList<T>(string command, Dictionary<string, object> arguments)
        {
            using (var pip = runspace.CreatePipeline())
            {
                var cmd = new Command(command);
                var parameters = (from t in arguments select new CommandParameter(t.Key, t.Value)).ToArray();
                foreach (var commandParameter in parameters)
                {
                    cmd.Parameters.Add(commandParameter);
                }

                pip.Commands.Add(cmd);
                var results = pip.Invoke();
                LogEnvironment.LogDebugEvent(null,$"Received {results.Count} results.",(int)LogSeverity.Report,null);
                LogEnvironment.LogDebugEvent(null, $"Output from Powershell-Command: {pip.Output.ReadToEnd()}", (int)LogSeverity.Report,null);
                if (GetPipeErrors<T>(pip, results, out var x))
                {
                    throw x;
                }

                return TryGetList<T>(results);
            }
        }

        /// <summary>
        /// Runs a single command without returning the result
        /// </summary>
        /// <param name="command">the command to execute</param>
        /// <param name="parameters">the parameters for the command</param>
        public void RunCommand(string command, Dictionary<string,object> parameters)
        {
            using (var pip = runspace.CreatePipeline())
            {
                var cmd = new Command(command);
                var arguments = (from t in parameters select new CommandParameter(t.Key, t.Value)).ToArray();
                foreach (var commandParameter in arguments)
                {
                    cmd.Parameters.Add(commandParameter);
                }

                pip.Commands.Add(cmd);
                var results = pip.Invoke();
                //var errors = pip.Error.Read();
                if (GetPipeErrors<object>(pip, results, out var x))
                {
                    throw x;
                }

                LogEnvironment.LogDebugEvent(null, $"PS:{command}->{results}", (int) LogSeverity.Report, null);
            }
        }

        private bool GetPipeErrors<TResult>(Pipeline pipeline, ICollection<PSObject> results,out PowerShellApiException<TResult> ex)
        {
            bool retVal = pipeline.Error.Count != 0;
            ex = null;
            if (retVal)
            {
                var errors = TryGetList<ErrorRecord>(GetErrors(pipeline));
                var resultList = TryGetList<TResult>(results);
                string message = "The execution of a command or script has thrown some errors";
                Exception inner = null;
                if (errors.Length == 1)
                {
                    var err = errors.First();
                    if (err.Exception != null)
                    {
                        inner = err.Exception;
                        message = inner.Message;
                    }
                    else if (!string.IsNullOrEmpty(err.ErrorDetails?.Message))
                    {
                        message = err.ErrorDetails.Message;
                    }
                }

                if (inner != null)
                {
                    ex = new PowerShellApiException<TResult>(message, inner, errors, resultList);
                }
                else
                {
                    ex = new PowerShellApiException<TResult>(message, errors, resultList);
                }
            }

            return retVal;
        }

        private ICollection<PSObject> GetErrors(Pipeline pipeline)
        {
            var li = new List<PSObject>();
            while (pipeline.Error.Count != 0)
            {
                var rec = pipeline.Error.Read();
                li.Add(rec as PSObject);
            }

            return li;
        }

        private T[] TryGetList<T>(ICollection<PSObject> rawObjects)
        {
            try
            {
                return (from t in rawObjects select t.BaseObject).Cast<T>().ToArray();
            }
            catch (InvalidCastException ex)
            {
                LogEnvironment.LogEvent($"Failed to cast result to demanded type: {ex}", LogSeverity.Error);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Unexpected error: {ex}", LogSeverity.Error);
            }

            return new T[0];
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            this.runspace.Close();
            this.runspace.Dispose();
            this.runspace = null;
        }
    }
}