using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Runtime
{
    /// <summary>
    /// Kleine Helfer rund um ScriptValue, die mehrere Knoten teilen.
    /// </summary>
    internal static class ValueHelper
    {
        /// <summary>
        /// Prueft, ob ein Wert als Bedingung "wahr" ist.
        /// </summary>
        /// <remarks>
        /// Bewusst streng: nur ein echtes true ist wahr. Alles andere - auch eine Zahl
        /// ungleich null oder ein nicht-null Objekt - ist falsch. Das entspricht
        /// CheckBooleanTrue des ScriptVisitors; eine grosszuegigere Auslegung wuerde
        /// bestehende Scripts still anders laufen lassen.
        /// </remarks>
        public static bool IsTrue(ScriptValue value, ExecutionContext context)
        {
            object obj = value?.GetValue(null, context.Policy);
            return obj is bool b && b;
        }
    }
}
