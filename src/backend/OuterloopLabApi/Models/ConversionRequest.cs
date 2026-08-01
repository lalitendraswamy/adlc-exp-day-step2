namespace OuterloopLabApi.Models;

public sealed record ConversionRequest(decimal Amount, string FromCurrency, string ToCurrency);
