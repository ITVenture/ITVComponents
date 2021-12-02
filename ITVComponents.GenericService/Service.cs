//-----------------------------------------------------------------------
// <copyright file="Service.cs" company="IT-Venture GmbH">
//     2009 by IT-Venture GmbH
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using ITVComponents.CommandLineParser;
using ITVComponents.Helpers;
using ITVComponents.Settings.Native;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

//using GenericService.Update;

namespace ITVComponents.GenericService
{
    /// <summary>
    /// the Mainclass of the Service
    /// </summary>
    public static class Service
    {
        /// <summary>
        /// Sets the Local-Directory and Settings-location to the bin-location of the service
        /// </summary>
        public static void SetLocalPath()
        {
            var loca = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            Directory.SetCurrentDirectory(loca);
            NativeSettings.Builder.SetBasePath(loca);
        }

        /// <summary>
        /// runs the Service
        /// </summary>
        /// <param name="parameters">Startup Parameters of the Application</param>
        /// <param name="configureHost">callback to configure the host, before it is started</param>
        public static void Run(string[] parameters, Action<IHostBuilder> configureHost)
        {
            var parser = new CommandLineParser.CommandLineParser(typeof(StartupArguments), false);
            var arg = new StartupArguments();
            parser.Configure(parameters, arg);

            try
            {
                AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
                Console.WriteLine("Current Platform is: {0}",
                    IntPtr.Size == 4 ? "x86" : IntPtr.Size == 8 ? "x64" : "unkonwn");
                if (arg.Install)
                {
                    var locationRaw = Assembly.GetEntryAssembly().Location;
                    var exeLocation = Path.Combine(Path.GetDirectoryName(locationRaw),Path.GetFileNameWithoutExtension(locationRaw));
                    File.WriteAllText($"{Assembly.GetEntryAssembly().Location}.install.bat",
                        $"sc.exe create \"{ServiceConfigHelper.ServiceName}\" binpath=\"{exeLocation}.exe\" start={ToStartType(ServiceConfigHelper.StartType)} displayname=\"{ServiceConfigHelper.DisplayName ?? ServiceConfigHelper.ServiceName}\" {((ServiceConfigHelper.Dependencies?.Count ?? 0) == 0 ? "" : $"depend=\"{string.Join("/", ServiceConfigHelper.Dependencies)}\"")}");
                    Console.WriteLine($"{Assembly.GetEntryAssembly().Location}.install.bat ausführen.");
                }
                else if (arg.UnInstall)
                {
                    File.WriteAllText($"{Assembly.GetEntryAssembly().Location}.uninstall.bat",
                        $"sc.exe uninstall \"{ServiceConfigHelper.ServiceName}\"");
                    Console.WriteLine($"{Assembly.GetEntryAssembly().Location}.uninstall.bat ausführen.");
                }
                else if (arg.Help)
                {
                    Console.WriteLine(parser.PrintUsage(true, 10, 75));
                }
                else
                {
                    ServiceStartup.RunService(arg, configureHost);
                }
            }
            catch (CommandLineSyntaxException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(parser.PrintUsage(true, 10, 75));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.OutlineException());
            }
        }

        private static string ToStartType(ServiceStartMode startType)
        {
            switch (startType)
            {
                case ServiceStartMode.Boot:
                    return "boot";
                case ServiceStartMode.System:
                    return "system";
                case ServiceStartMode.Automatic:
                    return "auto";
                case ServiceStartMode.Manual:
                    return "demand";
                case ServiceStartMode.Disabled:
                    return "disabled";
                default:
                    throw new ArgumentOutOfRangeException(nameof(startType), startType, null);
            }
        }

        /// <summary>
        /// Attempt to find out why the service sometimes crashes for no reason
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="unhandledExceptionEventArgs">the arguments for the unhandled exception</param>
        private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
        {
            try
            {
                string pth = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string specialLogFile = string.Format(@"{0}\Crash{1}.log", pth, DateTime.Now.Ticks);
                using (StreamWriter wr = new StreamWriter(specialLogFile, false, System.Text.Encoding.Default))
                {
                    wr.WriteLine(unhandledExceptionEventArgs.ExceptionObject);
                    wr.WriteLine("IsTerminating: {0}", unhandledExceptionEventArgs.IsTerminating);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}