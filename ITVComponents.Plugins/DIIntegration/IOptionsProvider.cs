namespace ITVComponents.Plugins.DIIntegration
{
    public interface IOptionsProvider<T>:IPlugin
    {
        public T GetOptions(T existing);
    }
}
