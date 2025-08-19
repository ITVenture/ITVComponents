namespace ITVComponents.WebCoreToolkit.Blazor.Configuration
{
    public class StubComponentConfiguration
    {
        private Dictionary<string, Type> stubRegisters = new Dictionary<string, Type>();

        public void RegisterStubComponent<T>(string name) where T : class
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));
            }
            if (stubRegisters.ContainsKey(name))
            {
                throw new InvalidOperationException($"A stub component with the name '{name}' is already registered.");
            }
            stubRegisters[name] = typeof(T);
        }

        public Type? GetStubComponentType(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));
            }
            stubRegisters.TryGetValue(name, out var type);
            return type;
        }
    }
}
