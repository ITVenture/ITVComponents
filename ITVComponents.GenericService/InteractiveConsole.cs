//-----------------------------------------------------------------------
// <copyright file="InteractiveConsole.cs" company="IT-Venture GmbH">
//     2024 by IT-Venture GmbH
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.Logging.DefaultLoggers.Console;
using ITVComponents.Plugins;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Security;
using PrettyPrompt;

namespace ITVComponents.GenericService
{
    /// <summary>
    /// Provides an interactive CScript-REPL that runs against an already populated <see cref="PluginFactory"/>.
    /// The factory is exposed to the REPL as the <c>factory</c>-variable, so the operator can inspect and drive the
    /// loaded plugins without the service-workers actually running.
    /// </summary>
    public static class InteractiveConsole
    {
        /// <summary>
        /// Name under which the <see cref="PluginFactory"/> is made available inside the REPL-scope.
        /// </summary>
        private const string FactoryVariableName = "factory";

        /// <summary>
        /// Runs the interactive REPL against the given factory. Blocks until the operator leaves the REPL.
        /// </summary>
        /// <param name="factory">the fully populated (but not deferred-initialized) plugin-factory</param>
        public static void Run(PluginFactory factory)
        {
            new Log2Console(-1, -1, true, true, true);
            RunAsync(factory).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Runs the interactive REPL against the given factory.
        /// </summary>
        /// <param name="factory">the fully populated (but not deferred-initialized) plugin-factory</param>
        public static async Task RunAsync(PluginFactory factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            // The factory is seeded into the persistent repl-scope so it stays available across all evaluated lines.
            var baseValues = new Dictionary<string, object> { { FactoryVariableName, factory } };
            using var session = ExpressionParser.BeginRepl(
                baseValues,
                a => DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession),
                ScriptingPolicy.Default);

            PrintBanner();

            var historyFile = Path.Combine(Path.GetTempPath(), "itv-service-repl-history.txt");
            await using var prompt = new Prompt(persistentHistoryFilepath: historyFile);

            while (true)
            {
                var response = await prompt.ReadLineAsync();
                if (!response.IsSuccess)
                {
                    // Ctrl+C / Ctrl+D on an empty line
                    break;
                }

                var input = response.Text?.Trim();
                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }

                if (IsExitCommand(input))
                {
                    break;
                }

                if (TryHandleMetaCommand(input, factory))
                {
                    continue;
                }

                try
                {
                    var result = LooksLikeBlock(input)
                        ? ExpressionParser.ParseBlock(input, session)
                        : ExpressionParser.Parse(input, session);
                    PrintResult(result);
                }
                catch (Exception ex)
                {
                    PrintError(ex);
                }
            }

            Console.WriteLine("Leaving interactive mode.");
        }

        /// <summary>
        /// Decides whether the input should be evaluated as a statement-block rather than a single expression.
        /// </summary>
        private static bool LooksLikeBlock(string input)
        {
            return input.Contains(';') || input.Contains('{') || input.Contains('\n');
        }

        private static bool IsExitCommand(string input)
        {
            return input.Equals("exit", StringComparison.OrdinalIgnoreCase)
                   || input.Equals("quit", StringComparison.OrdinalIgnoreCase)
                   || input.Equals(":q", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryHandleMetaCommand(string input, PluginFactory factory)
        {
            switch (input.ToLowerInvariant())
            {
                case "?":
                case ":help":
                    PrintHelp();
                    return true;
                case ":plugins":
                    PrintPlugins(factory);
                    return true;
                default:
                    return false;
            }
        }

        private static void PrintPlugins(PluginFactory factory)
        {
            var any = false;
            foreach (IPlugin plugin in factory)
            {
                any = true;
                Console.WriteLine($"  {plugin.UniqueName}  ({plugin.GetType().FullName})");
            }

            if (!any)
            {
                Console.WriteLine("  (no plugins loaded)");
            }
        }

        private static void PrintResult(object result)
        {
            if (result == null)
            {
                return;
            }

            WriteColored("=> " + FormatValue(result), ConsoleColor.Cyan);
        }

        private static string FormatValue(object value)
        {
            if (value is string s)
            {
                return s;
            }

            if (value is IDictionary)
            {
                return value.ToString();
            }

            if (value is IEnumerable enumerable)
            {
                var items = enumerable.Cast<object>().Take(50).Select(x => x?.ToString() ?? "null");
                return "[" + string.Join(", ", items) + "]";
            }

            return value.ToString();
        }

        private static void PrintError(Exception ex)
        {
            WriteColored($"{ex.GetType().Name}: {ex.Message}", ConsoleColor.Red);
        }

        private static void WriteColored(string text, ConsoleColor color)
        {
            var previous = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
            }
            finally
            {
                Console.ForegroundColor = previous;
            }
        }

        private static void PrintBanner()
        {
            Console.WriteLine();
            Console.WriteLine("ITV interactive service-console (CScript REPL).");
            Console.WriteLine("The service-workers are NOT started; the loaded plugins are available via the 'factory' variable.");
            Console.WriteLine("Type ':help' for a list of commands, 'exit' to leave.");
            Console.WriteLine();
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Commands:");
            Console.WriteLine("  :help / ?       Show this help");
            Console.WriteLine("  :plugins        List the loaded plugins");
            Console.WriteLine("  exit / quit     Leave the interactive mode");
            Console.WriteLine();
            Console.WriteLine("Everything else is evaluated as a CScript expression or statement-block.");
            Console.WriteLine("The loaded plugin-factory is available as 'factory', e.g.:");
            Console.WriteLine("  factory[\"MyPlugin\"]            // resolve a plugin by its unique name");
            Console.WriteLine("  factory[\"MyPlugin\"].SomeMethod()");
            Console.WriteLine();
        }
    }
}
