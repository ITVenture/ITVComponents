namespace ITVComponents.Scripting.CScript.Ast.Expressions
{
    /// <summary>
    /// Die zweistelligen Rechen- und Bitoperatoren.
    /// </summary>
    /// <remarks>
    /// Entspricht BaseOperations des abgeloesten Builders, ohne dessen Wert None: ein
    /// unbekannter Operator ist beim Bauen ein Fehler und darf nicht als stiller Default
    /// in den Baum gelangen. AndAlso/OrElse fehlen ebenfalls - die kurzschliessenden
    /// Operatoren haben mit LogicalNode einen eigenen Knoten, weil sie ihre rechte Seite
    /// nicht immer auswerten.
    /// </remarks>
    public enum BinaryOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulus,
        And,
        Or,
        Xor,
        LeftShift,
        RightShift
    }
}
