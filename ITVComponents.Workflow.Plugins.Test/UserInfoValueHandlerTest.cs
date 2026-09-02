using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.Workflow.Plugins.WebCoreToolkit;
using ITVComponents.Workflow.ValueHandles;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Plugins.Test
{
    /// <summary>
    /// Prueft den mitgelieferten Benutzer-Handler: er nimmt, was an Kennungen da ist, ergaenzt den Rest,
    /// verschmilzt die flachen Felder nach der vereinbarten Regel - und schreibt nie.
    /// </summary>
    [TestClass]
    public class UserInfoValueHandlerTest
    {
        private SqliteConnection connection;
        private DbContextOptions<TestSecurityContext> options;
        private TestContextFactory factory;

        [TestInitialize]
        public void Setup()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            options = new DbContextOptionsBuilder<TestSecurityContext>().UseSqlite(connection).Options;
            factory = new TestContextFactory(() => new TestSecurityContext(options));

            using var db = new TestSecurityContext(options);
            db.Database.EnsureCreated();

            db.Users.Add(new TestUser { UserId = 7, UserName = "mwyler", Email = "login@example.com" });
            db.Users.Add(new TestUser { UserId = 8, UserName = "lonely", Email = "lonely@example.com" });
            db.TenantUsers.Add(new TestTenantUser
            {
                TenantUserId = 70, UserId = 7, TenantId = 3, Enabled = true
            });
            db.Employees.Add(new TestEmployee
            {
                EmployeeId = 700, UserId = 7, TenantId = 3, TenantUserId = 70,
                FirstName = "Matthias", LastName = "Wyler", EMail = "work@example.com",
                InvitationStatus = TestInvitationStatus.Committed
            });
            db.UserProperties.Add(new TestUserProperty
            {
                CustomUserPropertyId = 1, UserId = 7, PropertyName = "CostCenter", Value = "4711",
                PropertyType = CustomUserPropertyType.Literal
            });

            // Der Mandanten-Eigentuemer: Benutzer und Mandanten-Zuordnung ja, Mitarbeiter NEIN - seine
            // Angaben stehen ausschliesslich im persoenlichen Rechnungsprofil.
            db.Users.Add(new TestUser { UserId = 9, UserName = "owner", Email = "owner@example.com" });
            db.TenantUsers.Add(new TestTenantUser
            {
                TenantUserId = 90, UserId = 9, TenantId = 4, Enabled = true
            });
            db.BillingProfiles.Add(new TestBillingProfile
            {
                BillingProfileId = 900, ProfileType = TestProfileType.Personal, OwnerUserId = 9,
                TenantId = 4, FirstName = "Olivia", LastName = "Owner", Email = "billing@example.com",
                PhoneNumber = "+41 31 000 00 00"
            });

            // Ein Firmenprofil desselben Eigentuemers: die Namensfelder beschreiben hier NICHT ihn.
            db.BillingProfiles.Add(new TestBillingProfile
            {
                BillingProfileId = 901, ProfileType = TestProfileType.Company, OwnerUserId = 9,
                TenantId = 5, FirstName = "Contact", LastName = "Person", CompanyName = "Acme AG",
                Email = "accounts@acme.example"
            });

            db.OddBillingProfiles.Add(new OddBillingProfile
            {
                BillingProfileId = 950, ProfileType = OddProfileType.Foundation, OwnerUserId = 9,
                FirstName = "Odd", LastName = "One"
            });

            db.TextUsers.Add(new TextUser
            {
                Id = "a3f0-9c", UserName = "identity", Email = "identity@example.com"
            });
            db.TextTenantUsers.Add(new TextTenantUser
            {
                TenantUserId = 90, UserId = "a3f0-9c", TenantId = 3, Enabled = false
            });
            db.SaveChanges();
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        // --- Die Kennungen kommen nicht alle ---------------------------------------------------------

        [TestMethod]
        public void AUserId_IsEnoughForEverything()
        {
            UserInfo info = Read(Employees(), (UserInfoValueHandler<TestUser, TestTenantUser,
                TestUserProperty>.ArgumentUserId, 7));

            Assert.IsTrue(info.Found);
            Assert.AreEqual("mwyler", info.UserName);
            Assert.AreEqual(70, info.TenantUserId);
            Assert.AreEqual(700, info.EmployeeId);
            Assert.AreEqual(3, info.TenantId);
            Assert.AreEqual(true, info.Enabled);
        }

        [TestMethod]
        public void AnEmployeeId_IsEnough()
        {
            UserInfo info = Read(Employees(), ("employeeId", 700));

            Assert.IsTrue(info.Found);
            Assert.AreEqual(7, info.UserId);
            Assert.AreEqual("mwyler", info.UserName);
            Assert.AreEqual(70, info.TenantUserId);
        }

        [TestMethod]
        public void ATenantUserId_IsEnough()
        {
            UserInfo info = Read(Employees(), ("tenantUserId", 70));

            Assert.IsTrue(info.Found);
            Assert.AreEqual(7, info.UserId);
            Assert.AreEqual(700, info.EmployeeId);
        }

        [TestMethod]
        public void AUserName_IsEnough()
        {
            UserInfo info = Read(Employees(), ("userName", "mwyler"));

            Assert.IsTrue(info.Found);
            Assert.AreEqual(7, info.UserId);
            Assert.AreEqual(700, info.EmployeeId);
        }

        [TestMethod]
        public void TheArgumentNames_AreNotCaseSensitive()
        {
            UserInfo info = Read(Employees(), ("UserID", 7));

            Assert.IsTrue(info.Found, "a designer types the name by hand - the case must not decide.");
        }

        [TestMethod]
        public void AUserWithoutTenantUserOrEmployee_IsStillFound()
        {
            UserInfo info = Read(Employees(), ("userId", 8));

            Assert.IsTrue(info.Found);
            Assert.AreEqual("lonely", info.UserName);
            Assert.IsNull(info.TenantUserId, "a user outside this tenant has no tenant assignment.");
            Assert.IsNull(info.Employee);
        }

        // --- Die Verschmelzung ----------------------------------------------------------------------

        [TestMethod]
        public void TheEmployeeBeatsTheUser_ForMailAndNames()
        {
            UserInfo info = Read(Employees(), ("userId", 7));

            Assert.AreEqual("work@example.com", info.EMail,
                "the business address is the one a process means - the login address is the fallback.");
            Assert.AreEqual("Matthias", info.FirstName);
            Assert.AreEqual("Matthias Wyler", info.DisplayName);
            Assert.AreEqual("Committed", info.InvitationStatus);
        }

        [TestMethod]
        public void WithoutAnEmployee_TheLoginAddressAndTheUserNameCarry()
        {
            UserInfo info = Read(Employees(), ("userId", 8));

            Assert.AreEqual("lonely@example.com", info.EMail);
            Assert.AreEqual("lonely", info.DisplayName);
        }

        [TestMethod]
        public void TheConcreteRecords_AreThere()
        {
            UserInfo info = Read(Employees(), ("userId", 7));

            Assert.IsInstanceOfType(info.User, typeof(TestUser));
            Assert.IsInstanceOfType(info.TenantUser, typeof(TestTenantUser));
            Assert.IsInstanceOfType(info.Employee, typeof(TestEmployee));
        }

        [TestMethod]
        public void TheCustomProperties_AreThereByName()
        {
            UserInfo info = Read(Employees(), ("userId", 7));

            Assert.AreEqual("4711", info.Properties["CostCenter"]);
            Assert.AreEqual("4711", info.Properties["costcenter"],
                "a field path should not have to guess the spelling.");
        }

        // --- Die andere Ausprägung ------------------------------------------------------------------

        [TestMethod]
        public void ATextKeyedUser_WorksWithoutAnySpecialConfiguration()
        {
            var handler = new UserInfoValueHandler<TextUser, TextTenantUser, TextUserProperty>(factory)
            {
                UniqueName = "textUsers"
            };

            UserInfo info = (UserInfo)handler.Read(Request(("userId", "a3f0-9c")));

            Assert.IsTrue(info.Found);
            Assert.AreEqual("identity", info.UserName);
            Assert.AreEqual(90, info.TenantUserId);
            Assert.AreEqual(false, info.Enabled);
        }

        // --- Was schiefgehen darf und was nicht -------------------------------------------------------

        [TestMethod]
        public void NobodyFound_Faults()
        {
            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => Read(Employees(), ("userId", 999)));

            StringAssert.Contains(ex.Message, "found nobody");
            StringAssert.Contains(ex.Message, "AllowMissing",
                "the message must say how to make this a normal state.");
        }

        [TestMethod]
        public void NobodyFound_WithAllowMissing_IsAnEmptyResult()
        {
            var handler = new EmployeeUserInfoValueHandler<TestUser, TestTenantUser, TestUserProperty,
                TestEmployee, TestBillingProfile>(factory, true) { UniqueName = "users" };

            var info = (UserInfo)handler.Read(Request(("userId", 999)));

            Assert.IsFalse(info.Found);
            Assert.IsNull(info.UserName);
        }

        [TestMethod]
        public void WithoutAnyArgument_Faults()
        {
            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => Read(Employees()));

            StringAssert.Contains(ex.Message, "whom to describe");
        }

        [TestMethod]
        public void AnEmployeeId_OnAHandlerWithoutEmployees_Faults()
        {
            var handler = new UserInfoValueHandler<TestUser, TestTenantUser, TestUserProperty>(factory)
            {
                UniqueName = "plainUsers"
            };

            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => handler.Read(Request(("employeeId", 700))));

            StringAssert.Contains(ex.Message, "without employees",
                "silently ignoring the argument would describe the wrong person.");
        }

        [TestMethod]
        public void Writing_IsRefused_WithTheReasonInTheMessage()
        {
            var handler = Employees();

            var ex = Assert.ThrowsExactly<NotSupportedException>(
                () => handler.Write(Request(("userId", 7)), new UserInfo()));

            StringAssert.Contains(ex.Message, "read-only");
            StringAssert.Contains(ex.Message, "rights",
                "the reason is the rights asymmetry - it belongs in the message, not only in a document.");
        }

        // --- Der Mandanten-Eigentuemer ---------------------------------------------------------------

        [TestMethod]
        public void TheTenantOwner_HasNoEmployee_AndIsDescribedByHisPersonalBillingProfile()
        {
            UserInfo info = Read(Employees(), ("userId", 9));

            Assert.IsTrue(info.Found);
            Assert.IsNull(info.Employee, "the owner has no employee record - that is the whole point.");
            Assert.AreEqual("Olivia", info.FirstName);
            Assert.AreEqual("Owner", info.LastName);
            Assert.AreEqual("Olivia Owner", info.DisplayName,
                "without the profile this would fall back to the user name.");
            Assert.AreEqual(900, info.BillingProfileId);
            Assert.IsNotNull(info.BillingProfile);
        }

        [TestMethod]
        public void TheOwnersPhoneNumber_ComesFromTheProfile_BecauseNothingElseCarriesOne()
        {
            UserInfo info = Read(Employees(), ("userId", 9));

            Assert.AreEqual("+41 31 000 00 00", info.PhoneNumber);
        }

        [TestMethod]
        public void TheLoginAddress_Wins_OverTheBillingAddress()
        {
            UserInfo info = Read(Employees(), ("userId", 9));

            Assert.AreEqual("owner@example.com", info.EMail,
                "the billing address may be a different one - the login address is the binding one.");
        }

        [TestMethod]
        public void ACompanyProfile_IsNeverUsedToDescribeTheOwner()
        {
            // Der Eigentuemer besitzt beide Profile; nur das persoenliche darf ihn beschreiben.
            UserInfo info = Read(Employees(), ("userId", 9));

            Assert.AreEqual(900, info.BillingProfileId);
            Assert.AreNotEqual("Contact", info.FirstName,
                "the names on a company profile describe a contact, not the owner.");
        }

        [TestMethod]
        public void AnEmployee_Wins_OverTheBillingProfile()
        {
            // Benutzer 7 hat einen Mitarbeiter - das Profil wird gar nicht erst gesucht.
            UserInfo info = Read(Employees(), ("userId", 7));

            Assert.AreEqual("Matthias", info.FirstName);
            Assert.IsNull(info.BillingProfile,
                "who has an employee record does not need the billing profile - and it must not override it.");
        }

        [TestMethod]
        public void ABillingProfileId_ResolvesTheOwnerBehindIt()
        {
            UserInfo info = Read(Employees(), ("billingProfileId", 900));

            Assert.IsTrue(info.Found);
            Assert.AreEqual(9, info.UserId);
            Assert.AreEqual("owner", info.UserName);
            Assert.AreEqual("Olivia", info.FirstName);
        }

        [TestMethod]
        public void ACompanyBillingProfileId_Faults_WithTheReasonInTheMessage()
        {
            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => Read(Employees(), ("billingProfileId", 901)));

            StringAssert.Contains(ex.Message, "Personal",
                "the message must name the condition that was not met.");
        }

        [TestMethod]
        public void ABillingProfileId_OnAHandlerWithoutProfiles_Faults()
        {
            var handler = new UserInfoValueHandler<TestUser, TestTenantUser, TestUserProperty>(factory)
            {
                UniqueName = "plainUsers"
            };

            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => handler.Read(Request(("billingProfileId", 900))));

            StringAssert.Contains(ex.Message, "without billing profiles");
        }

        [TestMethod]
        public void AnUnknownProfileType_IsNotTreatedAsPersonal()
        {
            // Ein umbenanntes oder erweitertes Enum darf nicht still zum Personenprofil werden - sonst
            // stuende der falsche Name in der Maske.
            UserInfo info = Read(OddProfiles(), ("userId", 9));

            Assert.IsNull(info.BillingProfile);
            Assert.IsNull(info.FirstName);
        }

        // --- Hilfsmittel ------------------------------------------------------------------------------

        private EmployeeUserInfoValueHandler<TestUser, TestTenantUser, TestUserProperty, TestEmployee,
                TestBillingProfile>
            Employees()
            => new EmployeeUserInfoValueHandler<TestUser, TestTenantUser, TestUserProperty, TestEmployee,
                TestBillingProfile>(factory) { UniqueName = "users" };

        /// <summary>
        /// Ein Handler auf einem Rechnungsprofil, dessen Profiltyp weder 'Personal' noch 'Company' heisst.
        /// </summary>
        private EmployeeUserInfoValueHandler<TestUser, TestTenantUser, TestUserProperty, TestEmployee,
                OddBillingProfile>
            OddProfiles()
            => new EmployeeUserInfoValueHandler<TestUser, TestTenantUser, TestUserProperty, TestEmployee,
                OddBillingProfile>(factory) { UniqueName = "users" };

        private static UserInfo Read(IWorkflowValueHandler handler, params (string Name, object Value)[] args)
            => (UserInfo)handler.Read(Request(args));

        private static ValueHandleRequest Request(params (string Name, object Value)[] args)
        {
            var arguments = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach ((string name, object value) in args)
            {
                arguments[name] = value;
            }

            return new ValueHandleRequest("users", "customer", arguments, "instance-1", "definition-1",
                "3", "token-1", "node-1");
        }
    }
}
