using System;
using System.Collections.Generic;
using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms the second axis of a shared asset: not "may this visitor enter", but "may THIS object go
    /// out to them". The decisive property is that a forgotten confirmation holds the answer back — that
    /// is what turns the object binding from a convention into a guarantee.
    /// </summary>
    [TestClass]
    public class SharedAssetGuardTests
    {
        [TestMethod]
        public void Without_An_Asset_Nothing_Is_Held_Back()
        {
            var ctx = NewContext(asset: null);

            Assert.IsFalse(ctx.MustHoldBack);
            Assert.IsTrue(ctx.Require("orderId", 4711), "a page outside a share must not have to work differently");
        }

        [TestMethod]
        public void A_Template_Without_Arguments_Behaves_As_Before()
        {
            var ctx = NewContext(Asset(AssetArgumentEnforcement.None));

            Assert.IsFalse(ctx.MustHoldBack);
            Assert.IsTrue(ctx.Confirmed);
        }

        [TestMethod]
        public void Confirming_The_Right_Object_Opens_The_Gate()
        {
            var ctx = NewContext(Asset(AssetArgumentEnforcement.Confirmed, ("orderId", "4711")));

            Assert.IsTrue(ctx.MustHoldBack, "nothing confirmed yet");
            Assert.IsTrue(ctx.Require("orderId", 4711));
            Assert.IsFalse(ctx.MustHoldBack);
        }

        [TestMethod]
        public void Confirming_A_Foreign_Object_Refuses_And_Stays_Refused()
        {
            var ctx = NewContext(Asset(AssetArgumentEnforcement.Confirmed, ("orderId", "4711")));

            Assert.IsFalse(ctx.Require("orderId", 4712));
            Assert.IsTrue(ctx.Denied);

            // Der spaetere richtige Wert darf den abgelehnten Zugriff nicht weisswaschen.
            Assert.IsTrue(ctx.Require("orderId", 4711));
            Assert.IsTrue(ctx.Denied);
            Assert.IsTrue(ctx.MustHoldBack);
        }

        [TestMethod]
        public void A_Forgotten_Confirmation_Holds_The_Answer_Back()
        {
            // Der eigentliche Zweck des Riegels: der vergessene Aufruf faellt als leere Seite auf und
            // nicht als stilles Loch.
            var ctx = NewContext(Asset(AssetArgumentEnforcement.Confirmed, ("orderId", "4711")));

            Assert.IsTrue(ctx.MustHoldBack);
            Assert.IsFalse(ctx.Confirmed);
        }

        [TestMethod]
        public void Without_Enforcement_A_Forgotten_Confirmation_Passes()
        {
            // Das ist der Grund, warum die Vorgabe fuer Vorlagen MIT Argumenten mindestens Confirmed sein
            // muss: None laesst genau das durch.
            var ctx = NewContext(Asset(AssetArgumentEnforcement.None, ("orderId", "4711")));

            Assert.IsFalse(ctx.MustHoldBack);
        }

        [TestMethod]
        public void Partly_Confirmed_Is_Not_Confirmed()
        {
            var ctx = NewContext(Asset(AssetArgumentEnforcement.Confirmed, ("orderId", "4711"), ("stage", "2")));

            Assert.IsTrue(ctx.Require("orderId", 4711));
            Assert.IsTrue(ctx.MustHoldBack, "the second required argument is still open");

            Assert.IsTrue(ctx.Require("stage", 2));
            Assert.IsFalse(ctx.MustHoldBack);
        }

        [TestMethod]
        public void Several_Arguments_At_Once_Report_Every_Failure()
        {
            var ctx = NewContext(Asset(AssetArgumentEnforcement.Confirmed, ("orderId", "4711"), ("stage", "2")));

            var ok = ctx.Require(new Dictionary<string, object> { ["orderId"] = 4712, ["stage"] = 3 });

            Assert.IsFalse(ok);
            Assert.IsTrue(ctx.Denied);
        }

        [TestMethod]
        public void Strict_Lets_A_Confirmation_Expire_With_The_Operation()
        {
            // Der Unterschied der beiden Grade, und er zaehlt im Circuit: dort lebt der Kontext laenger
            // als ein Vorgang.
            var ctx = NewContext(Asset(AssetArgumentEnforcement.Strict, ("orderId", "4711")));
            Assert.IsTrue(ctx.Require("orderId", 4711));
            Assert.IsFalse(ctx.MustHoldBack);

            ctx.ResetConfirmation();

            Assert.IsTrue(ctx.MustHoldBack, "the next operation has to confirm again");
        }

        [TestMethod]
        public void Confirmed_Keeps_The_Confirmation_Across_Operations()
        {
            var ctx = NewContext(Asset(AssetArgumentEnforcement.Confirmed, ("orderId", "4711")));
            Assert.IsTrue(ctx.Require("orderId", 4711));

            ctx.ResetConfirmation();

            Assert.IsFalse(ctx.MustHoldBack);
        }

        [TestMethod]
        public void A_Resolver_Lifts_A_Sub_Object_To_The_Shared_Level()
        {
            // Der Datei-Endpunkt kennt eine Datei, die Freigabe einen Auftrag. Ob das zusammengehoert,
            // weiss nur der Host - einmal je Argumenttyp, nicht je Endpunkt.
            var asset = Asset(AssetArgumentEnforcement.Confirmed, ("orderId", "4711"));
            asset.Arguments = new[] { new AssetArgumentDeclaration("orderId", AssetArgumentType.Int, true, "order-of") };
            var ctx = NewContext(asset, new StubResolver("order-of", "positionId", 815, 4711));

            Assert.IsTrue(ctx.Require("positionId", 815));
            Assert.IsFalse(ctx.MustHoldBack);
        }

        [TestMethod]
        public void A_Resolver_That_Lands_On_A_Foreign_Object_Refuses()
        {
            var asset = Asset(AssetArgumentEnforcement.Confirmed, ("orderId", "4711"));
            asset.Arguments = new[] { new AssetArgumentDeclaration("orderId", AssetArgumentType.Int, true, "order-of") };
            var ctx = NewContext(asset, new StubResolver("order-of", "positionId", 816, 4712));

            Assert.IsFalse(ctx.Require("positionId", 816));
            Assert.IsTrue(ctx.Denied);
        }

        [TestMethod]
        public void An_Argument_Nobody_Knows_Neither_Confirms_Nor_Refuses()
        {
            var ctx = NewContext(Asset(AssetArgumentEnforcement.Confirmed, ("orderId", "4711")));

            Assert.IsFalse(ctx.Require("somethingElse", 1), "still not confirmed, so still held back");
            Assert.IsFalse(ctx.Denied, "but it is not a violation either - it is simply not this level");

            Assert.IsTrue(ctx.Require("orderId", 4711));
            Assert.IsFalse(ctx.MustHoldBack);
        }

        private static AssetInfo Asset(AssetArgumentEnforcement enforcement,
            params (string Name, string Value)[] values)
        {
            var declarations = new List<AssetArgumentDeclaration>();
            var input = new Dictionary<string, string>();
            foreach (var value in values)
            {
                declarations.Add(new AssetArgumentDeclaration(value.Name, AssetArgumentType.Int));
                input[value.Name] = value.Value;
            }

            Assert.IsTrue(AssetArgumentValues.TryCreate(declarations, input, out var parsed, out var error), error);
            return new AssetInfo
            {
                AssetKey = "abc",
                UserScopeName = "TenantA",
                Arguments = declarations.ToArray(),
                Values = parsed,
                Enforcement = enforcement
            };
        }

        private static SharedAssetContext NewContext(AssetInfo? asset, params IAssetArgumentResolver[] resolvers)
        {
            var httpContext = new DefaultHttpContext();
            if (asset != null)
            {
                httpContext.Items[Global.SharedAssetKeyItemKey] = asset.AssetKey;
                httpContext.Items[Global.SharedAssetSegmentItemKey] = SharedAssetPath.BuildSegment(asset.AssetKey);
            }

            var services = new StubServices(asset, resolvers);
            return new SharedAssetContext(new HttpContextAccessor { HttpContext = httpContext },
                new StubContextUser(services), Microsoft.Extensions.Options.Options.Create(new SharedAssetPathOptions()), services,
                NullLogger<SharedAssetContext>.Instance);
        }

        private sealed class StubResolver : IAssetArgumentResolver
        {
            private readonly string source;
            private readonly object sourceValue;
            private readonly object target;

            public StubResolver(string key, string source, object sourceValue, object target)
            {
                Key = key;
                this.source = source;
                this.sourceValue = sourceValue;
                this.target = target;
            }

            public string Key { get; }

            public bool TryResolve(string targetArgument, string sourceArgument, object value, out object resolved)
            {
                resolved = target;
                return string.Equals(sourceArgument, source, StringComparison.OrdinalIgnoreCase)
                       && Equals(value, sourceValue);
            }
        }

        private sealed class StubAdapter : ISharedAssetAdapter
        {
            private readonly AssetInfo? asset;
            public StubAdapter(AssetInfo? asset) => this.asset = asset;

            public AssetInfo GetAssetInfo(string assetKey, ClaimsPrincipal requestor, bool asOwner = false) => asset!;
            public bool VerifyRequestLocation(string requestPath, string assetKey, string userScope, ClaimsPrincipal requestor) => true;
            public AssetTemplateInfo[] GetEligibleShares(string requestPath) => Array.Empty<AssetTemplateInfo>();
            public AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title) => null!;
            public AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title,
                IDictionary<string, string> argumentValues, string recipientLabel, out string error)
            {
                error = null!;
                return null!;
            }

            public bool UpdateSharedAsset(FullAssetInfo updateInfo) => false;
            public bool DeleteSharedAsset(FullAssetInfo assetInfo) => false;
            public string CreateAnonymousLink(AssetInfo info, HttpContext context) => string.Empty;
            public string CreateLink(AssetInfo info, HttpContext context) => string.Empty;
            public string CreateLink(AssetInfo info, string origin) => string.Empty;
            public string CreateAnonymousLink(AssetInfo info, string origin) => string.Empty;

            public SharedAssetListItem[] ListSharedAssets(string search, int skip, int take, out int total)
            {
                total = 0;
                return Array.Empty<SharedAssetListItem>();
            }

            public bool RotateAnonymousToken(string assetKey) => false;
            public FullAssetInfo FindAnonymousAsset(string assetKey) => null!;
            void ISharedAssetAdapter.SetImpersonationOff() { }
            void ISharedAssetAdapter.SetImpersonationOn() { }
        }

        private sealed class StubServices : IServiceProvider
        {
            private readonly StubAdapter adapter;
            private readonly IAssetArgumentResolver[] resolvers;

            public StubServices(AssetInfo? asset, IAssetArgumentResolver[] resolvers)
            {
                adapter = new StubAdapter(asset);
                this.resolvers = resolvers;
            }

            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(ISharedAssetAdapter)) return adapter;
                if (serviceType == typeof(IEnumerable<IAssetArgumentResolver>)) return resolvers;
                return null;
            }
        }

        private sealed class StubContextUser : IContextUserProvider
        {
            public StubContextUser(IServiceProvider services) => Services = services;
            public ClaimsPrincipal User { get; } = new(new ClaimsIdentity("test"));
            public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>();
            public string RequestPath => "/";
            public IServiceProvider Services { get; }
        }
    }
}
