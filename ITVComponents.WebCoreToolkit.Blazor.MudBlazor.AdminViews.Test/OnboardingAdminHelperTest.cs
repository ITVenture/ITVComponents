using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.ViewModels;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Die strategie-neutralen Regeln der Onboarding-Verwaltung, die bis zur Zusammenfuehrung in
    /// <c>OnboardingAdminHelper</c> in <b>zwei</b> Handlern wortgleich standen (flach und hierarchisch).
    /// </summary>
    /// <remarks>
    /// Geprueft wird genau der Teil, der jetzt nur noch EINMAL existiert - Berechtigungs-Zuordnung,
    /// Namensfindung, Adress-Uebernahme. Vorher haette ein Test hier nur eine der beiden Fassungen
    /// getroffen und die andere in Ruhe gelassen; das war der eigentliche Mangel, nicht die Zeilenzahl.
    /// <para>
    /// Alles hier ist reine Funktion: kein DbContext, keine Dienste. Die getippten Abfragen bleiben
    /// bewusst bei den Handlern - dort haengen sie an strategie-eigenen Entitaeten und liessen sich nur
    /// mit rund neunzehn Typparametern zusammenlegen.
    /// </para></remarks>
    [TestClass]
    public class OnboardingAdminHelperTest
    {
        // --- Berechtigungen ---------------------------------------------------------------------------

        [TestMethod]
        public void WritePermission_IsTheFullEditTierPerKind()
        {
            CollectionAssert.AreEqual(OnboardingAdminPermissions.DirectRoleWrite,
                OnboardingAdminHelper.RequiredWritePermission(EmployeeRoleMappingKind.DirectRole));
            CollectionAssert.AreEqual(OnboardingAdminPermissions.PermissionSetWrite,
                OnboardingAdminHelper.RequiredWritePermission(EmployeeRoleMappingKind.PermissionSet));
            CollectionAssert.AreEqual(OnboardingAdminPermissions.DelegationWrite,
                OnboardingAdminHelper.RequiredWritePermission(EmployeeRoleMappingKind.Delegation),
                "creating or renaming a delegation needs the FULL edit tier - not the weaker assign tier.");
        }

        [TestMethod]
        public void WritePermission_OfAnUnknownKind_FallsBackToAnyWrite()
        {
            CollectionAssert.AreEqual(OnboardingAdminPermissions.RoleMappingsAnyWrite,
                OnboardingAdminHelper.RequiredWritePermission((EmployeeRoleMappingKind)999),
                "a kind added later must not silently become permission-free.");
        }

        [TestMethod]
        public void AssignPermission_IsWeakerOnlyForDelegation()
        {
            CollectionAssert.AreEqual(OnboardingAdminPermissions.DelegationAssign,
                OnboardingAdminHelper.RequiredAssignPermission(EmployeeRoleMappingKind.Delegation),
                "the weaker assign tier on a delegation role is the whole point of that kind.");
            CollectionAssert.AreEqual(OnboardingAdminPermissions.DirectRoleWrite,
                OnboardingAdminHelper.RequiredAssignPermission(EmployeeRoleMappingKind.DirectRole));
            CollectionAssert.AreEqual(OnboardingAdminPermissions.DirectRoleWrite,
                OnboardingAdminHelper.RequiredAssignPermission(EmployeeRoleMappingKind.PermissionSet),
                "everything that is not a delegation is assigned with the direct-role write authority.");
        }

        // --- Namensfindung ----------------------------------------------------------------------------

        [TestMethod]
        public async Task FreeRoleName_TakesTheWishedNameWhenItIsFree()
        {
            var asked = new List<string>();
            var name = await OnboardingAdminHelper.FindFreeRoleNameAsync("Sales", c =>
            {
                asked.Add(c);
                return Task.FromResult(false);
            });

            Assert.AreEqual("Sales", name);
            CollectionAssert.AreEqual(new[] { "Sales" }, asked,
                "a free wished name must not cause any further lookups.");
        }

        [TestMethod]
        public async Task FreeRoleName_SuffixesOnCollision()
        {
            var taken = new HashSet<string> { "Sales", "Sales_1" };
            var name = await OnboardingAdminHelper.FindFreeRoleNameAsync("Sales",
                c => Task.FromResult(taken.Contains(c)));

            Assert.AreEqual("Sales_2", name);
        }

        [TestMethod]
        public async Task FreeRoleName_UsesTheLastVariantBeforeGivingUp()
        {
            var taken = new HashSet<string> { "Sales", "Sales_1", "Sales_2", "Sales_3", "Sales_4" };
            Assert.AreEqual("Sales_5", await OnboardingAdminHelper.FindFreeRoleNameAsync("Sales",
                c => Task.FromResult(taken.Contains(c))));
        }

        [TestMethod]
        public async Task FreeRoleName_GivesUpAfterFiveVariants()
        {
            var name = await OnboardingAdminHelper.FindFreeRoleNameAsync("Sales", _ => Task.FromResult(true));

            Assert.IsNull(name,
                "with the base name and all five variants taken the save must fail - not invent a sixth.");
        }

        // --- Adressen ---------------------------------------------------------------------------------

        [TestMethod]
        public void ToInput_OfAMissingAddress_IsAnEmptyInput()
        {
            AddressInput input = OnboardingAdminHelper.ToInput<object>(null);

            Assert.IsNotNull(input, "the mask needs an object to bind to, not null.");
            Assert.IsNull(input.Name);
        }

        [TestMethod]
        public void ToInput_CarriesEveryField()
        {
            var address = new TestAddress
            {
                Name = "ITVenture",
                Addition1 = "c/o",
                Addition2 = "4th floor",
                Street = "Hauptstrasse",
                Number = "12a",
                Zip = "8000",
                City = "Zuerich"
            };

            AddressInput input = OnboardingAdminHelper.ToInput(address);

            Assert.AreEqual("ITVenture", input.Name);
            Assert.AreEqual("c/o", input.Addition1);
            Assert.AreEqual("4th floor", input.Addition2);
            Assert.AreEqual("Hauptstrasse", input.Street);
            Assert.AreEqual("12a", input.Number);
            Assert.AreEqual("8000", input.Zip);
            Assert.AreEqual("Zuerich", input.City);
        }

        [TestMethod]
        public void Fill_UsesTheFallbackNameWhenTheMaskLeavesItBlank()
        {
            var target = new TestAddress();

            OnboardingAdminHelper.Fill(target, new AddressInput { Name = "   " }, "ITVenture AG");

            Assert.AreEqual("ITVenture AG", target.Name,
                "an address without a name could not be matched to anything in the invoicing.");
        }

        [TestMethod]
        public void Fill_KeepsAGivenNameAndNeverWritesNullIntoZipOrCity()
        {
            var target = new TestAddress { Zip = "old", City = "old" };

            OnboardingAdminHelper.Fill(target, new AddressInput { Name = "Given" }, "Fallback");

            Assert.AreEqual("Given", target.Name);
            Assert.AreEqual(string.Empty, target.Zip, "Zip is not nullable in the row - empty, never null.");
            Assert.AreEqual(string.Empty, target.City, "same for City.");
        }

        /// <summary>
        /// Eine Adresszeile ohne Strategie. <see cref="AddressBase{TBillingProfile}"/> braucht seinen
        /// Typparameter nur fuer die Navigation zum Rechnungsprofil - fuer alles, was hier geprueft wird,
        /// genuegt <see cref="object"/>. Genau deshalb kommen die beiden Methoden mit EINEM Typparameter
        /// aus und nicht mit der ganzen Kette.
        /// </summary>
        private sealed class TestAddress : AddressBase<object>
        {
        }
    }
}
