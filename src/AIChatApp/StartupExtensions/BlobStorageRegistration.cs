using Azure.Storage.Blobs;

namespace StartupExtensions;

public static class BlobStorageRegistration
{
  public static void AddBlobStorageIfAvailable(this WebApplicationBuilder builder, ILogger logger)
  {
    var config = builder.Configuration;

    var blobConnectionString = config["storage-connection-string"];
    var containerName = config["STORAGE_CONTAINER_NAME"];

    if (string.IsNullOrWhiteSpace(blobConnectionString) || string.IsNullOrWhiteSpace(containerName))
    {
      logger.LogWarning("No BlobServiceClient: Documents can NOT be retrieved from blob store");
      return;
    }

    var blobServiceClient = new BlobServiceClient(blobConnectionString);
    var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

    if (containerClient.Exists())
    {
      builder.Services.AddSingleton(blobServiceClient);
      builder.Services.AddSingleton(containerClient);
      logger.LogInformation("BlobServiceClient registered: Documents can be retrieved from blob store");
    }
    else
    {
      logger.LogWarning($"No BlobServiceClient: Container '{containerName}' does not exist.");
    }
  }
}
