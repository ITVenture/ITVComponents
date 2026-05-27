namespace ITVComponents.GenericService.WebService.Configurators
{
    public interface IAuthInit
    {
        /// <summary>
        /// Registers an authentication service on this init instance
        /// </summary>
        /// <param name="provider">the config-provider that will inject configs at the appropriate point in time</param>
        void RegisterAuthenticationService(IAuthenticationConfigProvider provider);
    }
}
