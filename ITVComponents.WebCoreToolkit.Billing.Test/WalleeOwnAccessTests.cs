using System;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Impl;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// wallee mit dem eigenen Konto eines Mandanten - was sich ohne Netz pruefen laesst.
    /// </summary>
    /// <remarks>
    /// Das SDK redet selbst mit wallee und laesst sich hier nicht umleiten. Festgenagelt wird darum die Wahl von
    /// Konto und Space und die Absagen am Mandanten-Endpunkt, bevor ueberhaupt etwas gelesen wird.
    /// </remarks>
    [TestClass]
    public class WalleeOwnAccessTests
    {
        private static readonly WalleeOptions Platform = new()
        {
            SpaceId = 100, ApplicationUserId = 1, ApplicationUserKey = "cGxhdGZvcm0ta2V5"
        };

        [TestMethod]
        public async Task OwnAccess_UsesTheTenantsSpaceAndUser()
        {
            var connection = await WalleeConnection.ForAsync(OwnSelection("4711"), Platform, null,
                new Credentials(ExternalServiceAuthenticationType.Basic, "88"), default);

            Assert.AreEqual(4711, connection.SpaceId);
            Assert.IsTrue(connection.IsOwnAccess);
        }

        [TestMethod]
        public async Task OwnAccess_NeedsAnApplicationUserAsBasicCredential()
        {
            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => WalleeConnection.ForAsync(
                OwnSelection("4711"), Platform, null, new Credentials(ExternalServiceAuthenticationType.BearerToken, null), default));

            Assert.AreEqual(PaymentErrorCodes.OwnAccessUnavailable, ex.Code);
        }

        [TestMethod]
        public async Task OwnAccess_WithoutCredentialStore_DoesNotFallBackToThePlatform()
        {
            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(
                () => WalleeConnection.ForAsync(OwnSelection("4711"), Platform, null, null, default));

            Assert.AreEqual(PaymentErrorCodes.OwnAccessUnavailable, ex.Code);
        }

        [TestMethod]
        public async Task OwnAccess_WithANonNumericSpace_IsRefused()
        {
            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => WalleeConnection.ForAsync(
                OwnSelection("not-a-space"), Platform, null, new Credentials(ExternalServiceAuthenticationType.Basic, "88"), default));

            Assert.AreEqual(PaymentErrorCodes.OwnAccessUnavailable, ex.Code);
        }

        [TestMethod]
        public async Task PlatformAccess_KeepsTheSpaceOfTheTenantOrTheSetting()
        {
            var withSpace = await WalleeConnection.ForAsync(ProviderAccessSelection.Platform(1, "wallee"), Platform, "555", null, default);
            var withoutSpace = await WalleeConnection.ForAsync(ProviderAccessSelection.Platform(1, "wallee"), Platform, null, null, default);

            Assert.AreEqual(555, withSpace.SpaceId);
            Assert.AreEqual(100, withoutSpace.SpaceId);
            Assert.IsFalse(withSpace.IsOwnAccess);
        }

        [TestMethod]
        public async Task OwnWebhook_IsRejectedWithoutSignatureEvenIfThePlatformSwitchIsOff()
        {
            // Der Plattform-Schalter VerifyWebhookSignatures=false darf den Mandanten-Endpunkt nicht oeffnen.
            IDbContextFactory<PaymentsTestContext> factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            int accessId;
            await using (var db = await factory.CreateDbContextAsync())
            {
                var access = new TenantProviderAccess
                {
                    TenantId = 1, Provider = "wallee", ConnectionName = "payments-wallee-1", ProviderAccountId = "4711",
                    Created = DateTime.UtcNow, Updated = DateTime.UtcNow
                };
                db.TenantProviderAccesses.Add(access);
                await db.SaveChangesAsync();
                accessId = access.TenantProviderAccessId;
            }

            var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory, new TenantSaleNotifier([]));
            var options = new WalleeOptions { SpaceId = 100, ApplicationUserId = 1, ApplicationUserKey = "a2V5", VerifyWebhookSignatures = false };
            var handler = new WalleeWebhookHandler<PaymentsTestContext>(sink, new StaticSettings<WalleeOptions>(options),
                new Credentials(ExternalServiceAuthenticationType.Basic, "88"));
            const string body = """{"entityId":1,"spaceId":4711,"listenerEntityTechnicalName":"Transaction","state":"FULFILL"}""";

            await Assert.ThrowsExactlyAsync<WalleeWebhookRejectedException>(() => handler.HandleOwnAsync(accessId, body, null, default));
            await Assert.ThrowsExactlyAsync<WalleeWebhookRejectedException>(() => handler.HandleOwnAsync(accessId + 1, body, null, default),
                "An access id that does not exist is refused, too.");
        }

        private static ProviderAccessSelection OwnSelection(string space) => ProviderAccessSelection.Own(new TenantProviderAccess
        {
            TenantProviderAccessId = 7, TenantId = 1, Provider = "wallee", ConnectionName = "payments-wallee-1",
            ProviderAccountId = space
        });

        private sealed class Credentials : IProviderCredentialStore
        {
            private readonly ExternalServiceAuthenticationType type;
            private readonly string? clientId;

            public Credentials(ExternalServiceAuthenticationType type, string? clientId)
            {
                this.type = type;
                this.clientId = clientId;
            }

            public Task<ProviderCredential> GetCredentialAsync(int tenantId, string connectionName, CancellationToken cancellationToken = default)
                => Task.FromResult(new ProviderCredential(type, clientId, "dGVuYW50LWtleQ=="));

            public Task SaveSecretAsync(int tenantId, string connectionName, ExternalServiceAuthenticationType authenticationType,
                string? clientId, string secret, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }
    }
}
