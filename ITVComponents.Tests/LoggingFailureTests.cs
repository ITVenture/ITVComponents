using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ITVComponents.Logging;
using ITVComponents.Logging.DefaultLoggers.FileLogging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Tests
{
    /// <summary>
    /// A log target that fails must neither stay silent nor take the message from the other targets (§122).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class LoggingFailureTests
    {
        private string dir = null!;

        [TestInitialize]
        public void Setup()
        {
            dir = Path.Combine(Path.GetTempPath(), "itv-log-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Could not remove test directory {dir}: {ex}");
            }
        }

        [TestMethod]
        public void A_Rotation_Blocked_By_Another_Process_Is_Written_Into_The_File_Once_And_Logging_Goes_On()
        {
            var logName = Path.Combine(dir, "agent.log");
            var writer = new FileLogWriter(logName, 2, 100, true, 0, -1);
            try
            {
                // Like a virus scanner: reads the file and lets others write, but not move it.
                using (new FileStream(logName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    for (var i = 0; i < 30; i++)
                    {
                        writer.LogEvent($"line {i}", 0, null);
                    }
                }
            }
            finally
            {
                writer.Dispose();
            }

            var text = File.ReadAllText(logName);
            Assert.AreEqual(1, Regex.Matches(text, "Log rotation of").Count, "reported once, the next attempt waits for the retry delay");
            StringAssert.Contains(text, "IOException");
            for (var i = 0; i < 30; i++)
            {
                StringAssert.Contains(text, $"line {i}" + Environment.NewLine);
            }

            Assert.IsFalse(File.Exists(Path.Combine(dir, "agent_0.log")), "the rotation did not happen");
        }

        [TestMethod]
        public void A_Rotation_Without_Hindrance_Still_Switches_The_File()
        {
            var logName = Path.Combine(dir, "agent.log");
            var writer = new FileLogWriter(logName, 2, 100, true, 0, -1);
            try
            {
                for (var i = 0; i < 30; i++)
                {
                    writer.LogEvent($"line {i}", 0, null);
                }
            }
            finally
            {
                writer.Dispose();
            }

            Assert.IsTrue(File.Exists(Path.Combine(dir, "agent_0.log")));
            StringAssert.Contains(File.ReadAllText(logName), "line 29");
            Assert.IsFalse(File.ReadAllText(logName).Contains("Log rotation of"));
        }

        [TestMethod]
        public void A_Throwing_Target_Does_Not_Take_The_Message_From_The_Targets_Behind_It_And_Is_Reported_Once()
        {
            var broken = new TestTarget(true);
            var working = new TestTarget(false);
            var trace = new CollectingListener();
            Trace.Listeners.Add(trace);
            LogEnvironment.RegisterLogTarget(broken);
            LogEnvironment.RegisterLogTarget(working);
            var hooked = new List<string>();
            try
            {
                using (LogEnvironment.SetTemporaryLogHook((debug, text, severity, context) => hooked.Add(text)))
                {
                    LogEnvironment.LogEvent("first", LogSeverity.Report);
                    LogEnvironment.LogEvent("second", LogSeverity.Report);
                    LogEnvironment.LogEvent("third", LogSeverity.Report);
                }
            }
            finally
            {
                broken.Dispose();
                working.Dispose();
                Trace.Listeners.Remove(trace);
            }

            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, working.Messages);
            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, hooked);
            Assert.AreEqual(1, trace.Lines.Count(l => l.Contains(typeof(TestTarget).FullName!) && l.Contains("failed")), string.Join(Environment.NewLine, trace.Lines));
        }

        private sealed class TestTarget : ILogTarget, IDisposable
        {
            private readonly bool fail;

            public TestTarget(bool fail)
            {
                this.fail = fail;
            }

            public List<string> Messages { get; } = new();

            public event EventHandler? Disposed;

            public void LogEvent(string eventText, int severity, string context)
            {
                if (fail)
                {
                    throw new IOException("the target is broken");
                }

                Messages.Add(eventText);
            }

            public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
        }

        private sealed class CollectingListener : TraceListener
        {
            public List<string> Lines { get; } = new();

            public override void Write(string? message)
            {
            }

            public override void WriteLine(string? message)
            {
                if (message != null)
                {
                    lock (Lines)
                    {
                        Lines.Add(message);
                    }
                }
            }
        }
    }
}
