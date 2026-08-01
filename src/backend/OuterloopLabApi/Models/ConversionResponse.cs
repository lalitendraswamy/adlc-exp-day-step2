namespace OuterloopLabApi.Models;

public sealed record ConversionResponse(
    string AuditId,
    decimal OriginalAmount,
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    string ProviderDateMarker,
    DateTime ExecutedAtUtc,
    decimal ConvertedAmount
);
