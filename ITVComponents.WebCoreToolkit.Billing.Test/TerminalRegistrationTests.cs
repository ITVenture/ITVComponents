using System;
using System.Collections.Generic;
using System.Reflection;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Baut die Terminal-Verdrahtung so, wie ein Host sie baut — mit <c>ValidateOnBuild</c>.
    /// </summary>
    /// <remarks>
    /// BUG-PRE256: <c>AddAgentTerminals</c> registrierte einen Dienst, der ein <see cref="PaymentsRuntime"/>
    /// aus dem DI verlangte, das niemand eintrug. Aufgefallen ist das erst beim ersten Host, der den
    /// Agent-Weg wirklich gestartet hat.
    /// </remarks>
    [TestClass]
    public class TerminalRegistrationTests
    {
        [TestMethod]
        public void AddAgentTerminals_BuildsWithValidation()
        {
            var services = new ServiceCollection();
            // Was ein Host ohnehin mitbringt: Kontext, Einstellungen, den Verbinder aus AgentServices.
            services.AddSingleton<IDbContextFactory<PaymentsTestContext>>(
                new PaymentsTestContextFactory(Guid.NewGuid().ToString()));
            services.AddSingleton<IGlobalSettings<TenantPaymentsOptions>>(new StaticSettings());
            services.AddSingleton(DispatchProxy.Create<IAgentConnector, UnusedProxy>());

            services.AddAgentTerminals<PaymentsTestContext>();

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            using var scope = provider.CreateScope();

            Assert.IsNotNull(scope.ServiceProvider.GetRequiredService<AgentTerminalPaymentService<PaymentsTestContext>>());
            var catalog = scope.ServiceProvider.GetRequiredService<ITerminalProviderCatalog>();
            CollectionAssert.Contains(new List<string>(ProviderKeys(catalog)), AgentTerminalPaymentService<PaymentsTestContext>.Key,
                "Der Agent-Weg muss im Katalog stehen, sonst bietet der Assistent ihn nicht an.");
        }

        private static IEnumerable<string> ProviderKeys(ITerminalProviderCatalog catalog)
        {
            foreach (var provider in catalog.GetProviders())
            {
                yield return provider.Key;
            }
        }

        /// <summary>Wird beim Bauen nur gehalten, nie gerufen.</summary>
        public class UnusedProxy : DispatchProxy
        {
            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
                => throw new NotSupportedException($"{targetMethod?.Name} is not expected to be called while wiring up.");
        }

        private sealed class StaticSettings : IGlobalSettings<TenantPaymentsOptions>
        {
            public TenantPaymentsOptions Value { get; } = new() { Enabled = true };

            public TenantPaymentsOptions ValueOrDefault => Value;

            public TenantPaymentsOptions GetValue(string explicitSettingName) => Value;

            public TenantPaymentsOptions GetValueOrDefault(string explicitSettingName) => Value;
        }
    }
}
