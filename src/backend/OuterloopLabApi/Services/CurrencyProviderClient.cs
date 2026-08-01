using System.Net;
using System.Text.Json;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Services;

public interface ICurrencyProviderClient
{
    Task<NormalizedProviderRate> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken ct);
}

public sealed class CurrencyProviderClient(ICurrencyProviderAdapter adapter, HttpClient httpClient) : ICurrencyProviderClient
{
    private readonly ICurrencyProviderAdapter _adapter = adapter;
    private readonly HttpClient _httpClient = httpClient;

    public async Task<NormalizedProviderRate> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken ct)
    {
        // Frankfurter endpoint is exposed under api.frankfurter.dev. If the configured base is frankfurter.dev,
        // transparently map it to the API host.
        var baseAddress = _httpClient.BaseAddress?.ToString() ?? string.Empty;
        var normalizedBase = baseAddress.TrimEnd('/');
        if (normalizedBase.EndsWith("frankfurter.dev", StringComparison.OrdinalIgnoreCase) && !normalizedBase.EndsWith("api.frankfurter.dev", StringComparison.OrdinalIgnoreCase))
        {
            normalizedBase = normalizedBase.Replace("frankfurter.dev", "api.frankfurter.dev", StringComparison.OrdinalIgnoreCase);
        }

        var url = $"{normalizedBase}/v2/rate/{fromCurrency}/{toCurrency}?expand=providers";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
        {
            throw new CurrencyProviderUnavailableException("Currency rate provider is currently unavailable.", ex);
        }

        if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity))
        {
            throw new CurrencyProviderUnavailableException("Currency rate provider is currently unavailable.");
        }

        if (!response.IsSuccessStatusCode)
        {
            // Do not leak provider exceptions.
            throw new CurrencyProviderUnavailableException("Currency rate provider is currently unavailable.");
        }

        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return _adapter.Normalize(doc.RootElement, fromCurrency, toCurrency);
        }
        catch (JsonException ex)
        {
            throw new CurrencyProviderUnavailableException("Currency rate provider response could not be parsed.", ex);
        }
    }
}

public interface ICurrencyProviderAdapter
{
    NormalizedProviderRate Normalize(JsonElement root, string fromCurrency, string toCurrency);
}

public sealed class CurrencyProviderUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
