namespace ITVComponents.Plugins.DIIntegration
{
    public interface IOptionsProvider<T>
    {
        public T GetOptions(T existing);
    }
}
