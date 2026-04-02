using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Azure.Cosmos;

namespace GloboTicket.Services.EventCatalog.HealthChecks
{
    public class CosmosDbHealthCheck : IHealthCheck
    {
        private readonly CosmosClient _cosmosClient;
        private readonly string _databaseName;        

        public CosmosDbHealthCheck(CosmosClient cosmosClient, IConfiguration configuration)
        {
            _cosmosClient = cosmosClient;
            _databaseName = configuration["CosmosDb:DatabaseName"];
        }        

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(3)); // match K8s timeoutSeconds

                var database = _cosmosClient.GetDatabase(_databaseName);
                await database.ReadAsync(cancellationToken: cts.Token);
                return HealthCheckResult.Healthy();
            }
            catch (OperationCanceledException)
            {
                return HealthCheckResult.Unhealthy("Cosmos DB health check timed out");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Cosmos DB unreachable", ex);
            }
        }
    }
}