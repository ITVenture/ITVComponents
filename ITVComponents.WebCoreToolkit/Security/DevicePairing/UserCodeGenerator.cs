using System;
using System.Security.Cryptography;
using System.Text;

namespace ITVComponents.WebCoreToolkit.Security.DevicePairing
{
    /// <summary>
    /// Erzeugt den Code, den ein Mensch abtippt.
    /// </summary>
    public static class UserCodeGenerator
    {
        /// <summary>
        /// Das Alphabet: <b>ohne verwechselbare Zeichen</b>.
        /// </summary>
        /// <remarks>
        /// Kein <c>0</c>/<c>O</c>, kein <c>1</c>/<c>I</c>/<c>L</c>, kein <c>U</c> neben <c>V</c>. Der Code
        /// wird vorgelesen und abgetippt, oft von jemandem, der nebenbei Kunden bedient - jede
        /// Verwechslung kostet einen zweiten Anlauf, und beim dritten ruft jemand an.
        /// <para>
        /// 30 Zeichen, bei acht Stellen also rund 6,6 &#215; 10^11 Moeglichkeiten. Das traegt nur zusammen
        /// mit Ablauf und Abfragebremse - fuer sich allein ist ein achtstelliger Code kein Geheimnis, und
        /// er ist auch keines: das Geheimnis ist der Geraetecode.
        /// </para>
        /// </remarks>
        private const string Alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";

        /// <summary>
        /// Erzeugt einen Benutzercode der gewuenschten Laenge, in Vierergruppen getrennt.
        /// </summary>
        /// <param name="length">Anzahl der Zeichen ohne Trenner</param>
        /// <returns>zum Beispiel <c>K7M4-9QPZ</c></returns>
        public static string Create(int length = 8)
        {
            if (length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "A user-code needs at least four characters.");
            }

            var sb = new StringBuilder(length + length / 4);
            for (var i = 0; i < length; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    sb.Append('-');
                }

                // RandomNumberGenerator und nicht Random: der Code ist zwar kein Geheimnis, aber ein
                // vorhersagbarer Code liesse sich einem Bestaetiger unterschieben.
                sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Bringt einen abgetippten Code auf die Form, in der er gespeichert ist.
        /// </summary>
        /// <remarks>
        /// Grossschreibung und Trenner weg, dann in Vierergruppen zurueck. Damit ist es gleichgueltig, ob
        /// jemand <c>k7m49qpz</c>, <c>K7M4 9QPZ</c> oder <c>K7M4-9QPZ</c> eintippt - alles davon ist
        /// derselbe Code, und keiner soll daran scheitern.
        /// </remarks>
        /// <param name="input">der eingegebene Code</param>
        /// <returns>der Code in Speicherform, oder null</returns>
        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            var raw = new StringBuilder();
            foreach (var c in input.ToUpperInvariant())
            {
                if (Alphabet.IndexOf(c) >= 0)
                {
                    raw.Append(c);
                }
            }

            if (raw.Length == 0)
            {
                return null;
            }

            var sb = new StringBuilder(raw.Length + raw.Length / 4);
            for (var i = 0; i < raw.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    sb.Append('-');
                }

                sb.Append(raw[i]);
            }

            return sb.ToString();
        }
    }
}
