using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.Billing.Terminals.WalleeLti;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Die Brücke zwischen einer Eingabemaske und dem, was ein Gerät liest.
    /// </summary>
    /// <remarks>
    /// Eine Maske liefert Text — auch für Zahlen und Schalter. Wer ihn unbesehen speichert, merkt das
    /// nicht beim Ausfüllen, sondern beim ersten Kassiervorgang, und sucht den Fehler dann überall,
    /// nur nicht in der Konfigurationsmaske.
    /// </remarks>
    [TestClass]
    public class TerminalSettingsTests
    {
        [TestMethod]
        public void Compose_WritesEachValueInTheShapeItIsReadIn()
        {
            var json = TerminalSettings.Compose((Fields, new Dictionary<string, string?>
            {
                ["host"] = "192.168.10.44",
                ["port"] = "50000",
                ["suppressdynamiccurrencyconversion"] = "true"
            }));

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Assert.AreEqual(JsonValueKind.String, root.GetProperty("host").ValueKind);
            Assert.AreEqual(JsonValueKind.Number, root.GetProperty("port").ValueKind,
                "A port written as a string is the error nobody traces back to the form.");
            Assert.AreEqual(JsonValueKind.True, root.GetProperty("suppressdynamiccurrencyconversion").ValueKind);
        }

        [TestMethod]
        public async Task Compose_ProducesSomethingTheDeviceCanActuallyRead()
        {
            // Der Test, der wirklich zaehlt: die Maske beschreibt sich selbst, und was dabei herauskommt,
            // muss dieselbe Klasse wieder einlesen koennen. Beide Seiten stammen aus derselben Quelle -
            // wenn eine driftet, faellt es hier auf und nicht an der Kasse.
            var device = new WalleeLtiTerminalDevice();
            var fields = await device.DescribeSettingsAsync();

            var json = TerminalSettings.Compose((fields, new Dictionary<string, string?>
            {
                ["host"] = "192.168.10.44",
                ["posid"] = "2001"
            }));

            var options = JsonSerializer.Deserialize<WalleeLtiTerminalOptions>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.IsNotNull(options);
            Assert.AreEqual("192.168.10.44", options.Host);
            Assert.AreEqual("2001", options.PosId);
            Assert.AreEqual(50000, options.Port, "The default of a field the user left alone must survive.");
            Assert.AreEqual(180, options.TransactionTimeoutSeconds);
        }

        [TestMethod]
        public void Compose_LeavesEmptyValuesOutInsteadOfWritingBlanks()
        {
            // Ein leerer Text ist NICHT dasselbe wie "nicht gesetzt": er ueberschreibt eine Vorbelegung.
            var json = TerminalSettings.Compose((Fields, new Dictionary<string, string?>
            {
                ["host"] = "10.0.0.5",
                ["posid"] = "   "
            }));

            using var document = JsonDocument.Parse(json);
            Assert.IsFalse(document.RootElement.TryGetProperty("posid", out _));
        }

        [TestMethod]
        public void Compose_LetsTheSecondSectionWinOverTheFirst()
        {
            // Die zwei Schritte des Assistenten: was das Geraet sagt, gilt gegenueber dem, was die
            // Web-Seite vorgeschlagen hat.
            var json = TerminalSettings.Compose(
                (Fields, new Dictionary<string, string?> { ["host"] = "10.0.0.5" }),
                (Fields, new Dictionary<string, string?> { ["host"] = "10.0.0.9" }));

            using var document = JsonDocument.Parse(json);
            Assert.AreEqual("10.0.0.9", document.RootElement.GetProperty("host").GetString());
        }

        [TestMethod]
        public void SettingKinds_StayAlignedWithTheFormTheyAreRenderedWith()
        {
            // Die Maske im Blazor-Toolkit rendert gegen DeclaredFieldKind, und die Werte werden
            // numerisch abgebildet. Waeren die Reihenfolgen verschieden, wuerde aus einer Auswahlliste
            // still ein Datumsfeld - sichtbar erst in der fertigen Maske.
            string[] expected =
                ["Text", "MultilineText", "Number", "Boolean", "Date", "Choice", "DateTime"];
            CollectionAssert.AreEqual(expected, Enum.GetNames<TerminalSettingKind>(),
                "Add new kinds at the END, here and in DeclaredFieldKind.");
        }

        [TestMethod]
        public async Task Administration_RefusesToTouchAnotherTenantsTerminal()
        {
            var factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            var admin = new TerminalAdministration<PaymentsTestContext>(factory);

            var mine = await admin.SaveAsync(new TerminalDefinition
            {
                TenantId = 1,
                Provider = "agent",
                ProviderTerminalId = "A999",
                DisplayName = "Kasse 1"
            });

            await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => admin.SaveAsync(new TerminalDefinition
            {
                TerminalId = mine,
                TenantId = 2,
                Provider = "agent",
                ProviderTerminalId = "A999",
                DisplayName = "geklaut"
            }));

            await Assert.ThrowsExactlyAsync<TenantPaymentException>(
                () => admin.SetEnabledAsync(2, mine, false));
        }

        [TestMethod]
        public async Task Administration_SavesAndReadsBackWhatTheWizardCollected()
        {
            var factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            var admin = new TerminalAdministration<PaymentsTestContext>(factory);

            var id = await admin.SaveAsync(new TerminalDefinition
            {
                TenantId = 1,
                Provider = "Agent",
                ProviderTerminalId = " A999 ",
                Route = " Kasse 1 ",
                DisplayName = " Theke vorne ",
                ConfigurationJson = """{"host":"10.0.0.5"}"""
            });

            var stored = (await admin.GetAsync(1)).Single();
            Assert.AreEqual(id, stored.TerminalId);
            Assert.AreEqual("agent", stored.Provider, "The provider key is stored lower-case, like everywhere else.");
            Assert.AreEqual("A999", stored.ProviderTerminalId);
            Assert.AreEqual("Kasse 1", stored.Route);
            Assert.AreEqual("Theke vorne", stored.DisplayName);
            Assert.IsTrue(stored.Enabled);

            await admin.SetEnabledAsync(1, id, false);
            Assert.IsFalse((await admin.GetAsync(1)).Single().Enabled);
        }

        [TestMethod]
        public async Task Administration_InsistsOnWhatATerminalCannotDoWithout()
        {
            var factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            var admin = new TerminalAdministration<PaymentsTestContext>(factory);

            await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => admin.SaveAsync(new TerminalDefinition
            {
                TenantId = 1,
                Provider = "agent",
                // ohne Geraetekennung
                DisplayName = "Kasse 1"
            }));
        }

        /// <summary>Ein kleiner Satz Felder, der für die reinen Umwandlungs-Tests genügt.</summary>
        private static readonly IReadOnlyList<TerminalSettingDescriptor> Fields =
        [
            new() { Name = "host", Kind = TerminalSettingKind.Text, Required = true },
            new() { Name = "port", Kind = TerminalSettingKind.Number, DefaultValue = "50000" },
            new() { Name = "posid", Kind = TerminalSettingKind.Text },
            new() { Name = "suppressdynamiccurrencyconversion", Kind = TerminalSettingKind.Boolean }
        ];
    }
}
