using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared
{
    /// <summary>
    /// Der strategie-neutrale Ausschnitt des Onboarding-Kontexts, der die Zustimmungs-Nachweise fuehrt.
    /// Beide Onboarding-Kontexte (flach und hierarchisch) erweitern ihn, weil eine Zustimmung mit der
    /// Mandanten-Strategie nichts zu tun hat.
    /// </summary>
    public interface IOnboardingConsentContext
    {
        DbSet<ConsentRecord> ConsentRecords { get; set; }
    }
}
