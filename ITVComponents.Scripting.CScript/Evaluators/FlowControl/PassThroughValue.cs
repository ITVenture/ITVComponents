namespace ITVComponents.Scripting.CScript.Evaluators.FlowControl
{
    public class PassThroughValue
    {
        public PassThroughValue(PassThroughType type, object value)
        {
            Type = type;
            Value = value;
        }

        public PassThroughType Type { get; }

        public object Value { get; }
    }

    public enum PassThroughType
    {
        Break,
        Continue,
        Return,
        Exception
    }
}
