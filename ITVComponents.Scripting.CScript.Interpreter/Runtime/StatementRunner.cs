using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Interpreter.Ast;

namespace ITVComponents.Scripting.CScript.Interpreter.Runtime
{
    /// <summary>
    /// Treibt eine Anweisungsfolge Schritt fuer Schritt voran.
    /// </summary>
    /// <remarks>
    /// Bewusst ein expliziter Frame-Stack statt CLR-Rekursion: nur wenn der Ausfuehrungszustand
    /// als Datenstruktur vorliegt, laesst sich ein Script zwischen zwei Anweisungen anhalten,
    /// inspizieren und weiterlaufen lassen. Ausdruecke werden weiterhin rekursiv ausgewertet -
    /// sie sind aus Sicht des Debuggers atomar.
    ///
    /// Die TransducerMachine des StateMachine-Projekts wurde hierfuer bewusst nicht erweitert:
    /// sie ist eine flache FSM, hier braucht es einen Kellerautomaten; ausserdem ist sie
    /// durchgaengig async, waehrend ein Debugger deterministisch synchron laufen muss.
    /// </remarks>
    public sealed class StatementRunner
    {
        private readonly List<Frame> frames = new List<Frame>();

        /// <summary>
        /// Ein Eintrag des Ausfuehrungsstapels: eine Anweisungsfolge und die Position darin.
        /// </summary>
        private sealed class Frame
        {
            public Frame(IReadOnlyList<IStatementNode> statements, bool ownsScope)
            {
                Statements = statements;
                OwnsScope = ownsScope;
            }

            public IReadOnlyList<IStatementNode> Statements { get; }

            /// <summary>Gibt an, ob dieser Frame beim Verlassen einen inneren Scope schliessen muss.</summary>
            public bool OwnsScope { get; }

            public int Index { get; set; }

            public bool AtEnd => Index >= Statements.Count;
        }

        /// <summary>
        /// Gibt an, ob noch Anweisungen ausstehen.
        /// </summary>
        public bool IsRunning => frames.Count != 0;

        /// <summary>
        /// Die aktuelle Verschachtelungstiefe. Der Debugger nutzt sie fuer "Schritt ueber"
        /// gegenueber "Schritt hinein".
        /// </summary>
        public int Depth => frames.Count;

        /// <summary>
        /// Die Anweisung, die der naechste <see cref="Step"/> ausfuehren wird, oder null,
        /// wenn nichts mehr aussteht.
        /// </summary>
        public IStatementNode Current
        {
            get
            {
                for (int i = frames.Count - 1; i >= 0; i--)
                {
                    if (!frames[i].AtEnd)
                    {
                        return frames[i].Statements[frames[i].Index];
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Legt eine Anweisungsfolge zur Ausfuehrung auf den Stapel.
        /// </summary>
        /// <param name="statements">die auszufuehrenden Anweisungen</param>
        /// <param name="ownsScope">
        /// ob beim Verlassen ein innerer Scope zu schliessen ist. Der Aufrufer hat ihn dann
        /// vorher geoeffnet.
        /// </param>
        public void Push(IReadOnlyList<IStatementNode> statements, bool ownsScope)
        {
            if (statements == null)
            {
                throw new ArgumentNullException(nameof(statements));
            }

            frames.Add(new Frame(statements, ownsScope));
        }

        /// <summary>
        /// Fuehrt genau eine Anweisung aus.
        /// </summary>
        /// <param name="context">der Ausfuehrungskontext</param>
        /// <returns>
        /// das Ergebnis der ausgefuehrten Anweisung. Ein abruptes Ergebnis raeumt die Frames
        /// bis zu der Stelle ab, die es behandelt.
        /// </returns>
        public Completion Step(ExecutionContext context)
        {
            UnwindFinishedFrames(context);
            if (frames.Count == 0)
            {
                return Completion.Normal;
            }

            Frame frame = frames[frames.Count - 1];
            IStatementNode statement = frame.Statements[frame.Index];
            frame.Index++;

            IExecutionObserver observer = context.Observer;
            observer?.BeforeStatement(statement, context);
            Completion completion = statement.Execute(context);
            observer?.AfterStatement(statement, context, completion);

            if (completion.IsAbrupt)
            {
                // Der Frame kann das Ergebnis nicht behandeln - er wird verlassen und das
                // Ergebnis nach aussen gereicht. Wer es behandelt (Schleife, Funktion, try),
                // entscheidet der aufrufende Knoten.
                PopFrame(context);
            }

            return completion;
        }

        /// <summary>
        /// Fuehrt aus, bis nichts mehr aussteht oder eine Anweisung abrupt endet.
        /// </summary>
        /// <param name="context">der Ausfuehrungskontext</param>
        /// <returns>das erste abrupte Ergebnis oder <see cref="Completion.Normal"/></returns>
        public Completion RunToCompletion(ExecutionContext context)
        {
            while (true)
            {
                UnwindFinishedFrames(context);
                if (frames.Count == 0)
                {
                    return Completion.Normal;
                }

                Completion completion = Step(context);
                if (completion.IsAbrupt)
                {
                    return completion;
                }
            }
        }

        private void UnwindFinishedFrames(ExecutionContext context)
        {
            while (frames.Count != 0 && frames[frames.Count - 1].AtEnd)
            {
                PopFrame(context);
            }
        }

        private void PopFrame(ExecutionContext context)
        {
            Frame frame = frames[frames.Count - 1];
            frames.RemoveAt(frames.Count - 1);
            if (frame.OwnsScope)
            {
                context.Variables.CollapseScope();
            }
        }
    }
}
