using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AIChatApp.Models;

namespace AIChatApp.Components.Chat;

public partial class ChatSource : ComponentBase
{
  [Parameter]
  public required Source Source { get; set; }

  [Inject]
  private IServiceProvider ServiceProvider { get; set; } = default!;

  [Inject]
  private IJSRuntime JS { get; set; } = default!;

  private string OverlayId = $"source-overlay-{Guid.NewGuid():N}";
  private IJSObjectReference? _module;

  // Dynamically resolve BlobServiceClient
  private BlobServiceClient? BlobServiceClient => ServiceProvider?.GetService<BlobServiceClient>();

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender)
    {
      try
      {
        _module = await JS.InvokeAsync<IJSObjectReference>("import", "/js/overlay.js");
        await _module.InvokeVoidAsync("wbInitOverlay", OverlayId);
      }
      catch (JSDisconnectedException)
      {
        // Safe to ignore during prerendering
      }
    }
  }

  // Convert an external URL to just its filename
  // Note that internal /files/{filename} URL, will call our FileController
  private string GetFilename(string url)
  {
    if (string.IsNullOrWhiteSpace(url) || BlobServiceClient is null)
      return url;

    try
    {
      var uri = new Uri(url);
      return Path.GetFileName(uri.LocalPath);
    }
    catch
    {
      // If the URL is malformed or parsing fails, fall back to the original
      return url;
    }
  }

  public static string GetFileSize(long bytes)
  {
    const long KB = 1024;
    const long MB = KB * 1024;

    if (bytes < KB)
    {
      return $"{bytes} bytes";
    }
    else if (bytes < MB)
    {
      double kilobytes = (double)bytes / KB;
      return $"{kilobytes:0.##} KB";
    }
    else
    {
      double megabytes = (double)bytes / MB;
      return $"{megabytes:0.##} MB";
    }
  }
}
