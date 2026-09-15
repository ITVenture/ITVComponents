using System;
using System.Security.Cryptography;
using System.Text;

namespace ITVComponents.Security
{
    /// <summary>
    /// Erzeugt und prueft Hashes von <b>maschinell erzeugten</b> Geheimnissen - Geraeteschluessel,
    /// API-Schluessel, Kopplungscodes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Warum es das ueberhaupt gibt:</b> das Toolkit hatte bis hierher keine Hash-Konvention.
    /// <see cref="PasswordSecurity"/> ist <i>reversible Verschluesselung</i> (DPAPI oder AES) und damit
    /// fuer ein Geheimnis, das nie zurueckgelesen werden muss, das falsche Werkzeug; der einzige echte
    /// Hash-Helfer (<c>ITVComponents.DataAccess.Helpers.HashHelper</c>) faellt auf SHA1 zurueck und dient
    /// Cache-Schluesseln.
    /// </para>
    /// <para>
    /// <b>Warum EIN Durchgang und kein PBKDF2 - das ist Absicht und keine Nachlaessigkeit.</b>
    /// Schluesselstreckung gleicht die geringe Entropie <i>gewaehlter</i> Passwoerter aus. Die Geheimnisse
    /// hier werden von <see cref="CreateSecret"/> aus 32 Zufallsbytes erzeugt und haben volle Entropie -
    /// da gibt es nichts zu erraten, was sich durch Streckung verteuern liesse. Dafuer laeuft die Pruefung
    /// bei JEDEM Aufruf eines angemeldeten Geraets: 100'000 PBKDF2-Runden waeren dort keine Haertung,
    /// sondern eine Bremse, die wir uns selbst einbauen. So halten es Anbieter von API-Schluesseln
    /// durchgehend.
    /// </para>
    /// <para>
    /// <b>Damit nicht geeignet fuer von Menschen gewaehlte Passwoerter.</b> Dafuer bleibt ASP.NET Identity
    /// zustaendig.
    /// </para>
    /// <para>
    /// <b>Format des gespeicherten Werts</b> (Base64 ueber diese Bytes), nach dem Hausmuster von
    /// <see cref="AesEncryptor"/>, das Salt und Laengen im Wert selbst mitfuehrt:
    /// </para>
    /// <code>
    /// [0]      Formatversion (1)
    /// [1]      Laenge des Salt
    /// [2..]    Salt
    /// [..]     HMAC-SHA256(Salt, Geheimnis), 32 Bytes
    /// </code>
    /// <para>
    /// Die Version steht vorn, damit sich das Verfahren spaeter wechseln laesst, ohne dass bestehende
    /// Werte unlesbar werden: <see cref="Verify"/> liest sie und entscheidet danach.
    /// </para>
    /// </remarks>
    public static class SecretHasher
    {
        private const byte CurrentVersion = 1;
        private const int SaltLength = 16;
        private const int HashLength = 32;

        /// <summary>
        /// Erzeugt ein neues Geheimnis mit voller Entropie - 32 Zufallsbytes, Base64Url-kodiert.
        /// </summary>
        /// <remarks>
        /// Base64Url, weil das Ergebnis in URLs, Kopfzeilen und Konfigurationsdateien landet und dort
        /// weder <c>+</c> noch <c>/</c> noch <c>=</c> gebrauchen kann.
        /// </remarks>
        /// <returns>das Geheimnis im Klartext - der einzige Moment, in dem es existiert</returns>
        public static string CreateSecret()
        {
            var raw = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(raw)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        /// <summary>
        /// Bildet den zu speichernden Hash eines Geheimnisses.
        /// </summary>
        /// <param name="secret">das Geheimnis im Klartext</param>
        /// <returns>der Wert, der in die Datenbank gehoert</returns>
        public static string Hash(string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                throw new ArgumentException("A secret is required.", nameof(secret));
            }

            var salt = RandomNumberGenerator.GetBytes(SaltLength);
            var hash = Compute(secret, salt);

            var blob = new byte[2 + SaltLength + HashLength];
            blob[0] = CurrentVersion;
            blob[1] = SaltLength;
            Buffer.BlockCopy(salt, 0, blob, 2, SaltLength);
            Buffer.BlockCopy(hash, 0, blob, 2 + SaltLength, HashLength);
            return Convert.ToBase64String(blob);
        }

        /// <summary>
        /// Prueft ein Geheimnis gegen einen gespeicherten Hash.
        /// </summary>
        /// <remarks>
        /// Vergleicht in <b>konstanter Zeit</b> (<see cref="CryptographicOperations.FixedTimeEquals"/>) -
        /// ein Vergleich, der beim ersten abweichenden Byte abbricht, verraet ueber die Laufzeit, wie weit
        /// ein Rateversuch gekommen ist.
        /// </remarks>
        /// <param name="secret">das vorgelegte Geheimnis</param>
        /// <param name="storedHash">der gespeicherte Wert</param>
        /// <returns>true, wenn das Geheimnis passt</returns>
        public static bool Verify(string secret, string storedHash)
        {
            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(storedHash))
            {
                return false;
            }

            byte[] blob;
            try
            {
                blob = Convert.FromBase64String(storedHash);
            }
            catch (FormatException)
            {
                // Kein gueltiger gespeicherter Wert. Nicht werfen: der Aufrufer prueft eine Anmeldung,
                // und eine kaputte Zeile in der Ablage darf ihn nicht mit einer Ausnahme beschaeftigen.
                return false;
            }

            if (blob.Length < 2 || blob[0] != CurrentVersion)
            {
                return false;
            }

            var saltLength = blob[1];
            if (blob.Length != 2 + saltLength + HashLength)
            {
                return false;
            }

            var salt = new byte[saltLength];
            Buffer.BlockCopy(blob, 2, salt, 0, saltLength);
            var expected = new byte[HashLength];
            Buffer.BlockCopy(blob, 2 + saltLength, expected, 0, HashLength);

            return CryptographicOperations.FixedTimeEquals(Compute(secret, salt), expected);
        }

        private static byte[] Compute(string secret, byte[] salt)
        {
            using var mac = new HMACSHA256(salt);
            return mac.ComputeHash(Encoding.UTF8.GetBytes(secret));
        }
    }
}
