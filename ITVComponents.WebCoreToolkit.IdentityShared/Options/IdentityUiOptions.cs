using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using ITVComponents.WebCoreToolkit.IdentityShared.Services.Options;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Options
{
    public class IdentityUiOptions
    {
        public List<ManagementNavPageDefinition> ManagementPages { get; set; } = new();

        public bool UseDefaultPageSet { get; set; }
        public bool UseExternalLogins { get; set; }
        public bool UseDefaultIdentityNavigator { get; set; }
        public bool UseDefaultMailSender { get; set; }
        public bool RegisterPageHandlers { get; set; }
        public bool UseDefaultIdentityUserGuard { get; set; }
        public string IdentityUserType { get; set; }

        public bool UserNameIsEmail { get; set; } = true;
        public bool UseLocalAccounts { get; set; } = true;

        /// <summary>
        /// Ob die Anmeldung per Passkey angeboten wird. Standard: <c>false</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Muss ausdruecklich eingeschaltet werden, weil es <b>nicht ableitbar</b> ist. Der EF-Benutzer-Speicher
        /// setzt die Passkey-Methoden immer um, also meldet <c>UserManager.SupportsUserPasskey</c> auch dann
        /// <c>true</c>, wenn der DbContext die Passkey-Entitaet gar nicht abbildet - und .NET 10 schliesst
        /// sie standardmaessig aus. Ohne diesen Schalter zeigte die Kontoverwaltung also einen Abschnitt an,
        /// der beim ersten Speichern in der Datenbank scheitert.
        /// </para>
        /// <para>
        /// Wer ihn einschaltet, muss im eigenen <c>OnModelCreating</c> auch die Passkey-Entitaet abbilden -
        /// die Kontexte der Bibliothek tun das bewusst nicht.
        /// </para>
        /// </remarks>
        public bool UsePasskeys { get; set; }

        public UserRegistrationInfo RegistrationPage { get; set; } = new UserRegistrationInfo
        {
            AllowRegister = true,
            Area = "Identity",
            Action = "Index",
            Controller = "Registration",
            ControllerLink = true
        };

        public ExternalLoginConfig ExternalLoginPage { get; set; } = new ExternalLoginConfig()
        {
            UseExternalLogins = true,
            Area = "Identity",
            //Page = "Account/ExternalLogin",
            Controller="Registration",
            Action= "LoginExternal",
            PostToController = true,
        };
    }
}
