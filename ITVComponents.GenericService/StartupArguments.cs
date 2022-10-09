using ITVComponents.CommandLineParser;

namespace ITVComponents.GenericService
{
    public class StartupArguments
    {
        [CommandParameter(ArgumentName = "/action:", IsOptional = true, ParameterDescription = "Sets the action to perform with the service", IsHelpParameter = false)]
        public RunAction Action { get; set; }
        
        [CommandParameter(ArgumentName="/run", IsOptional=true,ParameterDescription="In combination with Install, UnInstall and Configure performs the given action immediately.", IsHelpParameter=false)]
        public bool Run{ get; set; }

        [CommandParameter(ArgumentName="/help", IsOptional=true,ParameterDescription="Shows this help message", IsHelpParameter=true)]
        public bool Help { get; set; }
    }


    public enum RunAction
    {
        None,
        Install,
        UnInstall,
        Configure,
        Debug
    }
}
