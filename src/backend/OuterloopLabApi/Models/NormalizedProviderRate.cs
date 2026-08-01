namespace OuterloopLabApi.Models;

public sealed record NormalizedProviderRate(
    decimal Rate,
    string ProviderDateMarker,
    string ProviderSourceMetadata
);
