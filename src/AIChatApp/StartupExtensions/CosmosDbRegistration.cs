using Microsoft.Azure.Cosmos;

namespace StartupExtensions;

public static class CosmosDbRegistration
{
  public static async Task AddCosmosClientIfAvailableAsync(this WebApplicationBuilder builder, ILogger logger)
  {
    var config = builder.Configuration;

    var connectionString = config["cosmosdb-connection-string"];
    var databaseName = config["AZURE_COSMOSDB_DATABASE_NAME"];
    var containerName = config["AZURE_COSMOSDB_CONTAINER_NAME"];

    if (string.IsNullOrWhiteSpace(connectionString) ||
        string.IsNullOrWhiteSpace(databaseName) ||
        string.IsNullOrWhiteSpace(containerName))
    {
      logger.LogWarning("No CosmosClient: Configuration values missing");
      return;
    }

    try
    {
      var tempCosmosClient = new CosmosClient(connectionString);

      var database = tempCosmosClient.GetDatabase(databaseName);
      await database.ReadAsync();

      var container = database.GetContainer(containerName);
      await container.ReadContainerAsync();

      var cosmosClientOptions = new CosmosClientOptions
      {
        SerializerOptions = new CosmosSerializationOptions
        {
          PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
      };

      builder.Services.AddSingleton(_ => new CosmosClient(connectionString, cosmosClientOptions));
      logger.LogInformation("CosmosClient registered: Database and container exist");
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      logger.LogWarning($"No CosmosClient: Database '{databaseName}' or container '{containerName}' not found");
    }
    catch (Exception ex)
    {
      logger.LogWarning($"No CosmosClient: Failed to access CosmosDB. Error: {ex.Message}");
    }
  }
}
