using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Formatting.Parser.Model
{
    internal class StringShifter
    {
        private string source;

        private int currentPosition = 0;

        private int skips = 0;

        public string Current => !Eof ? source.Substring(currentPosition, 1):null;

        public string Pre1 => currentPosition < source.Length - 1 ? source.Substring(currentPosition, 2) : null;

        public string Pre2 => currentPosition < source.Length - 2 ? source.Substring(currentPosition,3) : null;

        public string Pre3 => currentPosition < source.Length - 3 ? source.Substring(currentPosition, 4) : null;

        public bool Eof => currentPosition >= source.Length;

        public StringShifter(string source)
        {
            this.source = source;
        }

        public bool MoveNext(int steps=1)
        {
            if (Eof)
            {
                throw new InvalidOperationException("End of string reached!");
            }

            if (skips > 0)
            {
                skips--;
                return true;
            }

            if (steps > 0)
            {
                currentPosition += steps;
            }

            return currentPosition < source.Length;
        }

        public void Skip(int steps = 1)
        {
            skips += steps;
        }
    }
}
