using Microsoft.Azure.Cosmos;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Repositories;

public sealed class AuditTrailRepository(Container container)
{
    private readonly Container _container = container;

    public Task CreateAsync(ConversionAuditDocument doc, CancellationToken ct)
        => _container.CreateItemAsync(doc, new PartitionKey(doc.AuditId), cancellationToken: ct);

    public async Task<ConversionAuditDocument?> GetAsync(string auditId, CancellationToken ct)
    {
        try
        {
            var resp = await _container.ReadItemAsync<ConversionAuditDocument>(auditId, new PartitionKey(auditId), cancellationToken: ct);
            return resp.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
