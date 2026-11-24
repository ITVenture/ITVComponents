using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json.Serialization;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.Linq;
using ITVComponents.DataAccess.Remote.ProxyObjects;
using ITVComponents.InterProcessCommunication.ParallelProcessing;
using ITVComponents.Json;
using ITVComponents.Logging.DefaultLoggers.ProcessBridge.MessageListening;
using ITVComponents.ParallelProcessing;
using ITVComponents.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Tests
{
    /// <summary>
    /// The former IManualSerializer types as plain classes (issue Json 01, follow-up): round trip through an open place
    /// in SerializationTypingMode.EnvelopePolymorphism, as they travel over IPC.
    /// </summary>
    [TestClass]
    public class TransferByValueTypesTests
    {
        private static T RoundTrip<T>(T value) where T : class
        {
            var json = JsonHelper.ToJson<object>(value, SerializationTypingMode.EnvelopePolymorphism);
            return (T)JsonHelper.FromJsonString<object>(json, SerializationTypingMode.EnvelopePolymorphism);
        }

        [TestMethod]
        public void DynamicResult_Keeps_Its_Columns_With_Their_Types()
        {
            var source = new DynamicResult(new Dictionary<string, object> { ["Id"] = 42L, ["Name"] = "x", ["Due"] = new DateTime(2026, 1, 2), ["Empty"] = null });

            var back = RoundTrip(source);

            CollectionAssert.AreEquivalent(new[] { "Id", "Name", "Due", "Empty" }, back.Keys);
            Assert.AreEqual<object>(42L, back["Id"]);
            Assert.AreEqual<object>("x", back["Name"]);
            Assert.AreEqual<object>(new DateTime(2026, 1, 2), back["Due"]);
            Assert.IsTrue(back["Empty"] is null or DBNull, $"came back as {back["Empty"]?.GetType().Name}");
        }

        [TestMethod]
        public void LinqParameter_Keeps_All_Settings()
        {
            var source = new LinqParameter(true) { ParameterName = "p", DbType = DbType.Int64, Direction = ParameterDirection.InputOutput, Value = 7L, Size = 8 };

            var back = RoundTrip(source);

            Assert.AreEqual("p", back.ParameterName);
            Assert.AreEqual(DbType.Int64, back.DbType);
            Assert.AreEqual(ParameterDirection.InputOutput, back.Direction);
            Assert.AreEqual<object>(7L, back.Value);
            Assert.AreEqual(8, back.Size);
            Assert.IsTrue(back.IsNullable);
        }

        [TestMethod]
        public void RemoteDataParameter_Keeps_Its_Value_Without_A_Connection()
        {
            var source = new RemoteDataParameter(new LinqParameter { ParameterName = "q", Value = Guid.Empty }, 5);

            var back = RoundTrip(source);

            Assert.AreEqual("q", back.ParameterName);
            Assert.AreEqual<object>(Guid.Empty, back.Value);
        }

        [TestMethod]
        public void LogMessageEventArgs_Keeps_Message_Severity_And_Context()
        {
            var back = RoundTrip(new LogMessageEventArgs("hello", 3, "ctx"));

            Assert.AreEqual("hello", back.Message);
            Assert.AreEqual(3, back.Severity);
            Assert.AreEqual("ctx", back.Context);
        }

        [TestMethod]
        public void Task_Keeps_Base_State_And_Its_Own_Fields()
        {
            var source = new SampleTask("work", 4) { Active = true, Payload = 1.5m };

            var back = RoundTrip(source);

            Assert.AreEqual("work", back.Description);
            Assert.AreEqual(4, back.Priority);
            Assert.IsTrue(back.Active);
            Assert.AreEqual<object>(1.5m, back.Payload);
        }

        [TestMethod]
        public void Process_Package_Gets_Its_Tasks_Back_With_The_Parent_Set()
        {
            var source = new SamplePackage(11, 2);

            var back = RoundTrip(source);

            Assert.AreEqual(11, back.Id);
            var tasks = back.GetTasks().Cast<SampleProcessTask>().ToList();
            Assert.AreEqual(2, tasks.Count);
            Assert.IsTrue(tasks.All(t => ReferenceEquals(t.Parent, back)), "the tasks belong to the package again");
            Assert.AreEqual("t1", tasks[1].Name);
        }

        [TestMethod]
        public void Former_Manual_Serializers_Travel_By_Value()
        {
            foreach (var type in new[] { typeof(DynamicResult), typeof(LinqParameter), typeof(RemoteDataParameter), typeof(LogMessageEventArgs), typeof(SampleTask), typeof(SamplePackage), typeof(TaskScheduler.ScheduleRequest) })
            {
                Assert.IsTrue(type.IsDefined(typeof(TransferByValueAttribute), true), type.Name);
            }
        }

        public sealed class SampleTask : TaskBase
        {
            [JsonConstructor]
            private SampleTask()
            {
            }

            public SampleTask(string description, int priority)
            {
                Description = description;
                Priority = priority;
            }

            public object Payload { get; set; }

            public override IResourceLock DemandExclusive() => null;

            public override bool IsDuplicateOf(ITask other) => false;

            public override Dictionary<string, object> BuildMetaData() => new Dictionary<string, object>();

            public override IDisposable Unsafe() => null;
        }

        public sealed class SamplePackage : ProcessPackageBase<SampleProcessTask, SamplePackage>
        {
            [JsonConstructor]
            private SamplePackage()
            {
            }

            public SamplePackage(int id, int taskCount)
            {
                Id = id;
                SetSubTasks(Enumerable.Range(0, taskCount).Select(i => new SampleProcessTask($"t{i}")).ToArray());
            }
        }

        public sealed class SampleProcessTask : ProcessTaskBase<SampleProcessTask, SamplePackage>
        {
            [JsonConstructor]
            private SampleProcessTask()
            {
            }

            public SampleProcessTask(string name)
            {
                Name = name;
            }

            public string Name { get; set; }

            public override bool IsDuplicateOf(ITask other) => false;

            public override Dictionary<string, object> BuildMetaData() => new Dictionary<string, object>();
        }
    }
}
