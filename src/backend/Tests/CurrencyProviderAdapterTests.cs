using System.Text.Json;
using OuterloopLabApi.Services;
using Xunit;

namespace Tests;

public sealed class CurrencyProviderAdapterTests
{
    [Fact]
    public void Normalizes_Rate_And_Date_From_Frankfurter_Like_Shape()
    {
        var json = "{\"date\":\"2026-08-01\",\"base\":\"EUR\",\"quote\":\"USD\",\"rate\":1.1498}";
        using var doc = JsonDocument.Parse(json);

        var adapter = new CurrencyProviderAdapter();
        var rate = adapter.NormalizeFrankfurterLike(doc.RootElement, "EUR", "USD");

        Assert.Equal(1.1498m, rate.Rate);
        Assert.Equal("2026-08-01", rate.ProviderDateMarker);
    }
}
