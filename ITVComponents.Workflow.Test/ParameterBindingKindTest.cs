using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    // MSTEST0032 ist hier bewusst aus: der Analyzer sieht eine Bedingung, die der Compiler
    // konstant faltet, und haelt die Zusicherung fuer sinnlos. Genau das ist ihr Zweck - sie soll
    // BRECHEN, wenn jemand einen Eintrag in der Mitte der Aufzaehlung einfuegt. Definitionen werden
    // mit diesen Zahlen gespeichert; ohne diesen Riegel wuerde aus jeder gespeicherten Bindung still
    // eine andere, und nichts wuerde rot.
    #pragma warning disable MSTEST0032

    /// <summary>
    /// Haelt die Bindungsarten fest - vor allem ihre ZAHLEN.
    /// </summary>
    [TestClass]
    public class ParameterBindingKindTest
    {
        /// <summary>
        /// Definitionen werden mit den Zahlen dieser Aufzaehlung gespeichert (<see cref="WorkflowJson"/>
        /// fuehrt bewusst keinen String-Konverter - in den Definitionen steht <c>"Kind": 1</c>). Ein
        /// neuer Eintrag gehoert deshalb ans ENDE: einer in der Mitte macht aus jeder gespeicherten
        /// <see cref="ParameterBindingKind.Expression"/>-Bindung still eine
        /// <see cref="ParameterBindingKind.Variable"/>, und zwar ohne dass irgendetwas rot wird.
        /// </summary>
        [TestMethod]
        public void The_Numbers_Of_The_Binding_Kinds_Are_The_Contract()
        {
            Assert.AreEqual(0, (int)ParameterBindingKind.Literal);
            Assert.AreEqual(1, (int)ParameterBindingKind.Variable);
            Assert.AreEqual(2, (int)ParameterBindingKind.Expression);
            Assert.AreEqual(3, (int)ParameterBindingKind.ValueHandle);
        }

        /// <summary>Dieselbe Regel gilt fuer die beiden Schalter der ValueHandle-Bindung.</summary>
        [TestMethod]
        public void The_Numbers_Of_Delivery_And_WriteBack_Are_The_Contract()
        {
            Assert.AreEqual(0, (int)ValueDelivery.Handle);
            Assert.AreEqual(1, (int)ValueDelivery.Value);
            Assert.AreEqual(0, (int)ValueWriteBackMode.Never);
            Assert.AreEqual(1, (int)ValueWriteBackMode.OnSuccess);
        }

        /// <summary>
        /// Die Vorgaben sind die vorsichtigen: ohne ausdrueckliche Angabe wird nichts geschrieben, und
        /// die Aktivitaet bekommt den Griff - der faellt als Typfehler sofort auf, wenn er falsch ist.
        /// </summary>
        [TestMethod]
        public void The_Defaults_Are_The_Cautious_Ones()
        {
            var binding = new ActivityInputBinding();
            Assert.AreEqual(ParameterBindingKind.Literal, binding.Kind);
            Assert.AreEqual(ValueDelivery.Handle, binding.Delivery);
            Assert.AreEqual(ValueWriteBackMode.Never, binding.WriteBack);
            Assert.IsFalse(binding.AllowInParallelRegion);
        }

        [TestMethod]
        public void A_ValueHandle_Binding_Survives_The_Round_Trip()
        {
            var node = new AutomatedActivityNode { Id = "n", ActivityRef = "touch" };
            node.Inputs.Add(new ActivityInputBinding
            {
                Parameter = "order",
                Kind = ParameterBindingKind.ValueHandle,
                HandlerName = "orders",
                Delivery = ValueDelivery.Value,
                WriteBack = ValueWriteBackMode.OnSuccess,
                AllowInParallelRegion = true,
                HandlerArguments = new List<ActivityInputBinding>
                {
                    new ActivityInputBinding
                    {
                        Parameter = "key", Kind = ParameterBindingKind.Variable, Source = "orderId"
                    }
                }
            });

            var definition = new WorkflowDefinition
            {
                TechnicalName = "vh", Version = 1, Name = "Griff",
                Nodes = new List<WorkflowNode> { node }
            };

            WorkflowDefinition copy = WorkflowJson.ImportDefinition(WorkflowJson.ExportDefinition(definition));

            ActivityInputBinding read = copy.Nodes.OfType<AutomatedActivityNode>().Single().Inputs.Single();
            Assert.AreEqual(ParameterBindingKind.ValueHandle, read.Kind);
            Assert.AreEqual("orders", read.HandlerName);
            Assert.AreEqual(ValueDelivery.Value, read.Delivery);
            Assert.AreEqual(ValueWriteBackMode.OnSuccess, read.WriteBack);
            Assert.IsTrue(read.AllowInParallelRegion);
            Assert.AreEqual(1, read.HandlerArguments.Count);
            Assert.AreEqual("orderId", read.HandlerArguments[0].Source,
                "the arguments run through the same binding machinery and must survive it typed.");
        }

        [TestMethod]
        public void A_UserTask_WriteBack_List_Survives_The_Round_Trip()
        {
            var node = new UserActivityNode { Id = "u", TaskKey = "Edit" };
            node.WriteBackParameters.Add("customer");
            var definition = new WorkflowDefinition
            {
                TechnicalName = "wb", Version = 1, Name = "Zurueck",
                Nodes = new List<WorkflowNode> { node }
            };

            WorkflowDefinition copy = WorkflowJson.ImportDefinition(WorkflowJson.ExportDefinition(definition));

            UserActivityNode read = copy.Nodes.OfType<UserActivityNode>().Single();
            CollectionAssert.AreEqual(new[] { "customer" }, read.WriteBackParameters.ToArray());
        }
    }
}
#pragma warning restore MSTEST0032
