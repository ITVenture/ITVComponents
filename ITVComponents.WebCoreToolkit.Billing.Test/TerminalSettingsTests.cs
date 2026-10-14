using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.AgentServices;
using ITVComponents.AgentServices.Abstractions;
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
        public void Compose_ProducesSomethingTheDeviceCanActuallyRead()
        {
            // Der Test, der wirklich zaehlt: das Geraet beschreibt sich selbst (ueber seinen
            // Einstellungstyp), die Maske schreibt daraus JSON, und die STRENGE Pruefung am Agenten muss es
            // annehmen. Beide Seiten stammen aus derselben Quelle - wenn eine driftet, faellt es hier auf
            // und nicht an der Kasse.
            var settingsType = DeviceSettingsReflection.SettingsTypeOf(typeof(WalleeLtiTerminalDevice));
            Assert.AreEqual(typeof(WalleeLtiTerminalOptions), settingsType);
            var fields = DeviceSettingsReflection.Describe(settingsType);

            var json = TerminalSettings.Compose((fields, new Dictionary<string, string?>
            {
                ["host"] = "192.168.10.44",
                ["posId"] = "2001"
            }));

            var errors = new List<SettingError>();
            var options = (WalleeLtiTerminalOptions)DeviceSettingsReflection.Validate(settingsType, json, errors);

            Assert.AreEqual(0, errors.Count, string.Join("; ", errors.Select(e => e.Message)));
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
            CollectionAssert.AreEqual(expected, Enum.GetNames<SettingKind>(),
                "Add new kinds at the END, here and in DeclaredFieldKind.");
        }

        /// <summary>
        /// BUG-PRE256: ein Agent-Terminal ohne Kasse und Objekt liess sich speichern, war aber nie erreichbar.
        /// </summary>
        [TestMethod]
        public void MissingRequired_NamesWhatTheRowLacks_RouteAndJsonAlike()
        {
            IReadOnlyList<TerminalSettingDescriptor> fields =
            [
                new() { Name = "route", StoredIn = TerminalSettingTarget.Route, Required = true },
                new() { Name = "objectName", Required = true },
                new() { Name = "note" }
            ];

            var empty = TerminalSettings.MissingRequired(fields, null, new Dictionary<string, string?>());
            CollectionAssert.AreEqual(new[] { "route", "objectName" }, empty.ToArray());

            var blank = TerminalSettings.MissingRequired(fields, "  ",
                new Dictionary<string, string?> { ["objectName"] = " " });
            CollectionAssert.AreEqual(new[] { "route", "objectName" }, blank.ToArray(),
                "Leerraum ist keine Angabe.");

            var complete = TerminalSettings.MissingRequired(fields, "kasse-1",
                new Dictionary<string, string?> { ["objectName"] = "Kartenterminal" });
            Assert.AreEqual(0, complete.Count, "Ein optionales Feld darf fehlen.");
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
            new() { Name = "host", Kind = SettingKind.Text, Required = true },
            new() { Name = "port", Kind = SettingKind.Number, DefaultValue = "50000" },
            new() { Name = "posid", Kind = SettingKind.Text },
            new() { Name = "suppressdynamiccurrencyconversion", Kind = SettingKind.Boolean }
        ];
    }
}
