using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            Process proc = Process.Start(pif);
            if (timeout > 0)
            {
                while (!proc.WaitForExit(timeout * 1000) && maxRetryCount >= 0)
                {
                    proc.Kill();
                    maxRetryCount--;
                    proc = Process.Start(pif);
                }
            }
            else
            {
                proc.WaitForExit();
            }

            if (RedirectConsole)
            {
                return new ProgramTerminationInformation
                {
                    ExitCode = proc.ExitCode, ConsoleOutput = proc.StandardOutput.ReadToEnd(),
                    ErrorOutput = proc.StandardError.ReadToEnd()
                };
            }

            return new ProgramTerminationInformation
            {
                ExitCode = proc.ExitCode
            };
        }

        protected async virtual Task<ProgramTerminationInformation> RunAsync(ProcessStartInfo pif, int timeout, int maxRetryCount)
        {
            Process proc = Process.Start(pif);
            if (timeout > 0)
            {
                while (!await WaitForExit(proc, timeout) && maxRetryCount >= 0)
                {
                    proc.Kill();
                    maxRetryCount--;
                    proc = Process.Start(pif);
                }
            }
            else
            {
#if NETCOREAPP3_1
                proc.WaitForExit();
#else
                await proc.WaitForExitAsync();
#endif
            }

            if (RedirectConsole)
            {
                return new ProgramTerminationInformation
                {
                    ExitCode = proc.ExitCode, ConsoleOutput = await proc.StandardOutput.ReadToEndAsync(),
                    ErrorOutput = await proc.StandardError.ReadToEndAsync()
                };
            }

            return new ProgramTerminationInformation
            {
                ExitCode = proc.ExitCode
            };
        }

        protected async Task<bool> WaitForExit(Process proc, int timeout)
        {
#if NETCOREAPP3_1
            return proc.WaitForExit(timeout * 1000);
#else
            var token = new CancellationToken();
            var src = CancellationTokenSource.CreateLinkedTokenSource(token);
            try
            {
                src.CancelAfter(timeout * 1000);
                await proc.WaitForExitAsync(src.Token);
                return true;
            }
            catch (TaskCanceledException ex)
            {
                return false;
            }
            finally
            {
                src.Dispose();
            }
#endif
        }
    }
}
