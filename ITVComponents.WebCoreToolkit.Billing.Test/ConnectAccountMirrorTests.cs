using System;
using System.Collections.Generic;
using System.Text.Json;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using V2 = Stripe.V2;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Nagelt die Uebersetzung eines v2-Connect-Kontos in den lokalen Spiegel fest - vor allem die eine Frage,
    /// an der Geld haengt: <b>darf dieser Laden kassieren?</b>
    /// </summary>
    /// <remarks>
    /// Der Anlass ist der Umstieg von <c>Accounts v1</c> auf v2. v1 beantwortete das mit einem Flag, v2 mit dem
    /// Status einer Faehigkeit, und der kennt vier Werte: <c>active</c>, <c>pending</c>, <c>restricted</c>,
    /// <c>unsupported</c>. Nur der erste ist ein Ja. Wer <c>pending</c> fuer "gleich soweit" oder
    /// <c>restricted</c> fuer "eingeschraenkt, aber laeuft" haelt, laesst einen Laden Geld annehmen, fuer das
    /// der Anbieter ihn nicht freigegeben hat - und das faellt erst bei der ersten Zahlung auf.
    /// <para>
    /// Kein Netz, keine Datenbank: die Abbildung ist eine reine Funktion, und genau deshalb steht sie in einer
    /// eigenen Klasse statt als statisches Mitglied des Dienstes.
    /// </para>
    /// </remarks>
    [TestClass]
    public class ConnectAccountMirrorTests
    {
        // --- Die Frage, an der Geld haengt ------------------------------------------------------

        [TestMethod]
        public void OnlyAnActiveCapabilityLetsAShopTakeMoney()
        {
            var account = Mirror();

            ConnectAccountMirror.Apply(account, Remote(cardPayments: "active"));

            Assert.IsTrue(account.ChargesEnabled);
            Assert.AreEqual("active", account.CardPaymentsStatus);
        }

        [DataTestMethod]
        [DataRow("pending")]
        [DataRow("restricted")]
        [DataRow("unsupported")]
        [DataRow(null)]
        public void EveryOtherCapabilityStatusIsANo(string status)
        {
            var account = Mirror();

            ConnectAccountMirror.Apply(account, Remote(cardPayments: status));

            Assert.IsFalse(account.ChargesEnabled,
                $"'{status ?? "null"}' ist nicht 'active' und darf deshalb nicht zum Kassieren fuehren - " +
                "die drei Nicht-Ja lesen sich fuer einen Menschen verschieden, fuer das Tor sind sie gleich.");
            Assert.AreEqual(status, account.CardPaymentsStatus,
                "der rohe Status wird trotzdem gespiegelt: die Maske soll 'wir pruefen' von " +
                "'wir brauchen etwas von Ihnen' unterscheiden koennen.");
        }

        [TestMethod]
        public void ADeactivatedConfigurationIsANoRegardlessOfItsCapability()
        {
            var account = Mirror();

            // Der Anbieter kann eine Konfiguration abschalten, ohne die Faehigkeiten darin zurueckzusetzen.
            ConnectAccountMirror.Apply(account, Remote(cardPayments: "active", merchantApplied: false));

            Assert.IsFalse(account.ChargesEnabled,
                "eine abgeschaltete Merchant-Konfiguration schlaegt jede Faehigkeit, die darunter noch " +
                "'active' sagt.");
        }

        [TestMethod]
        public void PayoutsAreJudgedSeparatelyFromCharges()
        {
            var account = Mirror();

            ConnectAccountMirror.Apply(account, Remote(cardPayments: "active", payouts: "pending"));

            Assert.IsTrue(account.ChargesEnabled, "kassieren geht.");
            Assert.IsFalse(account.PayoutsEnabled, "ausgezahlt wird noch nichts.");
            Assert.AreEqual("pending", account.PayoutsStatus);
        }

        // --- Was v2 nicht mehr liefert und abgeleitet wird ---------------------------------------

        [TestMethod]
        public void DetailsAreSubmittedWhenNothingWaitsOnTheTenantAnyMore()
        {
            var account = Mirror();

            // Der Anbieter prueft noch selbst - vom Mandanten will niemand mehr etwas.
            ConnectAccountMirror.Apply(account, Remote(cardPayments: "pending",
                entries: new[] { Entry("stripe", "currently_due", "document.verification") }));

            Assert.IsTrue(account.DetailsSubmitted,
                "'Angaben vollstaendig' heisst in v2: keine Anforderung wartet mehr auf den MANDANTEN. " +
                "Was der Anbieter noch selbst prueft, geht ihn nichts mehr an.");
        }

        [TestMethod]
        public void DetailsAreNotSubmittedWhileTheTenantStillOwesSomething()
        {
            var account = Mirror();

            ConnectAccountMirror.Apply(account, Remote(cardPayments: "pending",
                entries: new[] { Entry("user", "currently_due", "individual.id_number") }));

            Assert.IsFalse(account.DetailsSubmitted);
        }

        [TestMethod]
        public void TheBlockingReasonComesFromTheCapabilityThatBlocks()
        {
            var account = Mirror();

            ConnectAccountMirror.Apply(account, Remote(cardPayments: "restricted",
                cardPaymentsReason: "verification_document_required"));

            Assert.AreEqual("verification_document_required", account.DisabledReason,
                "v2 nennt den Grund je Faehigkeit; gespiegelt wird der, der das Kassieren verhindert.");
        }

        [TestMethod]
        public void ChargesComeBeforePayoutsWhenBothAreBlocked()
        {
            var account = Mirror();

            ConnectAccountMirror.Apply(account, Remote(cardPayments: "restricted",
                cardPaymentsReason: "card_problem", payouts: "restricted", payoutsReason: "payout_problem"));

            Assert.AreEqual("card_problem", account.DisabledReason,
                "ohne Kartenzahlung gibt es gar keinen Verkauf - ein Auszahlungsproblem obendrauf ist die " +
                "kleinere der beiden Sorgen und wuerde die groessere verdecken.");
        }

        [TestMethod]
        public void AWorkingAccountHasNoBlockingReason()
        {
            var account = Mirror();

            ConnectAccountMirror.Apply(account, Remote(cardPayments: "active", payouts: "active"));

            Assert.IsNull(account.DisabledReason);
        }

        // --- Die Anforderungen behalten die Form, die die Masken lesen ---------------------------

        [TestMethod]
        public void RequirementsKeepTheBucketsTheViewsAlreadyRead()
        {
            var account = Mirror();

            ConnectAccountMirror.Apply(account, Remote(cardPayments: "pending", entries: new[]
            {
                Entry("user", "currently_due", "individual.id_number"),
                Entry("user", "past_due", "individual.address"),
                Entry("user", "eventually_due", "individual.verification"),
                Entry("stripe", "currently_due", "document.check")
            }));

            using var doc = JsonDocument.Parse(account.RequirementsJson);
            var root = doc.RootElement;

            // v2 liefert EINE flache Liste; die vier Faecher entstehen hier wieder - der Deadline-Status je
            // Eintrag traegt exakt die drei v1-Namen, und "in Pruefung" ist, was beim Anbieter liegt.
            Assert.AreEqual("individual.id_number", Single(root, "currentlyDue"));
            Assert.AreEqual("individual.address", Single(root, "pastDue"));
            Assert.AreEqual("individual.verification", Single(root, "eventuallyDue"));
            Assert.AreEqual("document.check", Single(root, "pendingVerification"));
        }

        [TestMethod]
        public void WithoutRequirementsNothingIsStored()
        {
            var account = Mirror();
            account.RequirementsJson = "{\"currentlyDue\":[\"von frueher\"]}";

            ConnectAccountMirror.Apply(account, Remote(cardPayments: "active"));

            Assert.IsNull(account.RequirementsJson,
                "sonst stuenden erledigte Anforderungen fuer immer in der Maske.");
        }

        [TestMethod]
        public void TheDeadlineIsTheMomentNotTheStatus()
        {
            var account = Mirror();
            var due = new DateTime(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc);

            ConnectAccountMirror.Apply(account, Remote(cardPayments: "pending", deadline: due,
                entries: new[] { Entry("user", "currently_due", "individual.id_number") }));

            Assert.AreEqual(due, account.RequirementsDeadline,
                "die Frist haengt am Konto, nicht am einzelnen Eintrag - der sagt nur, in welches Fach er faellt.");
        }

        // --- Identitaet ------------------------------------------------------------------------

        [TestMethod]
        public void CountryCurrencyAndDashboardComeFromTheirNewPlaces()
        {
            var account = Mirror();

            var remote = Remote(cardPayments: "active");
            remote.Identity = new V2.Core.AccountIdentity { Country = "ch" };
            remote.Defaults = new V2.Core.AccountDefaults { Currency = "chf" };
            remote.Dashboard = "express";

            ConnectAccountMirror.Apply(account, remote);

            Assert.AreEqual("CH", account.Country, "Laendercodes werden gross gespiegelt.");
            Assert.AreEqual("CHF", account.DefaultCurrency);
            Assert.AreEqual("express", account.DashboardType);
        }

        [TestMethod]
        public void WhatTheProviderDoesNotSayDoesNotOverwriteWhatIsKnown()
        {
            var account = Mirror();
            account.Country = "CH";
            account.DefaultCurrency = "CHF";

            // Eine Antwort ohne Identity/Defaults - etwa weil das Include fehlte.
            ConnectAccountMirror.Apply(account, Remote(cardPayments: "active"));

            Assert.AreEqual("CH", account.Country,
                "ein fehlendes Feld ist keine Aussage; es darf den bekannten Wert nicht loeschen.");
            Assert.AreEqual("CHF", account.DefaultCurrency);
        }

        // --- Testgeruest ------------------------------------------------------------------------

        private static TenantPaymentAccount Mirror()
            => new() { TenantId = 7, ProviderAccountId = "acct_test", Created = DateTime.UtcNow };

        /// <summary>Ein v2-Konto, wie es der Anbieter zurueckgibt - nur so weit gefuellt, wie der Fall braucht.</summary>
        private static V2.Core.Account Remote(string cardPayments = null, string cardPaymentsReason = null,
            string payouts = null, string payoutsReason = null, bool merchantApplied = true,
            bool recipientApplied = true, DateTime? deadline = null,
            IEnumerable<V2.Core.AccountRequirementsEntry> entries = null)
        {
            var account = new V2.Core.Account
            {
                Id = "acct_test",
                Configuration = new V2.Core.AccountConfiguration
                {
                    Merchant = new V2.Core.AccountConfigurationMerchant
                    {
                        Applied = merchantApplied,
                        Capabilities = new V2.Core.AccountConfigurationMerchantCapabilities
                        {
                            CardPayments = new V2.Core.AccountConfigurationMerchantCapabilitiesCardPayments
                            {
                                Status = cardPayments,
                                StatusDetails = Details<V2.Core.AccountConfigurationMerchantCapabilitiesCardPaymentsStatusDetail>(cardPaymentsReason)
                            }
                        }
                    },
                    Recipient = new V2.Core.AccountConfigurationRecipient
                    {
                        Applied = recipientApplied,
                        Capabilities = new V2.Core.AccountConfigurationRecipientCapabilities
                        {
                            StripeBalance = new V2.Core.AccountConfigurationRecipientCapabilitiesStripeBalance
                            {
                                Payouts = new V2.Core.AccountConfigurationRecipientCapabilitiesStripeBalancePayouts
                                {
                                    Status = payouts,
                                    StatusDetails = Details<V2.Core.AccountConfigurationRecipientCapabilitiesStripeBalancePayoutsStatusDetail>(payoutsReason)
                                }
                            }
                        }
                    }
                }
            };

            var list = new List<V2.Core.AccountRequirementsEntry>(entries ?? Array.Empty<V2.Core.AccountRequirementsEntry>());
            if (list.Count != 0 || deadline != null)
            {
                account.Requirements = new V2.Core.AccountRequirements
                {
                    Entries = list,
                    Summary = deadline == null
                        ? null
                        : new V2.Core.AccountRequirementsSummary
                        {
                            MinimumDeadline = new V2.Core.AccountRequirementsSummaryMinimumDeadline { Time = deadline }
                        }
                };
            }

            return account;
        }

        private static List<T> Details<T>(string code) where T : class, new()
        {
            if (code == null)
            {
                return null;
            }

            var detail = new T();
            typeof(T).GetProperty("Code")!.SetValue(detail, code);
            return new List<T> { detail };
        }

        private static V2.Core.AccountRequirementsEntry Entry(string awaitingActionFrom, string deadlineStatus,
            string description)
            => new()
            {
                AwaitingActionFrom = awaitingActionFrom,
                Description = description,
                MinimumDeadline = new V2.Core.AccountRequirementsEntryMinimumDeadline { Status = deadlineStatus }
            };

        private static string Single(JsonElement root, string bucket)
        {
            var array = root.GetProperty(bucket);
            Assert.AreEqual(1, array.GetArrayLength(), $"Fach '{bucket}' sollte genau einen Eintrag haben.");
            return array[0].GetString();
        }
    }
}
