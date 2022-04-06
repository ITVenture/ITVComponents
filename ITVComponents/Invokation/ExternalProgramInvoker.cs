using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.Threading;

namespace ITVComponents.Invokation
{
    /// <summary>
    /// Helper class for external Program invokation
    /// </summary>
    public class ExternalProgramInvoker
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use console redirection for the executed tasks
        /// </summary>
        public bool RedirectConsole { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether ShellExecute is being used to launch the processes
        /// </summary>
        public bool UseShellExecute { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the process runs hidden
        /// </summary>
        public bool Hidden { get; set; } = true;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="applicationName">the path to the application image</param>
        /// <param name="arguments">the arguments that must be passed to the program</param>
        /// <param name="timeout">the timeout in which the program must terminate</param>
        /// <param name="maxRetryCount">the maximum number of retries</param>
        /// <returns>a program termination information object that contains information about the application run</returns>
        public ProgramTerminationInformation RunApplication(string applicationName, string arguments, int timeout,
                                                            int maxRetryCount)
        {
            ProcessStartInfo pif = new ProcessStartInfo
            {
                Arguments = arguments,
                CreateNoWindow = Hidden,
                FileName = applicationName,
                RedirectStandardError = RedirectConsole,
                RedirectStandardOutput = RedirectConsole,
                UseShellExecute = UseShellExecute
            };
            return Run(pif, timeout, maxRetryCount);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="applicationName">the path to the application image</param>
        /// <param name="executionDirectory">the working directory of the called process</param>
        /// <param name="arguments">the arguments that must be passed to the program</param>
        /// <param name="timeout">the timeout in which the program must terminate</param>
        /// <param name="maxRetryCount">the maximum number of retries</param>
        /// <returns>a program termination information object that contains information about the application run</returns>
        public ProgramTerminationInformation RunApplication(string applicationName, string executionDirectory, string arguments, int timeout,
                                                            int maxRetryCount)
        {
            ProcessStartInfo pif = new ProcessStartInfo
            {
                Arguments = arguments,
                CreateNoWindow = Hidden,
                FileName = applicationName,
                RedirectStandardError = RedirectConsole,
                RedirectStandardOutput = RedirectConsole,
                UseShellExecute = UseShellExecute,
                WorkingDirectory = executionDirectory
            };

            return Run(pif, timeout, maxRetryCount);
        }

        /// <summary>
        /// Runs an application fetches the outputs and returns the result back to the caller
        /// </summary>
        /// <param name="applicationName">the path to the application image</param>
        /// <param name="arguments">the arguments that must be passed to the program</param>
        public ProgramTerminationInformation RunApplication(string applicationName, string arguments)
        {
            ProcessStartInfo pif = new ProcessStartInfo
            {
                Arguments = arguments,
                CreateNoWindow = Hidden,
                FileName = applicationName,
                RedirectStandardError = RedirectConsole,
                RedirectStandardOutput = RedirectConsole,
                UseShellExecute = UseShellExecute
            };
            return Run(pif, -1, 0);
        }

        /// <summary>
        /// Runs an application fetches the outputs and returns the result back to the caller
        /// </summary>
        /// <param name="applicationName">the path to the application image</param>
        /// <param name="executionDirectory">the working directory of the called process</param>
        /// <param name="arguments">the arguments that must be passed to the program</param>
        public ProgramTerminationInformation RunApplication(string applicationName, string executionDirectory,
                                                            string arguments)
        {
            ProcessStartInfo pif = new ProcessStartInfo
            {
                Arguments = arguments,
                CreateNoWindow = Hidden,
                FileName = applicationName,
                RedirectStandardError = RedirectConsole,
                RedirectStandardOutput = RedirectConsole,
                UseShellExecute = UseShellExecute,
                WorkingDirectory = executionDirectory
            };

            return Run(pif, -1, 0);
        }

        /// <summary>
        /// Runs an application fetches the outputs and returns the result back to the caller
        /// </summary>
        /// <param name="info">the ProcessStart-Info that defines a process to run. The info may be modified depending on the configuration of this invokator</param>
        /// <param name="timeout">the timeout to wait before the process is stopped</param>
        /// <param name="maxRetryCount">the maximum retry-count before the execution is considered a failure</param>
        public ProgramTerminationInformation RunApplication(ProcessStartInfo info, int timeout = -1, int maxRetryCount = 0)
        {
            info.CreateNoWindow = Hidden;
            info.RedirectStandardError = RedirectConsole;
            info.RedirectStandardOutput = RedirectConsole;
            info.UseShellExecute = UseShellExecute;
            return Run(info, timeout, maxRetryCount);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="applicationName">the path to the application image</param>
        /// <param name="arguments">the arguments that must be passed to the program</param>
        /// <param name="timeout">the timeout in which the program must terminate</param>
        /// <param name="maxRetryCount">the maximum number of retries</param>
        /// <returns>a program termination information object that contains information about the application run</returns>
        public async Task<ProgramTerminationInformation> RunApplicationAsync(string applicationName, string arguments, int timeout,
                                                            int maxRetryCount)
        {
            ProcessStartInfo pif = new ProcessStartInfo
            {
                Arguments = arguments,
                CreateNoWindow = Hidden,
                FileName = applicationName,
                RedirectStandardError = RedirectConsole,
                RedirectStandardOutput = RedirectConsole,
                UseShellExecute = UseShellExecute
            };
            return await RunAsync(pif, timeout, maxRetryCount);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="applicationName">the path to the application image</param>
        /// <param name="executionDirectory">the working directory of the called process</param>
        /// <param name="arguments">the arguments that must be passed to the program</param>
        /// <param name="timeout">the timeout in which the program must terminate</param>
        /// <param name="maxRetryCount">the maximum number of retries</param>
        /// <returns>a program termination information object that contains information about the application run</returns>
        public async Task<ProgramTerminationInformation> RunApplicationAsync(string applicationName, string executionDirectory, string arguments, int timeout,
                                                            int maxRetryCount)
        {
            ProcessStartInfo pif = new ProcessStartInfo
            {
                Arguments = arguments,
                CreateNoWindow = Hidden,
                FileName = applicationName,
                RedirectStandardError = RedirectConsole,
                RedirectStandardOutput = RedirectConsole,
                UseShellExecute = UseShellExecute,
                WorkingDirectory = executionDirectory
            };

            return await RunAsync(pif, timeout, maxRetryCount);
        }

        /// <summary>
        /// Runs an application fetches the outputs and returns the result back to the caller
        /// </summary>
        /// <param name="applicationName">the path to the application image</param>
        /// <param name="arguments">the arguments that must be passed to the program</param>
        public async Task<ProgramTerminationInformation> RunApplicationAsync(string applicationName, string arguments)
        {
            ProcessStartInfo pif = new ProcessStartInfo
            {
                Arguments = arguments,
                CreateNoWindow = Hidden,
                FileName = applicationName,
                RedirectStandardError = RedirectConsole,
                RedirectStandardOutput = RedirectConsole,
                UseShellExecute = UseShellExecute
            };
            return await RunAsync(pif, -1, 0);
        }

        /// <summary>
        /// Runs an application fetches the outputs and returns the result back to the caller
        /// </summary>
        /// <param name="info">the ProcessStart-Info that defines a process to run. The info may be modified depending on the configuration of this invokator</param>
        /// <param name="timeout">the timeout to wait before the process is stopped</param>
        /// <param name="maxRetryCount">the maximum retry-count before the execution is considered a failure</param>
        public async Task<ProgramTerminationInformation> RunApplicationAsync(ProcessStartInfo info, int timeout= -1, int maxRetryCount = 0)
        {
            info.CreateNoWindow = Hidden;
            info.RedirectStandardError = RedirectConsole;
            info.RedirectStandardOutput = RedirectConsole;
            info.UseShellExecute = UseShellExecute;
            return await RunAsync(info, timeout, maxRetryCount);
        }

        /// <summary>
        /// Runs an application fetches the outputs and returns the result back to the caller
        /// </summary>
        /// <param name="applicationName">the path to the application image</param>
        /// <param name="executionDirectory">the working directory of the called process</param>
        /// <param name="arguments">the arguments that must be passed to the program</param>
        public async Task<ProgramTerminationInformation> RunApplicationAsync(string applicationName, string executionDirectory,
                                                            string arguments)
        {
            ProcessStartInfo pif = new ProcessStartInfo
            {
                Arguments = arguments,
                CreateNoWindow = Hidden,
                FileName = applicationName,
                RedirectStandardError = RedirectConsole,
                RedirectStandardOutput = RedirectConsole,
                UseShellExecute = UseShellExecute,
                WorkingDirectory = executionDirectory
            };

            return await RunAsync(pif, -1, 0);
        }

        /// <summary>
        /// Executes the given ProcessStartInfo
        /// </summary>
        /// <param name="pif">the processStartInfo that is used to call an external program</param>
        /// <param name="timeout">the timeout in which the program must terminate</param>
        /// <param name="maxRetryCount">the maximum number of retries</param>
        /// <returns>the TerminationInformation of the called process</returns>
        protected virtual ProgramTerminationInformation Run(ProcessStartInfo pif, int timeout, int maxRetryCount)
        {
            return AsyncHelpers.RunSync(() => RunAsync(pif, timeout, maxRetryCount));
        }

        private async Task<bool> RunProc(Process proc, StringBuilder con, StringBuilder err, int timeout)
        {
            var token = new CancellationToken();
            var src = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (timeout > 0)
            {
                src.CancelAfter(timeout);
            }

            proc.Start();
            List<Task> waits = new List<Task>();
            if (proc.StartInfo.RedirectStandardError)
            {
                waits.Add(ReadStream(proc.StandardError, err, true, proc.StartInfo.FileName, OnErrorOutput, token));
            }

            if (proc.StartInfo.RedirectStandardOutput)
            {
                waits.Add(ReadStream(proc.StandardOutput, con, false, proc.StartInfo.FileName, OnConsoleOutput, token));
            }

#if NETCOREAPP3_1
                waits.Add(WaitForExit(proc, timeout));
#else
            waits.Add(proc.WaitForExitAsync(token));
#endif
            waits.ForEach(n => n.ConfigureAwait(false));
            await Task.WhenAll(waits);
            return waits.All(n => n.IsCompletedSuccessfully);
        }

        private Task WaitForExit(Process proc, int timeout)
        {
            if (timeout > 0)
            {
                if (proc.WaitForExit(timeout))
                {
                    return Task.CompletedTask;
                }

                var token = new CancellationToken(true);
                return Task.FromCanceled(token);
            }

            proc.WaitForExit();
            return Task.CompletedTask;
        }

        private async Task ReadStream(StreamReader procStandardOutput, StringBuilder con, bool isError, string topic, Action<string> raiseEventAction, CancellationToken token)
        {
            string s = null;
            while ((s = await procStandardOutput.ReadLineAsync().WithCancellation(token).ConfigureAwait(false)) != null)
            {
                con.AppendLine(s);
                LogEnvironment.LogDebugEvent(null,s,isError?(int)LogSeverity.Error:(int)LogSeverity.Report, topic);
                raiseEventAction(s);
            }
        }

        protected virtual async Task<ProgramTerminationInformation> RunAsync(ProcessStartInfo pif, int timeout, int maxRetryCount)
        {
            int exitCode = -1;
            Process proc = new Process() { StartInfo = pif };
            var err = new StringBuilder();
            var con = new StringBuilder();
            var completed = false;
            if (timeout > 0)
            {
                var ok = false;
                while (!ok && maxRetryCount >= 0)
                {
                    err.Clear();
                    con.Clear();
                    completed = await RunProc(proc, con, err, timeout);
                    try
                    {
                        if (!completed)
                        {
                            proc.Kill();
                            maxRetryCount--;
                        }
                        else
                        {
                            ok = true;
                        }
                    }
                    finally
                    {
                        exitCode = proc.ExitCode;
                        proc.Close();
                        if (!ok)
                        {
                            proc = new Process()
                            {
                                StartInfo = pif
                            };
                        }
                    }
                }
            }
            else
            {
                try
                {
                    completed = await RunProc(proc, con, err, -1);
                }
                finally
                {
                    exitCode = proc.ExitCode;
                    proc.Close();
                }
            }

            if (RedirectConsole)
            {
                return new ProgramTerminationInformation
                {
                    ExitCode = exitCode, ConsoleOutput = con.ToString(),
                    ErrorOutput = err.ToString(),
                    Completed = completed
                };
            }

            return new ProgramTerminationInformation
            {
                ExitCode = exitCode,
                Completed = completed
            };
        }

        /// <summary>
        /// Raises the ConsoleOutput event
        /// </summary>
        /// <param name="e">the string that was read from the console-stream</param>
        protected virtual void OnConsoleOutput(string e)
        {
            ConsoleOutput?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the ErrorOutput event
        /// </summary>
        /// <param name="e">the string that was read from the error-stream</param>
        protected virtual void OnErrorOutput(string e)
        {
            ErrorOutput?.Invoke(this, e);
        }

        /// <summary>
        /// Is fired, when the current process fires a normal console output
        /// </summary>
        public event EventHandler<string> ConsoleOutput;

        /// <summary>
        /// Is fired, when the current process fires an error output
        /// </summary>
        public event EventHandler<string> ErrorOutput;
    }
}
