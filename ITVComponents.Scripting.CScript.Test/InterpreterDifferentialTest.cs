using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Scripting.CScript.Test
{
    /// <summary>
    /// Vergleicht Interpreter und ScriptVisitor an zufaellig erzeugten Ausdruecken.
    /// </summary>
    /// <remarks>
    /// Handgeschriebene Tests pruefen das, woran der Autor gedacht hat. Der Interpreter soll
    /// den ScriptVisitor aber vollstaendig ersetzen - also auch dort, wo niemand hingeschaut
    /// hat. Deshalb hier erzeugte Ausdruecke: Operatorvorrang, gemischte Zahlentypen und
    /// Klammerung in Kombinationen, die von Hand niemand aufschreibt.
    ///
    /// Verglichen wird nicht nur der Wert, sondern auch der Typ - "1" und "1L" sind
    /// verschiedene Ergebnisse - und das Fehlerverhalten: wirft eine Maschine, muss die andere
    /// ebenfalls werfen. Eine Maschine, die stillschweigend ein Ergebnis liefert, wo die andere
    /// scheitert, waere die gefaehrlichere Abweichung.
    ///
    /// Der Zufall ist mit festem Startwert erzeugt: ein Fehlschlag ist reproduzierbar, und der
    /// Test wird nicht sporadisch rot.
    /// </remarks>
    [TestClass]
    public class InterpreterDifferentialTest
    {
        /// <summary>
        /// Fester Startwert - siehe Klassenkommentar.
        /// </summary>
        private const int Seed = 20260718;

        private const int ExpressionCount = 400;

        /// <summary>Operatoren, die mit beliebigen Zahlentypen umgehen.</summary>
        private static readonly string[] NumericOperators = { "+", "-", "*", "/", "%" };

        /// <summary>
        /// Operatoren, die ganze Zahlen verlangen.
        /// </summary>
        /// <remarks>
        /// Sie brauchen einen eigenen Zweig im Erzeuger: mit Gleitkomma- oder Dezimaloperanden
        /// scheitern sie in beiden Maschinen, und aus zwei Fehlschlaegen laesst sich nichts
        /// ueber Uebereinstimmung lernen. Ohne diese Trennung waren nur 30 von 400 erzeugten
        /// Ausdruecken ueberhaupt vergleichbar.
        /// </remarks>
        private static readonly string[] IntegerOperators = { "&", "|", "^", "<<", ">>" };

        private static readonly string[] Comparisons = { "<", ">", "<=", ">=", "==", "!=" };

        /// <summary>Operanden mit ganzzahligem Wert - a, b und c sind long, int und short.</summary>
        private static readonly string[] IntegerOperands = { "a", "b", "c", "1", "2", "3", "7", "2L" };

        /// <summary>Operanden mit gebrochenem Wert - d, e und f sind double, float und decimal.</summary>
        private static readonly string[] FractionalOperands = { "d", "e", "f", "3D", "4M", "5F" };

        private static Dictionary<string, object> Variables()
        {
            return new Dictionary<string, object>
            {
                { "a", 10L }, { "b", 50 }, { "c", (short)5 },
                { "d", .5 }, { "e", .75F }, { "f", 99M }
            };
        }

        [TestMethod]
        public void ArithmeticExpressionsAgree()
        {
            AssertAgreement(random => Arithmetic(random, 3));
        }

        [TestMethod]
        public void BooleanExpressionsAgree()
        {
            AssertAgreement(random => Boolean(random, 2));
        }

        [TestMethod]
        public void UnaryAndParenthesisAgree()
        {
            AssertAgreement(random =>
            {
                string inner = Arithmetic(random, 2);
                switch (random.Next(4))
                {
                    case 0:
                        return $"-({inner})";
                    case 1:
                        return $"~({inner})";
                    case 2:
                        return $"(({inner}))";
                    default:
                        return $"+({inner})";
                }
            });
        }

        /// <summary>
        /// Erzeugt Ausdruecke und vergleicht beide Maschinen. Sammelt alle Abweichungen ein,
        /// statt beim ersten Fund abzubrechen - eine einzelne Abweichung sagt wenig, ein Muster
        /// darueber, wo die Ursache liegt.
        /// </summary>
        private static void AssertAgreement(Func<Random, string> generate)
        {
            var random = new Random(Seed);
            var deviations = new List<string>();
            int compared = 0;

            for (int i = 0; i < ExpressionCount; i++)
            {
                string expression = generate(random);
                Outcome visitor = Run(() => ExpressionParser.Parse(expression, Variables()));
                Outcome interpreter = Run(() => ScriptInterpreter.Parse(expression, Variables()));

                if (visitor.Failed && interpreter.Failed)
                {
                    // Beide scheitern - die Meldungen muessen sich nicht decken.
                    continue;
                }

                compared++;
                if (visitor.Failed != interpreter.Failed)
                {
                    deviations.Add($"{expression}\n    ScriptVisitor: {visitor}\n    Interpreter:   {interpreter}");
                    continue;
                }

                if (!Equals(visitor.Value, interpreter.Value) ||
                    visitor.Value?.GetType() != interpreter.Value?.GetType())
                {
                    deviations.Add($"{expression}\n    ScriptVisitor: {visitor}\n    Interpreter:   {interpreter}");
                }
            }

            // Untergrenze, damit der Test nicht unbemerkt wertlos wird: scheitern die erzeugten
            // Ausdruecke reihenweise in beiden Maschinen, vergleicht er nichts mehr und waere
            // trotzdem gruen. Gemessen liegt die Quote bei 70 bis 100 Prozent.
            Assert.IsTrue(compared >= ExpressionCount / 2,
                $"Nur {compared} von {ExpressionCount} Ausdruecken waren in beiden Maschinen " +
                "auswertbar - der Erzeuger produziert zu viel Unsinn, der Vergleich sagt nichts mehr aus.");

            if (deviations.Count != 0)
            {
                var message = new StringBuilder();
                message.AppendLine(
                    $"{deviations.Count} von {compared} vergleichbaren Ausdruecken weichen ab:");
                foreach (string deviation in deviations.Take(10))
                {
                    message.AppendLine("  " + deviation);
                }

                Assert.Fail(message.ToString());
            }
        }

        /// <param name="integerOnly">
        /// ob der Teilbaum ganzzahlig bleiben muss. Wird von den Bit-Operatoren nach unten
        /// weitergegeben, damit deren Operanden auswertbar sind.
        /// </param>
        private static string Arithmetic(Random random, int depth, bool integerOnly = false)
        {
            if (depth == 0)
            {
                return integerOnly || random.Next(2) == 0
                    ? IntegerOperands[random.Next(IntegerOperands.Length)]
                    : FractionalOperands[random.Next(FractionalOperands.Length)];
            }

            bool useIntegerOperator = integerOnly || random.Next(2) == 0;
            string op = useIntegerOperator
                ? IntegerOperators[random.Next(IntegerOperators.Length)]
                : NumericOperators[random.Next(NumericOperators.Length)];

            string left = Arithmetic(random, depth - 1, useIntegerOperator);
            string right;
            if (op == "<<" || op == ">>")
            {
                // Der Verschiebebetrag muss klein sein, sonst ist das Ergebnis zwar definiert,
                // aber wenig aussagekraeftig.
                right = random.Next(1, 8).ToString();
            }
            else
            {
                right = Arithmetic(random, depth - 1, useIntegerOperator);
            }

            return random.Next(3) == 0 ? $"({left} {op} {right})" : $"{left} {op} {right}";
        }

        private static string Boolean(Random random, int depth)
        {
            if (depth == 0)
            {
                return $"{Arithmetic(random, 1)} {Comparisons[random.Next(Comparisons.Length)]} " +
                       $"{Arithmetic(random, 1)}";
            }

            string left = Boolean(random, depth - 1);
            string right = Boolean(random, depth - 1);
            string op = random.Next(2) == 0 ? "&&" : "||";
            return random.Next(3) == 0 ? $"({left} {op} {right})" : $"{left} {op} {right}";
        }

        private static Outcome Run(Func<object> action)
        {
            try
            {
                return new Outcome(action(), null);
            }
            catch (Exception ex)
            {
                return new Outcome(null, ex);
            }
        }

        private readonly struct Outcome
        {
            public Outcome(object value, Exception error)
            {
                Value = value;
                Error = error;
            }

            public object Value { get; }

            public Exception Error { get; }

            public bool Failed => Error != null;

            public override string ToString()
            {
                if (Failed)
                {
                    return $"{Error.GetType().Name}: {Error.Message.Split('\n')[0]}";
                }

                return Value == null ? "null" : $"{Value} ({Value.GetType().Name})";
            }
        }
    }
}
