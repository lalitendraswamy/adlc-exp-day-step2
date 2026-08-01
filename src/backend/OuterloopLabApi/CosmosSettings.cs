namespace OuterloopLabApi;

public sealed class CosmosSettings
{
    public string CosmosDbUri { get; init; } = default!;
    public string DatabaseName { get; init; } = default!;
    public string ContainerName { get; init; } = default!;

    public string CosmosDbAccountName { get; init; } = default!;
    public string CosmosDbResourceGroup { get; init; } = default!;
    public string CosmosDbRegion { get; init; } = default!;

    public string ManagedIdentityClientId { get; init; } = default!;

    public static CosmosSettings FromEnvironment()
    {
        static string Required(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Missing required environment variable: {key}");
            return value;
        }

        // Constraint: bind values exclusively from the exact keys defined in docs\CONTAINER_ENVIRONMENT_VARIABLES.md.
        return new CosmosSettings
        {
            CosmosDbUri = Required("COSMOS_DB_URI"),
            DatabaseName = Required("COSMOS_DB_DATABASE"),
            ContainerName = Required("COSMOS_DB_CONTAINER"),
            CosmosDbAccountName = Required("COSMOS_DB_ACCOUNT_NAME"),
            CosmosDbResourceGroup = Required("COSMOS_DB_RESOURCE_GROUP"),
            CosmosDbRegion = Required("COSMOS_DB_REGION"),
            ManagedIdentityClientId = Required("AZURE_MANAGED_IDENTITY_CLIENT_ID")
        };
    }
}
