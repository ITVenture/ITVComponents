using ITVComponents.CommandLineParser;

namespace ITVComponents.GenericService
{
    public class StartupArguments
    {
        [CommandParameter(ArgumentName="/install", IsOptional=true,ParameterDescription="Generates an installation.bat file", IsHelpParameter=false)]
        public bool Install { get; set; }

        [CommandParameter(ArgumentName="/uninstall", IsOptional=true,ParameterDescription="Generates an uninstallation.bat file", IsHelpParameter=false)]
        public bool UnInstall { get; set; }

        [CommandParameter(ArgumentName="/configure", IsOptional=true,ParameterDescription="Runs this service in config-mode", IsHelpParameter=false)]
        public bool Configure{ get; set; }

        [CommandParameter(ArgumentName="/Debug", IsOptional=true,ParameterDescription="Runs this service in debug-mode", IsHelpParameter=false)]
        public bool Debug{ get; set; }

        [CommandParameter(ArgumentName="/help", IsOptional=true,ParameterDescription="Shows this help message", IsHelpParameter=true)]
        public bool Help { get; set; }
    }
}
