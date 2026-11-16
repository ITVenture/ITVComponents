using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Extensibility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// The payout tab hands out its texts already translated, from ordinary resources - in every language the
    /// toolkit carries, and never as a JSON record or a bare resource key.
    /// </summary>
    [TestClass]
    public class PayoutProfileModuleTextTests
    {
        [TestMethod]
        [DataRow("en", "Payouts")]
        [DataRow("de", "Auszahlungen")]
        [DataRow("de-CH", "Auszahlungen")]
        [DataRow("fr-CH", "Virements")]
        [DataRow("it", "Bonifici")]
        [DataRow("es-ES", "Payouts")]
        public async Task Every_Text_Is_Plain_And_In_The_Readers_Language(string culture, string expectedTitle)
        {
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            try
            {
                // The texts need no database; the context is only touched by Load/Persist.
                var module = new TenantPayoutProfileModule<PaymentsTestContext>(null!);
                var ctx = new CustomInfoContext { Mode = CustomInfoMode.Edit, TenantId = 1 };

                Assert.AreEqual(expectedTitle, module.Title);

                var texts = new List<string> { module.Title };
                foreach (var field in module.GetFields(ctx))
                {
                    texts.Add(field.Label);
                    texts.Add(field.HelpText);
                    texts.AddRange(field.Choices?.Select(c => c.Label) ?? Enumerable.Empty<string>());
                }

                var badCountry = await module.ValidateAsync(new Dictionary<string, string> { [nameof(TenantPaymentProfile.Country)] = "CHE" },
                    null!, ctx, CancellationToken.None);
                var badUrl = await module.ValidateAsync(new Dictionary<string, string> { [nameof(TenantPaymentProfile.BusinessUrl)] = "shop.example.com" },
                    null!, ctx, CancellationToken.None);
                Assert.IsFalse(badCountry.Valid);
                Assert.IsFalse(badUrl.Valid);
                texts.Add(badCountry.Message);
                texts.Add(badUrl.Message);

                foreach (var text in texts)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(text), $"{culture}: an empty text");
                    Assert.IsFalse(text.StartsWith("{"), $"{culture}: a JSON record instead of a text: {text}");
                    Assert.IsFalse(text.StartsWith("Payout_"), $"{culture}: a missing resource: {text}");
                }
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        }
    }
}
