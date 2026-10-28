using System;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Ein in der Admin-Maske angelegter Mandant bekommt einen eigenen Schluessel (Leitfaden §95).
    /// </summary>
    [TestClass]
    public class TenantPasswordAssignmentTest
    {
        [TestMethod]
        public void NewTenant_GetsItsOwnKey()
        {
            var first = new Tenant();
            var second = new Tenant();

            TenantPasswordAssignment.EnsureForNewTenant(first, true);
            TenantPasswordAssignment.EnsureForNewTenant(second, true);

            Assert.IsFalse(string.IsNullOrEmpty(first.TenantPassword));
            Assert.AreEqual(32, Convert.FromBase64String(first.TenantPassword).Length);
            Assert.AreNotEqual(first.TenantPassword, second.TenantPassword, "Every tenant needs a key of its own.");
        }

        [TestMethod]
        public void KeyFromUpdateTenant_IsKept()
        {
            var tenant = new Tenant { TenantPassword = "set-by-host" };

            TenantPasswordAssignment.EnsureForNewTenant(tenant, true);

            Assert.AreEqual("set-by-host", tenant.TenantPassword);
        }

        [TestMethod]
        public void SwitchedOff_NoKeyIsSet()
        {
            var tenant = new Tenant();

            TenantPasswordAssignment.EnsureForNewTenant(tenant, false);

            Assert.IsNull(tenant.TenantPassword);
        }
    }
}
