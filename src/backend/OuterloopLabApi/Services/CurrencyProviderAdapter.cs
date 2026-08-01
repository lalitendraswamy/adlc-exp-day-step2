using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Services;

public sealed class CurrencyProviderAdapter : ICurrencyProviderAdapter
{
    public NormalizedProviderRate Normalize(JsonElement root, string fromCurrency, string toCurrency)
        => NormalizeFrankfurterLike(root, fromCurrency, toCurrency);

    public NormalizedProviderRate NormalizeFrankfurterLike(JsonElement root, string fromCurrency, string toCurrency)
    {
        // Frankfurter v2 returns { date, base, quote, rate, ... }
        // Constraint: do not hardcode full third-party schemas; extract via flexible property lookup.
        if (!TryExtractDecimalByName(root, new[] { "rate", "conversion_rate", "conversionRate" }, out var rate))
        {
            throw new CurrencyProviderParseException("Could not extract a rate from provider response.");
        }

        var providerDate = TryExtractStringByName(root, new[] { "date", "providerDate", "asOf" })
            ?? throw new CurrencyProviderParseException("Could not extract a provider date marker from provider response.");

        string providerSourceMetadata = ExtractProvidersMetadata(root);

        return new NormalizedProviderRate(rate, providerDate, providerSourceMetadata);
    }

    private static bool TryExtractDecimalByName(JsonElement root, string[] names, out decimal value)
    {
        foreach (var name in names)
        {
            if (TryGetPropertyCaseInsensitive(root, name, out var el) && el.ValueKind == JsonValueKind.Number)
            {
                if (el.TryGetDecimal(out value)) return true;
                if (el.TryGetDouble(out var d))
                {
                    value = Convert.ToDecimal(d);
                    return true;
                }
            }

            if (TryGetPropertyCaseInsensitive(root, name, out el) && el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (decimal.TryParse(s, out value)) return true;
            }
        }

        // Fallback: if provider uses { rates: { ... } } or { conversion_rates: { ... } },
        // attempt to pull a first numeric "rate" leaf.
        foreach (var containerName in new[] { "rates", "conversion_rates" })
        {
            if (!TryGetPropertyCaseInsensitive(root, containerName, out var container)) continue;
            if (container.ValueKind != JsonValueKind.Object) continue;
            foreach (var prop in container.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Number && prop.Value.ValueKind != JsonValueKind.String)
                    continue;
                if (TryExtractDecimalLoose(prop.Value, out value)) return true;
            }
        }

        value = 0m;
        return false;
    }

    private static bool TryExtractDecimalLoose(JsonElement el, out decimal value)
    {
        if (el.ValueKind == JsonValueKind.Number)
        {
            if (el.TryGetDecimal(out value)) return true;
            if (el.TryGetDouble(out var d))
            {
                value = Convert.ToDecimal(d);
                return true;
            }
        }
        else if (el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString();
            if (decimal.TryParse(s, out value)) return true;
        }

        value = 0m;
        return false;
    }

    private static string? TryExtractStringByName(JsonElement root, string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetPropertyCaseInsensitive(root, name, out var el) && el.ValueKind == JsonValueKind.String)
            {
                return el.GetString();
            }
        }

        return null;
    }

    private static string ExtractProvidersMetadata(JsonElement root)
    {
        // If providers are present, normalize them. If absent, return an empty string.
        if (!TryGetPropertyCaseInsensitive(root, "providers", out var providers))
            return string.Empty;

        if (providers.ValueKind == JsonValueKind.Array)
        {
            var keys = new List<string>();
            foreach (var item in providers.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    keys.Add(item.GetString() ?? string.Empty);
                    continue;
                }

                if (item.ValueKind == JsonValueKind.Object && TryGetPropertyCaseInsensitive(item, "key", out var keyEl))
                {
                    if (keyEl.ValueKind == JsonValueKind.String)
                        keys.Add(keyEl.GetString() ?? string.Empty);
                }
            }

            return string.Join(",", keys.Where(k => !string.IsNullOrWhiteSpace(k)));
        }

        return providers.ValueKind == JsonValueKind.String ? providers.GetString() ?? string.Empty : string.Empty;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public decimal RoundHalfAwayFromZero(decimal value, int decimals)
        => Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}

public sealed class CurrencyProviderParseException(string message) : Exception(message);
