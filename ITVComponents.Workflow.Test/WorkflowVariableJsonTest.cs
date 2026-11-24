using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// The variable stack in the envelope format (issue Json 01, phase 4): registered types come back typed on every
    /// level, collections in their exact type, unregistered types untyped but not lost.
    /// </summary>
    [TestClass]
    public class WorkflowVariableJsonTest
    {
        [TestMethod]
        public void Registered_Records_Come_Back_Typed_Also_In_Nested_Object_Fields()
        {
            WorkflowJson.RegisterVariableType<Order>("wfvartest.order");
            WorkflowJson.RegisterVariableType<Line>("wfvartest.line");
            var variables = new Dictionary<string, object>
            {
                ["order"] = new Order { Number = "A-1", Payload = new Line { Sku = "x", Quantity = 3 } },
                ["lines"] = new List<Line> { new Line { Sku = "y", Quantity = 1 } },
                ["amount"] = 42,
                ["big"] = 42L,
                ["price"] = 9.95m,
                ["due"] = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                ["name"] = "text",
                ["flag"] = true,
                ["nothing"] = null
            };

            var json = WorkflowJson.SerializeVariables(variables);
            var back = WorkflowJson.DeserializeVariables(json);

            var order = (Order)back["order"];
            Assert.AreEqual("A-1", order.Number);
            Assert.IsInstanceOfType<Line>(order.Payload, "an object field inside a record keeps its type");
            Assert.AreEqual(3, ((Line)order.Payload).Quantity);
            Assert.IsInstanceOfType<List<Line>>(back["lines"], "a list comes back as list, not as array");
            Assert.AreEqual<object>(42, back["amount"]);
            Assert.AreEqual<object>(42L, back["big"]);
            Assert.AreEqual<object>(9.95m, back["price"]);
            Assert.AreEqual<object>(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), back["due"]);
            Assert.AreEqual<object>("text", back["name"]);
            Assert.AreEqual<object>(true, back["flag"]);
            Assert.IsTrue(back.ContainsKey("nothing"));
            Assert.IsNull(back["nothing"]);

            var root = JsonNode.Parse(json)!.AsObject();
            Assert.AreEqual("wfvartest.order", root["order"]!["$type"]!.GetValue<string>(), "readable, one property per variable");
        }

        [TestMethod]
        public void Unregistered_Types_Come_Back_As_Plain_Values_And_Are_Logged()
        {
            var variables = new Dictionary<string, object> { ["record"] = new Unregistered { Value = 5 } };
            var json = WorkflowJson.SerializeVariables(variables);

            var messages = new List<string>();
            Dictionary<string, object> back;
            using (LogEnvironment.SetTemporaryLogHook((debug, text, severity, context) => messages.Add(text)))
            {
                back = WorkflowJson.DeserializeVariables(json);
            }

            var record = back["record"] as Dictionary<string, object>;
            Assert.IsNotNull(record, $"came back as {back["record"]?.GetType().FullName ?? "null"}");
            Assert.AreEqual<object>(5, record["Value"]);
            Assert.IsTrue(messages.Any(m => m.Contains("Unregistered")), "the reason is in the log");
        }

        [TestMethod]
        public void Iteration_Failures_Keep_Their_Item_Type()
        {
            var failures = new List<object>
            {
                new IterationFailure { Item = 7L, Index = 1, Message = "locked" },
                new IterationFailure { Item = Guid.Empty, Index = 2, Message = "crash", ExceptionType = "System.Exception" }
            };

            var back = WorkflowJson.DeserializeVariables(
                WorkflowJson.SerializeVariables(new Dictionary<string, object> { ["failures"] = failures }));

            var list = ((IEnumerable<object>)back["failures"]).Cast<IterationFailure>().ToList();
            Assert.AreEqual<object>(7L, list[0].Item);
            Assert.AreEqual(1, list[0].Index);
            Assert.AreEqual<object>(Guid.Empty, list[1].Item);
            Assert.IsTrue(list[1].WasThrown);
        }

        [TestMethod]
        public void Compensations_Carry_Their_Variable_State()
        {
            var entries = new List<CompensationEntry>
            {
                new CompensationEntry
                {
                    Id = "c1", Sequence = 1, NodeId = "n", HandlerNodeId = "h",
                    Variables = new Dictionary<string, object> { ["count"] = 3, ["when"] = TimeSpan.FromHours(1) }
                }
            };

            var back = WorkflowJson.DeserializeCompensations(WorkflowJson.SerializeCompensations(entries));

            Assert.AreEqual<object>(3, back[0].Variables["count"]);
            Assert.AreEqual<object>(TimeSpan.FromHours(1), back[0].Variables["when"]);
        }

        [TestMethod]
        public void Definition_Literals_And_Configuration_Keep_Their_Exact_Type()
        {
            var due = new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc);
            var node = new AutomatedActivityNode { Id = "a", ActivityRef = "x" };
            node.Inputs.Add(new ActivityInputBinding { Parameter = "big", Kind = ParameterBindingKind.Literal, Literal = 42L });
            node.Inputs.Add(new ActivityInputBinding { Parameter = "due", Kind = ParameterBindingKind.Literal, Literal = due });
            node.Inputs.Add(new ActivityInputBinding { Parameter = "text", Kind = ParameterBindingKind.Literal, Literal = "plain" });
            node.Configuration["factor"] = 2.5d;
            node.Configuration["id"] = Guid.Empty;
            node.Configuration["retries"] = 3;

            var json = WorkflowJson.ExportDefinition(new WorkflowDefinition { TechnicalName = "lit", Nodes = new List<WorkflowNode> { node } });
            var back = (AutomatedActivityNode)WorkflowJson.ImportDefinition(json).Nodes[0];

            Assert.AreEqual<object>(42L, back.Inputs[0].Literal);
            Assert.AreEqual<object>(due, back.Inputs[1].Literal);
            Assert.AreEqual<object>("plain", back.Inputs[2].Literal);
            Assert.AreEqual<object>(2.5d, back.Configuration["factor"]);
            Assert.AreEqual<object>(Guid.Empty, back.Configuration["id"]);
            Assert.AreEqual<object>(3, back.Configuration["retries"]);
            StringAssert.Contains(json, "\"kind\": \"activity\"", "the portable kind discriminator stays");
        }

        [TestMethod]
        public void Bare_Values_Of_Existing_Definitions_Are_Read_As_Before()
        {
            var json = "{\"TechnicalName\":\"old\",\"Nodes\":[{\"kind\":\"activity\",\"Id\":\"a\",\"ActivityRef\":\"x\"," +
                       "\"Inputs\":[{\"Parameter\":\"n\",\"Literal\":7}],\"Configuration\":{\"rate\":1.5,\"name\":\"t\",\"obj\":{\"X\":1}}}]}";

            var node = (AutomatedActivityNode)WorkflowJson.ImportDefinition(json).Nodes[0];

            Assert.AreEqual<object>(7, node.Inputs[0].Literal);
            Assert.AreEqual<object>(1.5m, node.Configuration["rate"]);
            Assert.AreEqual<object>("t", node.Configuration["name"]);
            Assert.AreEqual<object>(1, ((Dictionary<string, object>)WorkflowJson.Materialize(node.Configuration["obj"]))["X"]);
        }

        [TestMethod]
        public void The_Former_Format_Is_Rejected_Loudly()
        {
            var former = "{\"$$Type\":\"wf-vars\",\"Data\":[{\"PropertyName\":\"a\",\"TypeName\":\"int\",\"Data\":1}]}";

            Assert.ThrowsExactly<JsonException>(() => WorkflowJson.DeserializeVariables(former));
        }

        public class Order
        {
            public string Number { get; set; }

            public object Payload { get; set; }
        }

        public class Line
        {
            public string Sku { get; set; }

            public int Quantity { get; set; }
        }

        public class Unregistered
        {
            public int Value { get; set; }
        }
    }
}
