namespace ITVComponents.WebCoreToolkit.Billing.Wallee.Models
{
    /// <summary>
    /// Was wallee bei einem Zustandswechsel schickt.
    /// </summary>
    /// <remarks>
    /// <b>Hier stehen keine Geschäftsdaten.</b> wallee meldet nur, DASS sich etwas geändert hat, und
    /// erwartet, dass die Entität danach über die API gelesen wird. Einzige Ausnahme ist
    /// <see cref="State"/>: den gibt es, wenn am Listener „payload signing and state" eingeschaltet ist —
    /// und dann spart er je Meldung einen Aufruf.
    /// </remarks>
    public sealed class WalleeWebhookNotice
    {
        /// <summary>Die Kennung dieses Zustandswechsels.</summary>
        public long EventId { get; set; }

        /// <summary>Die Kennung der Entität, die sich geändert hat — die Transaktion oder die Erstattung.</summary>
        public long EntityId { get; set; }

        /// <summary>Die Kennung der Entitätsart.</summary>
        public long ListenerEntityId { get; set; }

        /// <summary>
        /// Die Art der Entität als Name: <c>Transaction</c>, <c>Refund</c>, … <b>Das Feld, das
        /// entscheidet</b>, wie die Kennung oben zu lesen ist.
        /// </summary>
        public string? ListenerEntityTechnicalName { get; set; }

        /// <summary>Der Raum, in dem die Entität liegt.</summary>
        public long SpaceId { get; set; }

        /// <summary>Die Kennung des Listeners, der gefeuert hat.</summary>
        public long WebhookListenerId { get; set; }

        /// <summary>Wann der Wechsel stattfand.</summary>
        public DateTime? Timestamp { get; set; }

        /// <summary>
        /// Der neue Zustand — nur vorhanden, wenn der Listener ihn mitschickt. Roh übernommen: die
        /// Aufzählung gehört wallee.
        /// </summary>
        public string? State { get; set; }
    }
}
