using Azure.Storage.Blobs;

public class BlobMetadata
{
  public string Name { get; set; } = default!;
  public long? Size { get; set; } // bytes
  public DateTime? LastModified { get; set; }
}

public class DocumentService
{
  private readonly BlobContainerClient? _containerClient;

  public DocumentService(BlobContainerClient? containerClient = null)
  {
    _containerClient = containerClient;
  }

  public bool IsInitialized() => _containerClient is not null;

  public async Task<List<BlobMetadata>> GetDocumentListAsync()
  {
    if (_containerClient is null)
      return new();   // Blob store not configured → empty list

    var results = new List<BlobMetadata>();

    await foreach (var blob in _containerClient.GetBlobsAsync())
    {
      results.Add(new BlobMetadata
      {
        Name = blob.Name,
        Size = blob.Properties.ContentLength,
        LastModified = blob.Properties.LastModified?.DateTime
      });
    }

    return results;
  }
}
