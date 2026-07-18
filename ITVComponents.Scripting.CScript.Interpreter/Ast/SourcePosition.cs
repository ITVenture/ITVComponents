using Antlr4.Runtime;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast
{
    /// <summary>
    /// Position eines Knotens im Quelltext. Wird an jedem Knoten mitgefuehrt, damit der
    /// Debugger Breakpoints setzen und Fehler lokalisieren kann.
    /// </summary>
    public readonly struct SourcePosition
    {
        /// <summary>
        /// Position fuer Knoten, die nicht aus Quelltext stammen (z.B. synthetische Knoten).
        /// </summary>
        public static readonly SourcePosition None = new SourcePosition(0, 0, 0, 0);

        public SourcePosition(int line, int column, int startIndex, int stopIndex)
        {
            Line = line;
            Column = column;
            StartIndex = startIndex;
            StopIndex = stopIndex;
        }

        /// <summary>1-basierte Zeile des ersten Tokens.</summary>
        public int Line { get; }

        /// <summary>0-basierte Spalte des ersten Tokens.</summary>
        public int Column { get; }

        /// <summary>Zeichenindex des ersten Tokens im Quelltext.</summary>
        public int StartIndex { get; }

        /// <summary>Zeichenindex des letzten Tokens im Quelltext.</summary>
        public int StopIndex { get; }

        /// <summary>
        /// Gibt an, ob diese Position auf echten Quelltext zeigt.
        /// </summary>
        public bool IsKnown => StopIndex > 0 || Line > 0;

        /// <summary>
        /// Liest die Position aus einem ANTLR-Kontext.
        /// </summary>
        public static SourcePosition FromContext(ParserRuleContext context)
        {
            if (context?.Start == null)
            {
                return None;
            }

            return new SourcePosition(context.Start.Line, context.Start.Column, context.Start.StartIndex,
                context.Stop?.StopIndex ?? context.Start.StopIndex);
        }

        public override string ToString()
        {
            return IsKnown ? $"({Line},{Column})" : "(unbekannt)";
        }
    }
}
