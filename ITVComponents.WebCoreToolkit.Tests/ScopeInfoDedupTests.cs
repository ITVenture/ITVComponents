using System.Linq;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.Comparers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// A tenant reachable directly and through inheritance counts as direct (DEV 4b7bb2790, §121).
    /// </summary>
    [TestClass]
    public class ScopeInfoDedupTests
    {
        [TestMethod]
        public void Direct_Access_Wins_Over_Inherited_Whatever_Comes_First()
        {
            var scopes = new[]
            {
                new ScopeInfo { ScopeName = "Shop", AccessMode = ScopeAccessMode.Inherited },
                new ScopeInfo { ScopeName = "shop", AccessMode = ScopeAccessMode.Direct },
                new ScopeInfo { ScopeName = "Other", AccessMode = ScopeAccessMode.Inherited }
            };

            var result = scopes.DistinctPreferDirectAccess().ToArray();

            Assert.AreEqual(2, result.Length, "one entry per tenant, without regard to case");
            Assert.AreEqual(ScopeAccessMode.Direct, result.Single(n => n.ScopeName.ToLower() == "shop").AccessMode);
            Assert.AreEqual(ScopeAccessMode.Inherited, result.Single(n => n.ScopeName == "Other").AccessMode);
            Assert.AreEqual(ScopeAccessMode.Direct, scopes.Reverse().DistinctPreferDirectAccess().Single(n => n.ScopeName.ToLower() == "shop").AccessMode);
        }
    }
}
