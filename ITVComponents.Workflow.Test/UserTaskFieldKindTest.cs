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
    /// Haelt die Feldarten der generischen Maske fest - vor allem ihre ZAHLEN.
    /// </summary>
    [TestClass]
    public class UserTaskFieldKindTest
    {
        /// <summary>
        /// Definitionen werden mit den Zahlen dieser Aufzaehlung gespeichert (<see cref="WorkflowJson"/>
        /// fuehrt bewusst keinen String-Konverter). Ein neuer Eintrag gehoert deshalb ans ENDE - wer
        /// einen in der Mitte einfuegt, macht aus jeder gespeicherten Auswahl still ein Datum, und
        /// zwar ohne dass irgendetwas rot wird.
        /// </summary>
        [TestMethod]
        public void The_Numbers_Of_The_Field_Kinds_Are_The_Contract()
        {
            Assert.AreEqual(0, (int)UserTaskFieldKind.Text);
            Assert.AreEqual(1, (int)UserTaskFieldKind.MultilineText);
            Assert.AreEqual(2, (int)UserTaskFieldKind.Number);
            Assert.AreEqual(3, (int)UserTaskFieldKind.Boolean);
            Assert.AreEqual(4, (int)UserTaskFieldKind.Date);
            Assert.AreEqual(5, (int)UserTaskFieldKind.Choice);
            Assert.AreEqual(6, (int)UserTaskFieldKind.DateTime);
        }

        [TestMethod]
        public void A_DateTime_Field_Survives_The_Round_Trip()
        {
            var start = new StartNode { Id = "s" };
            start.FormFields.Add(new UserTaskField
            {
                Name = "cutOff", Kind = UserTaskFieldKind.DateTime, Required = true,
                Label = "{\"de\":\"Stichzeit\"}"
            });
            start.FormFields.Add(new UserTaskField { Name = "dueDay", Kind = UserTaskFieldKind.Date });
            var definition = new WorkflowDefinition
            {
                TechnicalName = "appointment", Version = 1, Name = "Termin",
                Nodes = new List<WorkflowNode> { start }
            };

            WorkflowDefinition copy = WorkflowJson.ImportDefinition(WorkflowJson.ExportDefinition(definition));

            StartNode read = copy.Nodes.OfType<StartNode>().Single();
            Assert.AreEqual(UserTaskFieldKind.DateTime, read.FormFields.Single(f => f.Name == "cutOff").Kind);
            Assert.AreEqual(UserTaskFieldKind.Date, read.FormFields.Single(f => f.Name == "dueDay").Kind,
                "the plain date must stay a plain date - that is the whole point of the second kind");
        }
    }
}
#pragma warning restore MSTEST0032
