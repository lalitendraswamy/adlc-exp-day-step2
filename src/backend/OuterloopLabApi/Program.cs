using System.Net;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.CosmosDB;
using Azure.ResourceManager.CosmosDB.Models;
using Microsoft.Azure.Cosmos;
using OuterloopLabApi.Models;
using OuterloopLabApi.Repositories;
using OuterloopLabApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Cosmos DB provisioning happens before the web app runs.
var cosmosSettings = CosmosSettings.FromEnvironment();
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    ManagedIdentityClientId = cosmosSettings.ManagedIdentityClientId
});

var partitionKeyPath = "/auditId";

// ARM provisioning is best-effort; failures should not stop the app because data-plane provisioning must still run.
try
{
    var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
    if (!string.IsNullOrWhiteSpace(subscriptionId))
    {
        var armClient = new ArmClient(credential);
        var accountResourceId = $"/subscriptions/{subscriptionId}/resourceGroups/{cosmosSettings.CosmosDbResourceGroup}/providers/Microsoft.DocumentDB/databaseAccounts/{cosmosSettings.CosmosDbAccountName}";
        var account = armClient.GetCosmosDBAccountResource(new Azure.Core.ResourceIdentifier(accountResourceId));

        var location = new Azure.Core.AzureLocation(cosmosSettings.CosmosDbRegion);
        // Create SQL database
        var dbResourceInfo = new CosmosDBSqlDatabaseResourceInfo(cosmosSettings.DatabaseName);
        var dbContent = new CosmosDBSqlDatabaseCreateOrUpdateContent(location, dbResourceInfo);
        var dbCollection = account.GetCosmosDBSqlDatabases();
        await dbCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, cosmosSettings.DatabaseName, dbContent);

        // Create SQL container
        // Best-effort: partition key is required for a successful create, but if ARM models differ,
        // data-plane provisioning will still guarantee correctness.
        var dbResource = dbCollection.GetCosmosDBSqlDatabase(cosmosSettings.DatabaseName);
        var containerResourceInfo = new CosmosDBSqlContainerResourceInfo(cosmosSettings.ContainerName);
        var containerContent = new CosmosDBSqlContainerCreateOrUpdateContent(location, containerResourceInfo);
        var containers = dbResource.GetCosmosDBSqlContainers();
        await containers.CreateOrUpdateAsync(Azure.WaitUntil.Completed, cosmosSettings.ContainerName, containerContent);
    }
}
catch
{
    // Best-effort only.
}

// Data-plane provisioning (must succeed for container existence).
var cosmosClient = new CosmosClient(cosmosSettings.CosmosDbUri, credential);
var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosSettings.DatabaseName);
var containerProperties = new ContainerProperties(cosmosSettings.ContainerName, partitionKeyPath);
var container = await database.CreateContainerIfNotExistsAsync(containerProperties);

builder.Services.AddSingleton(cosmosClient);
builder.Services.AddSingleton(container);
builder.Services.AddSingleton(new AuditTrailRepository(container));
builder.Services.AddSingleton<ICurrencyProviderAdapter, CurrencyProviderAdapter>();
builder.Services.AddSingleton<CurrencyProviderAdapter>();
builder.Services.AddSingleton<ICurrencyConversionService, CurrencyConversionService>();

builder.Services.AddHttpClient<ICurrencyProviderClient, CurrencyProviderClient>((sp, http) =>
{
    var baseUrl = Environment.GetEnvironmentVariable("CURRENCY_API_BASE_URL") ?? "https://frankfurter.dev";
    http.BaseAddress = new Uri(baseUrl);
    http.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

app.MapPost("/api/conversions", async (ConversionRequest request, ICurrencyConversionService service, CancellationToken ct) =>
{
    var errors = ValidateRequest(request);
    if (errors is not null)
    {
        return Results.Problem(statusCode: (int)HttpStatusCode.BadRequest, title: "Invalid request", detail: errors);
    }

    try
    {
        var result = await service.CreateConversionAsync(request.Amount, request.FromCurrency, request.ToCurrency, ct);
        return Results.Ok(result);
    }
    catch (CurrencyProviderUnavailableException ex)
    {
        return Results.Problem(statusCode: (int)HttpStatusCode.ServiceUnavailable, title: "Currency rate provider unavailable", detail: ex.Message);
    }
    catch (CurrencyProviderParseException ex)
    {
        return Results.Problem(statusCode: (int)HttpStatusCode.ServiceUnavailable, title: "Currency rate provider unavailable", detail: ex.Message);
    }
    catch (Exception)
    {
        // Do not leak raw exception details.
        return Results.Problem(statusCode: (int)HttpStatusCode.ServiceUnavailable, title: "Conversion unavailable", detail: "Currency rate provider is currently unavailable.");
    }
});

app.MapGet("/api/conversions/{auditId}", async (string auditId, ICurrencyConversionService service, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(auditId))
    {
        return Results.Problem(statusCode: (int)HttpStatusCode.BadRequest, title: "Invalid audit ID", detail: "Audit ID is required.");
    }

    var result = await service.GetConversionAsync(auditId, ct);
    if (result is null)
    {
        return Results.Problem(statusCode: (int)HttpStatusCode.NotFound, title: "Audit record not found", detail: "No conversion audit record exists for the provided audit ID.");
    }

    return Results.Ok(result);
});

app.Run();

static string? ValidateRequest(ConversionRequest request)
{
    if (request.Amount <= 0) return "Amount must be a positive number.";
    if (!IsValidCurrency(request.FromCurrency)) return "FromCurrency must be an uppercase 3-letter ISO code.";
    if (!IsValidCurrency(request.ToCurrency)) return "ToCurrency must be an uppercase 3-letter ISO code.";
    return null;
}

static bool IsValidCurrency(string value) =>
    !string.IsNullOrWhiteSpace(value) &&
    value.Length == 3 &&
    value.All(ch => ch >= 'A' && ch <= 'Z');
