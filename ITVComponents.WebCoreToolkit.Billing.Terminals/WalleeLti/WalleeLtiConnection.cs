using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;

namespace ITVComponents.WebCoreToolkit.Billing.Terminals.WalleeLti
{
    /// <summary>
    /// Eine Verbindung zu einem wallee-Terminal im lokalen Netz (Local Till Interface).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Das Protokoll in drei Sätzen: TCP, die Kasse ist der Client und das Gerät der Server (Port
    /// 50000, sofern nichts anderes eingestellt ist). Jede Nachricht ist XML, <b>dem vier Bytes
    /// Längenangabe vorangehen</b> — eine 32-Bit-Zahl ohne Vorzeichen in Netzwerk-Byteordnung. Auf eine
    /// Anfrage folgen beliebig viele Benachrichtigungen und am Schluss genau eine Antwort.
    /// </para>
    /// <para>
    /// <b>Die Längenangabe ist der Grund, warum man hier nicht einfach lesen darf, bis nichts mehr
    /// kommt.</b> Ein Socket liefert eine Nachricht in Stücken oder mehrere auf einmal; ohne den
    /// Rahmen setzt man zwei Nachrichten zu einer zusammen oder wartet auf die Hälfte einer dritten.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen ein echtes Gerät gelaufen.</b> Rahmen, Namensräume und Nachrichtennamen
    /// stammen aus der Dokumentation von wallee (LTI 2.51).
    /// </para>
    /// </remarks>
    public sealed class WalleeLtiConnection : IAsyncDisposable
    {
        /// <summary>Der Namensraum der Kassen-Nachrichten.</summary>
        public static readonly XNamespace Pos = "http://www.vibbek.com/pos";

        /// <summary>Der Namensraum der Geräte-Nachrichten (Anzeige, Drucker, Karteneingabe).</summary>
        public static readonly XNamespace Device = "http://www.vibbek.com/device";

        /// <summary>
        /// Die grösste Nachricht, die angenommen wird.
        /// </summary>
        /// <remarks>
        /// Eine Obergrenze ist nötig, weil die Längenangabe aus dem Netz kommt: ohne sie liesse sich
        /// mit vier Bytes ein Speicherblock von vier Gigabyte anfordern.
        /// </remarks>
        private const int MaxMessageBytes = 4 * 1024 * 1024;

        private readonly TcpClient client;
        private readonly NetworkStream stream;

        private WalleeLtiConnection(TcpClient client)
        {
            this.client = client;
            stream = client.GetStream();
        }

        /// <summary>Öffnet eine Verbindung zum Gerät.</summary>
        public static async Task<WalleeLtiConnection> ConnectAsync(string host, int port, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var client = new TcpClient();
            try
            {
                using var timed = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timed.CancelAfter(timeout);
                await client.ConnectAsync(host, port, timed.Token);
            }
            catch (Exception ex)
            {
                client.Dispose();
                throw new TerminalDeviceException(
                    $"No connection to the payment terminal at {host}:{port}. Check that the device is switched on, on the same network and that its till interface is enabled: {ex.Message}", ex);
            }

            return new WalleeLtiConnection(client);
        }

        /// <summary>Schickt eine Nachricht.</summary>
        public async Task SendAsync(XElement message, CancellationToken cancellationToken)
        {
            var payload = Encoding.UTF8.GetBytes(message.ToString(SaveOptions.DisableFormatting));
            var frame = new byte[4 + payload.Length];
            // Ohne Vorzeichen und in Netzwerk-Byteordnung - andersherum liest das Geraet eine voellig
            // andere Laenge und wartet auf Bytes, die nie kommen.
            BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)payload.Length);
            payload.CopyTo(frame, 4);

            await stream.WriteAsync(frame, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        /// <summary>Liest die nächste Nachricht.</summary>
        public async Task<XElement> ReceiveAsync(CancellationToken cancellationToken)
        {
            var header = new byte[4];
            await ReadExactlyAsync(header, cancellationToken);
            var length = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (length == 0 || length > MaxMessageBytes)
            {
                throw new TerminalDeviceException(
                    $"The payment terminal announced a message of {length} bytes, which is outside what this connection accepts. The stream is out of step and the connection has to be re-established.");
            }

            var payload = new byte[length];
            await ReadExactlyAsync(payload, cancellationToken);

            try
            {
                return XElement.Parse(Encoding.UTF8.GetString(payload));
            }
            catch (Exception ex)
            {
                throw new TerminalDeviceException(
                    $"The payment terminal sent something that is not readable XML: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Schickt eine Anfrage und liest, bis die zugehörige Antwort kommt.
        /// </summary>
        /// <remarks>
        /// Dazwischen stehen Benachrichtigungen — Karteneingabe, Anzeige, Beleg. Sie werden an
        /// <paramref name="onNotification"/> gereicht: der Beleg steht in einer davon, und wer nur auf
        /// die Antwort wartet, wirft ihn weg.
        /// </remarks>
        /// <param name="request">die Anfrage</param>
        /// <param name="responseName">der erwartete Name der Antwort, ohne Namensraum</param>
        /// <param name="onNotification">was mit jeder Zwischenmeldung geschieht</param>
        /// <param name="cancellationToken">bricht das Warten ab</param>
        public async Task<XElement> ExchangeAsync(XElement request, string responseName,
            Action<XElement>? onNotification, CancellationToken cancellationToken)
        {
            await SendAsync(request, cancellationToken);

            while (true)
            {
                var message = await ReceiveAsync(cancellationToken);
                var name = message.Name.LocalName;

                if (string.Equals(name, responseName, StringComparison.Ordinal))
                {
                    return message;
                }

                if (string.Equals(name, "errorNotification", StringComparison.Ordinal))
                {
                    // Das Geraet bricht den Vorgang ab. Ob dabei schon Geld geflossen ist, sagt diese
                    // Meldung NICHT - darum wird sie oben als unklar behandelt und nicht als Fehlschlag.
                    throw new TerminalDeviceException(
                        $"The payment terminal reported an error: {message.Value.Trim()}");
                }

                onNotification?.Invoke(message);
            }
        }

        /// <summary>Liest genau so viele Bytes, wie der Puffer fasst.</summary>
        /// <remarks>
        /// <c>ReadAsync</c> darf weniger liefern als verlangt, und genau daran scheitern selbstgebaute
        /// Protokolle: bei kleinen Nachrichten fällt es nie auf, bei einem langen Beleg jedes Mal.
        /// </remarks>
        private async Task ReadExactlyAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var chunk = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
                if (chunk == 0)
                {
                    throw new TerminalDeviceException(
                        "The payment terminal closed the connection in the middle of a message. Whether the payment went through is NOT known from this.");
                }

                read += chunk;
            }
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            stream.Dispose();
            client.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
