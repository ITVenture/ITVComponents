using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Optimization
{
    public interface IExecutor
    {
        bool CanExecute(object value, ScriptValue[] arguments);

        object Invoke(object value, ScriptValue[] arguments);

        bool CanExecute(object value, object[] arguments);

        object Invoke(object value, object[] arguments);
    }
}
