using System.IO;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace ITVComponents.Scripting.CScript
{
    internal class ErrorListener : IAntlrErrorListener<IToken>
    {
        private StringBuilder bld = new StringBuilder();

        private int suspectLine = -1;

        public int SuspectLine { get { return suspectLine; } }

        /// <summary>
        /// Gets all error messages that were generated while parsing the provided expression
        /// </summary>
        /// <returns>all collected error messages</returns>
        public string GetAllErrors()
        {
            try
            {
                suspectLine = -1;
                return bld.ToString();
            }
            finally
            {
                bld.Clear();
            }
        }

        #region Implementation of IAntlrErrorListener<in IToken>

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine,
            string msg, RecognitionException e)
        {
            if (suspectLine == -1)
            {
                suspectLine = offendingSymbol.TokenSource.Line;
            }
            /*if (!(e is NoViableAltException))
            {*/
                string ems = msg;
                if (e != null)
                {
                    ems = e.Message;
                }

                bld.AppendFormat("Error at {0}/{1}: {2} ({3})\r\n", line, charPositionInLine, offendingSymbol.Text, ems);
            //}
        }

        #endregion
    }
}
