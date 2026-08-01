using System.Text.Json.Serialization;

namespace OuterloopLabApi.Models;

public sealed class ConversionAuditDocument
{
    // Cosmos DB uses "id" as the document id.
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    // Partition key.
    public string AuditId { get; set; } = default!;

    public decimal OriginalAmount { get; set; }
    public string FromCurrency { get; set; } = default!;
    public string ToCurrency { get; set; } = default!;

    public decimal Rate { get; set; }
    public decimal ConvertedAmount { get; set; }

    public string ProviderDateMarker { get; set; } = default!;
    public DateTime ExecutedAtUtc { get; set; }

    public string ProviderSourceMetadata { get; set; } = default!;
}
