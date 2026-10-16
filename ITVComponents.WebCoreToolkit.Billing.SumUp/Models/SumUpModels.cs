using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITVComponents.WebCoreToolkit.Billing.SumUp.Models
{
    // Die Formen folgen der OpenAPI-Beschreibung von SumUp (sumup-openapi, Stand Oktober 2026). Alles, was nicht
    // Pflicht ist, ist nullable: noch lief nichts davon gegen die Sandbox, und ein fehlendes Feld soll ein leeres
    // Feld ergeben und keinen Lesefehler mitten in einer Zahlung.

    /// <summary>Ein Betrag in der KLEINSTEN Einheit - nur im Geraete-Checkout so; die Transaktions-API rechnet in der Haupteinheit.</summary>
    public sealed class SumUpMoney
    {
        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("minor_unit")]
        public int MinorUnit { get; set; }

        [JsonPropertyName("value")]
        public long Value { get; set; }
    }

    /// <summary>Die Partner-Angaben an einem Geraete-Checkout.</summary>
    public sealed class SumUpAffiliate
    {
        [JsonPropertyName("app_id")]
        public string AppId { get; set; } = string.Empty;

        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        /// <summary>Pflicht im Affiliate-Block - und unsere Vorgangskennung, nach der sich spaeter suchen laesst.</summary>
        [JsonPropertyName("foreign_transaction_id")]
        public string ForeignTransactionId { get; set; } = string.Empty;

        [JsonPropertyName("tags")]
        public Dictionary<string, string>? Tags { get; set; }
    }

    /// <summary><c>POST /v0.1/merchants/{merchant_code}/readers/{reader_id}/checkout</c></summary>
    public sealed class SumUpReaderCheckoutRequest
    {
        [JsonPropertyName("total_amount")]
        public SumUpMoney TotalAmount { get; set; } = new();

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("return_url")]
        public string? ReturnUrl { get; set; }

        [JsonPropertyName("tip_rates")]
        public decimal[]? TipRates { get; set; }

        [JsonPropertyName("affiliate")]
        public SumUpAffiliate? Affiliate { get; set; }
    }

    public sealed class SumUpReaderCheckoutCreated
    {
        [JsonPropertyName("checkout_id")]
        public string? CheckoutId { get; set; }

        [JsonPropertyName("client_transaction_id")]
        public string ClientTransactionId { get; set; } = string.Empty;
    }

    /// <summary><c>GET /v0.1/merchants/{merchant_code}/readers/{reader_id}/checkout/{checkout_id}</c></summary>
    public sealed class SumUpReaderCheckout
    {
        [JsonPropertyName("checkout_id")]
        public string? CheckoutId { get; set; }

        [JsonPropertyName("client_transaction_id")]
        public string? ClientTransactionId { get; set; }

        /// <summary><c>pending</c>, <c>successful</c>, <c>failed</c>, <c>cancelled</c>.</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("payment_failure_reason")]
        public string? PaymentFailureReason { get; set; }

        /// <summary>Danach bricht SumUp den Checkout von selbst ab.</summary>
        [JsonPropertyName("valid_until")]
        public DateTime? ValidUntil { get; set; }

        [JsonPropertyName("total_amount")]
        public SumUpMoney? TotalAmount { get; set; }
    }

    /// <summary>Die Huelle <c>{ "data": … }</c> der Geraete-API.</summary>
    public sealed class SumUpData<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    /// <summary><c>POST /v0.1/merchants/{merchant_code}/readers</c></summary>
    public sealed class SumUpCreateReaderRequest
    {
        [JsonPropertyName("pairing_code")]
        public string PairingCode { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public sealed class SumUpReader
    {
        /// <summary><c>rdr_…</c> - die Kennung, unter der das Geraet danach angesprochen wird.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary><c>unknown</c>, <c>processing</c>, <c>paired</c>, <c>expired</c>.</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("device")]
        public SumUpReaderDevice? Device { get; set; }
    }

    public sealed class SumUpReaderDevice
    {
        [JsonPropertyName("identifier")]
        public string? Identifier { get; set; }

        /// <summary><c>solo</c> oder <c>virtual-solo</c>.</summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }

    /// <summary><c>GET /v0.1/merchants/{merchant_code}/readers/{reader_id}/status</c> (Solo ab 3.3.39.0)</summary>
    public sealed class SumUpReaderStatus
    {
        /// <summary><c>ONLINE</c> oder <c>OFFLINE</c>.</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary><c>IDLE</c>, <c>SELECTING_TIP</c>, <c>WAITING_FOR_CARD</c>, …</summary>
        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("battery_level")]
        public double? BatteryLevel { get; set; }

        [JsonPropertyName("firmware_version")]
        public string? FirmwareVersion { get; set; }
    }

    /// <summary><c>GET /v2.1/merchants/{merchant_code}/transactions?…</c> - Betraege hier in der HAUPTeinheit.</summary>
    public sealed class SumUpTransaction
    {
        /// <summary>Die Kennung, gegen die erstattet wird und unter der der Beleg abgerufen wird.</summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("transaction_code")]
        public string? TransactionCode { get; set; }

        [JsonPropertyName("client_transaction_id")]
        public string? ClientTransactionId { get; set; }

        [JsonPropertyName("foreign_transaction_id")]
        public string? ForeignTransactionId { get; set; }

        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; }

        [JsonPropertyName("tip_amount")]
        public decimal? TipAmount { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        /// <summary><c>SUCCESSFUL</c>, <c>CANCELLED</c>, <c>FAILED</c>, <c>PENDING</c>, <c>REFUNDED</c>.</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime? Timestamp { get; set; }

        [JsonPropertyName("card")]
        public SumUpCard? Card { get; set; }
    }

    public sealed class SumUpCard
    {
        [JsonPropertyName("last_4_digits")]
        public string? Last4Digits { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    /// <summary><c>GET /v1.1/receipts/{transaction_id}?mid={merchant_code}</c> - strukturiert, kein fertiger Beleg.</summary>
    public sealed class SumUpReceipt
    {
        [JsonPropertyName("transaction_data")]
        public SumUpReceiptTransaction? TransactionData { get; set; }

        [JsonPropertyName("acquirer_data")]
        public SumUpAcquirerData? AcquirerData { get; set; }

        [JsonPropertyName("emv_data")]
        public JsonElement? EmvData { get; set; }
    }

    public sealed class SumUpReceiptTransaction
    {
        [JsonPropertyName("receipt_no")]
        public string? ReceiptNo { get; set; }

        [JsonPropertyName("verification_method")]
        public string? VerificationMethod { get; set; }

        [JsonPropertyName("entry_mode")]
        public string? EntryMode { get; set; }

        [JsonPropertyName("card")]
        public SumUpCard? Card { get; set; }
    }

    public sealed class SumUpAcquirerData
    {
        [JsonPropertyName("tid")]
        public string? TerminalId { get; set; }

        [JsonPropertyName("authorization_code")]
        public string? AuthorizationCode { get; set; }
    }

    /// <summary>Die Meldung an <c>return_url</c> (<c>solo.transaction.updated</c>). Ohne Signatur - siehe Handler.</summary>
    public sealed class SumUpWebhookNotice
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("event_type")]
        public string? EventType { get; set; }

        [JsonPropertyName("payload")]
        public SumUpWebhookPayload? Payload { get; set; }
    }

    public sealed class SumUpWebhookPayload
    {
        [JsonPropertyName("client_transaction_id")]
        public string? ClientTransactionId { get; set; }

        [JsonPropertyName("merchant_code")]
        public string? MerchantCode { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
