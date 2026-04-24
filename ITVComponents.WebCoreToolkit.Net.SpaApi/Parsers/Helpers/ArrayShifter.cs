using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Helpers
{
    internal class ArrayShifter<T>
    {
        private readonly T[] array;

        private int currentIndex;

        public ArrayShifter(T[] array)
        {
            this.array = array;
            currentIndex = 0;
        }

        public bool Eof => currentIndex >= array.Length;

        public T Current => !Eof ? array[currentIndex] : throw new EndOfStreamException("End of Array reached!");

        public T Next => array.Length > currentIndex ? array[currentIndex] : default(T);

        public bool MoveNext() => ++currentIndex < array.Length;
    }
}
