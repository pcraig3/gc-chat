using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("files")]
public class FileController : ControllerBase
{
  private readonly BlobServiceClient? _blobServiceClient;
  private readonly string? _containerName;

  public FileController(BlobServiceClient? blobServiceClient, IConfiguration config)
  {
    _blobServiceClient = blobServiceClient;
    _containerName = config["STORAGE_CONTAINER_NAME"];
  }

  [HttpGet("{fileName}")]
  public async Task<IActionResult> GetFile(string fileName)
  {
    // Return 503 if the blob service isn't properly configured
    if (_blobServiceClient is null || string.IsNullOrWhiteSpace(_containerName))
    {
      return StatusCode(503, "Blob service not configured.");
    }

    var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
    var blobClient = containerClient.GetBlobClient(fileName);

    // Return not found if file doesn't exist
    if (!await blobClient.ExistsAsync())
      return NotFound();

    var stream = await blobClient.OpenReadAsync();
    var contentType = GetMimeType(fileName);

    // For other file types, force download
    return File(stream, contentType, fileName);
  }

  // Helper method to determine the MIME type
  private static string GetMimeType(string fileName)
  {
    var extension = Path.GetExtension(fileName).ToLowerInvariant();
    return extension switch
    {
      ".pdf" => "application/pdf",
      ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      ".doc" => "application/msword",
      ".txt" => "text/plain",
      ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      ".xls" => "application/vnd.ms-excel",
      ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
      ".ppt" => "application/vnd.ms-powerpoint",
      ".csv" => "text/csv",
      ".jpg" => "image/jpeg",
      ".jpeg" => "image/jpeg",
      ".png" => "image/png",
      ".gif" => "image/gif",
      ".zip" => "application/zip",
      ".json" => "application/json",
      ".xml" => "application/xml",
      _ => "application/octet-stream" // Default to binary download for unknown types
    };
  }
}
