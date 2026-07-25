using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Helpers;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Initialization;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Plugins.WebCoreToolkit.Test
{
    /// <summary>Eine Test-Aktivitaet, die als Plugin geladen wird.</summary>
    public class TestActivity : IActivityPlugin
    {
        public string UniqueName { get; set; }

        public event EventHandler Disposed;

        public void Execute(WorkflowActivityContext context)
        {
        }

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ein Test-Loader, der die Aktivitaet als Scoped-Plugin bereitstellt.</summary>
    public class TestActivityLoader : IDynamicLoader
    {
        private static readonly Dictionary<string, string> Scoped = new Dictionary<string, string>
        {
            { "act", "[wtatest]<ITVComponents.Workflow.Plugins.WebCoreToolkit.Test.TestActivity>" }
        };

        public string UniqueName { get; set; }

        public event EventHandler Disposed;

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);

        public IEnumerable<string> LoadDynamicAssemblies(PluginLoadType currentLoadType, bool writeAccess = true)
            => Array.Empty<string>();

        public bool HasParamsFor(string uniqueName) => false;

        public void GetGenericParams(string uniqueName, List<GenericTypeArgument> genericTypeArguments,
            Dictionary<string, object> customVariables, StringFormatProvider formatter)
        {
        }

        public bool HasScopedPlugin(string pluginName) => Scoped.ContainsKey(pluginName);

        public PluginConfigurationItem GetScopedPlugin(string pluginName)
            => Scoped.TryGetValue(pluginName, out string ctor)
                ? new PluginConfigurationItem { Name = pluginName, ConstructionString = ctor }
                : null;

        public IEnumerable<PluginConfigurationItem> GetScopedPluginNames()
            => Scoped.Select(kv => new PluginConfigurationItem { Name = kv.Key, ConstructionString = kv.Value });
    }

    /// <summary>Ein minimaler IHttpContextAccessor fuer den Test.</summary>
    public sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; }
    }

    /// <summary>Ein IPermissionScope-Test-Doppel: kein HTTP-Scope, aber SetFixedScope wirkt (Basisklasse).</summary>
    public sealed class TestPermissionScope : PermissionScopeBase
    {
        protected override string GetPermissionScopePrefix() => null;

        protected override void SetPermissionScopePrefix(string newScope, bool asTemporary)
        {
        }
    }

    /// <summary>Geteilter Sink, in den der (scoped) Fake-Helper den gesehenen Tenant schreibt.</summary>
    public sealed class TenantSink
    {
        public string LastSeenTenant { get; set; }
    }

    /// <summary>
    /// Ein IWebPluginHelper-Doppel: liefert einen echten PluginFactory-Operations-Scope und meldet den
    /// Tenant, den der (scoped) IPermissionScope beim Oeffnen trug, in den geteilten Sink - so laesst sich
    /// beweisen, dass der Host den Tenant der Instanz in seinen DI-Scope gesetzt hat.
    /// </summary>
    public sealed class FakeWebPluginHelper : IWebPluginHelper
    {
        private readonly IPermissionScope scope;
        private readonly PluginFactory backing;
        private readonly TenantSink sink;

        public FakeWebPluginHelper(IPermissionScope scope, PluginFactory backing, TenantSink sink)
        {
            this.scope = scope;
            this.backing = backing;
            this.sink = sink;
        }

        public IPluginFactory CreateOperationScope(string explicitPluginScope)
        {
            sink.LastSeenTenant = scope.PermissionPrefix;
            return backing.NewScope(new Dictionary<string, object>(), null, false);
        }

        public IPluginFactory CreateOperationScope()
        {
            sink.LastSeenTenant = scope.PermissionPrefix;
            return backing.NewScope(new Dictionary<string, object>(), null, false);
        }

        public PluginFactory GetFactory() => throw new NotSupportedException();

        public PluginFactory GetFactory(string explicitPluginScope) => throw new NotSupportedException();

        public void ResetFactory()
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Prueft den <see cref="WebToolkitActivityHost"/> und den Hintergrund-Scope-Seam: eine Instanz wird
    /// in einem DI-Scope aufgeloest, der auf ihren Tenant fixiert ist; die Aktivitaet kommt aus der
    /// tenant-spezifischen Plugin-Factory.
    /// </summary>
    [TestClass]
    public class WebToolkitActivityHostTest
    {
        private PluginFactory activityFactory;

        [TestInitialize]
        public void Setup()
        {
            activityFactory = new PluginFactory(ScopeMode.PerAsyncContext);
            activityFactory.RegisterAssembly("wtatest", typeof(TestActivity).Assembly);
            activityFactory.LoadPlugin<TestActivityLoader>("loader",
                "[wtatest]<ITVComponents.Workflow.Plugins.WebCoreToolkit.Test.TestActivityLoader>");
        }

        [TestCleanup]
        public void Cleanup() => activityFactory?.Dispose();

        private ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.AddSingleton<TenantSink>();
            services.AddScoped<IHttpContextAccessor, TestHttpContextAccessor>();
            services.AddScoped<IPermissionScope, TestPermissionScope>();
            services.AddScoped<IContextUserProvider, DefaultContextUserProvider>();
            services.AddScoped<IWebPluginHelper>(sp => new FakeWebPluginHelper(
                sp.GetRequiredService<IPermissionScope>(), activityFactory, sp.GetRequiredService<TenantSink>()));
            return services.BuildServiceProvider();
        }

        [TestMethod]
        public void PrepareEmptyContext_PinsTheTenantScope()
        {
            using ServiceProvider provider = BuildProvider();
            using IServiceScope scope = provider.CreateScope();

            scope.ServiceProvider.PrepareEmptyContext("tenantX", out _);

            Assert.AreEqual("tenantX", scope.ServiceProvider.GetRequiredService<IPermissionScope>().PermissionPrefix);
        }

        [TestMethod]
        public void PrepareBackgroundContext_SetsAuthenticatedNamedUserAndTenant()
        {
            using ServiceProvider provider = BuildProvider();
            using IServiceScope scope = provider.CreateScope();

            scope.ServiceProvider.PrepareBackgroundContext("#Toolkit#Process", "tenantX", out _);

            var user = scope.ServiceProvider.GetRequiredService<IContextUserProvider>().User;
            // Das ist exakt, was FilterAvailable prueft -> aktiviert die tenant-abhaengigen Filter.
            Assert.IsTrue(user.Identities.Any(i => i.IsAuthenticated),
                "the background user must be authenticated (that is what activates the filters).");
            Assert.AreEqual("#Toolkit#Process", user.Identity.Name);
            Assert.AreEqual("tenantX", scope.ServiceProvider.GetRequiredService<IPermissionScope>().PermissionPrefix);
        }

        [TestMethod]
        public void PrepareEmptyContext_NullScope_LeavesNoFixedTenant()
        {
            using ServiceProvider provider = BuildProvider();
            using IServiceScope scope = provider.CreateScope();

            scope.ServiceProvider.PrepareEmptyContext(null, out _);

            Assert.IsTrue(string.IsNullOrEmpty(
                scope.ServiceProvider.GetRequiredService<IPermissionScope>().PermissionPrefix));
        }

        [TestMethod]
        public void OpenScope_ResolvesActivityFromTenantFactory()
        {
            using ServiceProvider provider = BuildProvider();
            var host = new WebToolkitActivityHost(provider);
            var instance = new WorkflowInstance { TenantId = "tenantA" };

            using IActivityScope activityScope = host.OpenScope(instance);
            IWorkflowActivity activity = activityScope.Resolve("act");

            Assert.IsInstanceOfType<TestActivity>(activity);
        }

        [TestMethod]
        public void OpenScope_FixesInstanceTenantForScopedServices()
        {
            // Der Host oeffnet intern seinen eigenen DI-Scope; der (scoped) Helper meldet den dort
            // fixierten Tenant in den Singleton-Sink - Beweis, dass der Tenant der Instanz ankommt.
            using ServiceProvider provider = BuildProvider();
            var host = new WebToolkitActivityHost(provider);
            var instance = new WorkflowInstance { TenantId = "tenantA" };

            using (IActivityScope activityScope = host.OpenScope(instance))
            {
                activityScope.Resolve("act");
            }

            Assert.AreEqual("tenantA", provider.GetRequiredService<TenantSink>().LastSeenTenant);
        }

        [TestMethod]
        public void OpenScope_UnknownActivity_Throws()
        {
            using ServiceProvider provider = BuildProvider();
            var host = new WebToolkitActivityHost(provider);
            var instance = new WorkflowInstance { TenantId = "tenantA" };

            using IActivityScope activityScope = host.OpenScope(instance);
            Assert.ThrowsException<InvalidOperationException>(() => activityScope.Resolve("does-not-exist"));
        }
    }
}
