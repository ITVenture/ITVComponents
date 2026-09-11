using System;
using System.Collections.Generic;
using System.Text;
using ITVComponents.EFRepo.DataSync;
using ITVComponents.EFRepo.DataSync.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.EFRepo.Test
{
    /// <summary>
    /// An welchen DbContext die Fremdschluessel-Aufloesung gebunden wird.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Kontext-Typ steht als VARIABLENDEKLARATION im erzeugten Skripttext
    /// (<c>XyzDbContext db = Global.Db;</c>). Zeigt er auf einen anderen Kontext als den, gegen den
    /// der Change spaeter laeuft, faellt das nicht beim Uebersetzen auf und auch nicht beim Vergleich -
    /// sondern erst beim Einspielen, je Datensatz, als uebersprungener Change mit einer Meldung ueber
    /// einen fehlgeschlagenen Ausdruck. Genau die Sorte Fehler, die man an der Quelle festnagelt.
    /// </para>
    /// <para>
    /// Geprueft wird deshalb der erzeugte Text und nicht ein Verhalten: der Text IST der Vertrag mit
    /// der Skript-Umgebung.
    /// </para>
    /// </remarks>
    [TestClass]
    public class ConfigChangeContextBindingTest
    {
        [TestMethod]
        public void ChangeContext_BindsTheAssignmentToTheGivenContext()
        {
            var handler = new TestHandler { UniqueName = "Cfg" };

            var expression = handler.Bind(typeof(TopicContext))
                .MakeLinqAssign("Network", "Networks", "RefTag");

            StringAssert.Contains(expression, "TopicContext db = Global.Db;",
                "Der Ausdruck muss gegen den Kontext der Sektion laufen, nicht gegen den des Hosts.");
            StringAssert.Contains(expression, "Entity.Network =");
            StringAssert.Contains(expression, "db.Networks");
        }

        [TestMethod]
        public void ChangeContext_BindsTheQueryToTheGivenContext()
        {
            var handler = new TestHandler { UniqueName = "Cfg" };

            var expression = handler.Bind(typeof(TopicContext))
                .MakeLinqQuery("Products", "RefTag");

            StringAssert.Contains(expression, "TopicContext db = Global.Db;");
        }

        [TestMethod]
        public void TwoSections_DoNotShareTheirContext()
        {
            var handler = new TestHandler { UniqueName = "Cfg" };

            var host = handler.Bind(typeof(HostContext)).MakeLinqQuery("Tenants", "TenantName");
            var topic = handler.Bind(typeof(TopicContext)).MakeLinqQuery("Products", "RefTag");

            StringAssert.Contains(host, "HostContext db = Global.Db;");
            StringAssert.Contains(topic, "TopicContext db = Global.Db;");
        }

        [TestMethod]
        public void ChangeContext_StillBuildsPlainDetails()
        {
            var handler = new TestHandler { UniqueName = "Cfg" };

            var detail = handler.Bind(typeof(TopicContext)).MakeDetail("Name", "Alpha", currentValue: "Beta");

            Assert.AreEqual("Name", detail.TargetProp);
            Assert.AreEqual("Alpha", detail.NewValue);
            Assert.AreEqual("Beta", detail.CurrentValue);
            Assert.IsTrue(detail.Apply);
        }

        /// <summary>Der Kontext einer Sektion mit eigenem Modell.</summary>
        private class TopicContext : DbContext
        {
        }

        /// <summary>Der Kontext des Hosts.</summary>
        private class HostContext : DbContext
        {
        }

        /// <summary>
        /// Der kleinste Handler, der sich bauen laesst - die Basis traegt die zu pruefende Mechanik.
        /// </summary>
        private sealed class TestHandler : ConfigurationHandlerBase
        {
            public IConfigChangeContext Bind(Type contextType) => CreateChangeContext(contextType);

            public override string[] PermissionsForReason(string reason) => Array.Empty<string>();

            public override object DescribeConfig(string fileType, IDictionary<string, int> filterDic, out string name)
            {
                name = null;
                return null;
            }

            public override void ApplyChanges(IEnumerable<Change> changes, StringBuilder messages,
                Action<string, Dictionary<string, object>> extendQuery = null)
            {
            }
        }
    }
}
