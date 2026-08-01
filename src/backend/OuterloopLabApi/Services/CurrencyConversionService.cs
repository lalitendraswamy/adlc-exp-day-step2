using OuterloopLabApi.Models;
using OuterloopLabApi.Repositories;

namespace OuterloopLabApi.Services;

public interface ICurrencyConversionService
{
    Task<ConversionResponse> CreateConversionAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken ct);
    Task<ConversionResponse?> GetConversionAsync(string auditId, CancellationToken ct);
}

public sealed class CurrencyConversionService(
    ICurrencyProviderClient providerClient,
    AuditTrailRepository repository
) : ICurrencyConversionService
{
    public async Task<ConversionResponse> CreateConversionAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken ct)
    {
        var rate = await providerClient.GetRateAsync(fromCurrency, toCurrency, ct);
        // Consistent midpoint rounding rule.
        var converted = Math.Round(amount * rate.Rate, 2, MidpointRounding.AwayFromZero);

        // Exact backend execution timestamp in UTC.
        var executedAtUtc = DateTime.UtcNow;
        var auditId = Guid.NewGuid().ToString();

        var doc = new ConversionAuditDocument
        {
            Id = auditId,
            AuditId = auditId,
            OriginalAmount = amount,
            FromCurrency = fromCurrency,
            ToCurrency = toCurrency,
            Rate = rate.Rate,
            ConvertedAmount = converted,
            ProviderDateMarker = rate.ProviderDateMarker,
            ExecutedAtUtc = executedAtUtc,
            ProviderSourceMetadata = rate.ProviderSourceMetadata
        };

        await repository.CreateAsync(doc, ct);

        return ToResponse(doc);
    }

    public async Task<ConversionResponse?> GetConversionAsync(string auditId, CancellationToken ct)
    {
        var doc = await repository.GetAsync(auditId, ct);
        return doc is null ? null : ToResponse(doc);
    }

    private static ConversionResponse ToResponse(ConversionAuditDocument doc)
        => new ConversionResponse(
            doc.AuditId,
            doc.OriginalAmount,
            doc.FromCurrency,
            doc.ToCurrency,
            doc.Rate,
            doc.ProviderDateMarker,
            doc.ExecutedAtUtc,
            doc.ConvertedAmount
        );
}
