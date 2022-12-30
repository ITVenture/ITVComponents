using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Options
{
    [SettingName("IdentityMail")]
    public class IdentityMailSettings
    {
        public string SenderAddress { get; set; }

        public string SenderDisplayName { get; set; }
        public string EmailHost { get; set; }
        public int EmailPort { get; set; }
        public string SenderUserName { get; set; }
        public string SenderPassword { get; set; }
        public bool UseSsl { get; set; }
    }
}
