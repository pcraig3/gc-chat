using System.Text.Json;
using AIChatApp.Helpers;
using AIChatApp.Models;

public class SearchService
{
  private readonly HttpClient _http;
  private readonly IConfiguration _config;
  private readonly ConfigHelper _configHelper;

  public SearchService(HttpClient http, IConfiguration config, ConfigHelper configHelper)
  {
    _http = http;
    _config = config;
    _configHelper = configHelper;
  }

  // Get the top search results count from configuration
  // Ensures minimum of 1 result
  private int GetTopSearchResultsCount()
  {
    var configValue = _configHelper.GetInt(_config, "var-vector-search-results") ?? 5; // default to 5
    return Math.Max(1, configValue); // ensure minimum of 1
  }

  public async Task<bool> ExistsAsync()
  {
    try
    {
      string searchEndpoint = _config["search-endpoint"]!;
      string indexName = _config["search-index-name"]!;
      string apiKey = _config["search-api-key"]!;

      // Simple ping request to verify the service exists
      var url = $"{searchEndpoint}/indexes/{indexName}/stats?api-version=2024-11-01-preview";

      var request = new HttpRequestMessage(HttpMethod.Get, url);
      request.Headers.Add("api-key", apiKey);

      var response = await _http.SendAsync(request);

      // If we get a successful response, the service exists
      return response.IsSuccessStatusCode;
    }
    catch
    {
      // Any exception indicates the service is not accessible
      return false;
    }
  }

  public async Task<List<Source>> GetTopChunks(string userQuery)
  {
    string searchEndpoint = _config["search-endpoint"]!;
    string indexName = _config["search-index-name"]!;
    string apiKey = _config["search-api-key"]!;
    int topResults = GetTopSearchResultsCount();

    var url = $"{searchEndpoint}/indexes/{indexName}/docs/search?api-version=2024-11-01-preview";

    var requestBody = new
    {
      search = userQuery,
      top = topResults,
      queryType = "semantic",
      select = "chunk,title,culture,path,type,size"
    };

    var request = new HttpRequestMessage(HttpMethod.Post, url)
    {
      Content = JsonContent.Create(requestBody)
    };

    request.Headers.Add("api-key", apiKey);

    var response = await _http.SendAsync(request);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
    var hits = json?.RootElement.GetProperty("value");

    // System.Text.Json.JsonElement.GetProperty(string) throws exception if the property is missing.

    var processedHits = hits?.EnumerateArray()
    .Where(hit =>
        hit.TryGetProperty("chunk", out var chunkProp) &&
        hit.TryGetProperty("title", out var titleProp) &&
        hit.TryGetProperty("culture", out var cultureProp) &&
        hit.TryGetProperty("path", out var pathProp) &&
        hit.TryGetProperty("size", out var sizeProp) &&
        !string.IsNullOrWhiteSpace(chunkProp.GetString()))
    .Select(hit =>
    {
      using var doc = JsonDocument.Parse(hit.GetRawText());

      var title = hit.GetProperty("title").GetString() ?? "";
      var chunk = hit.GetProperty("chunk").GetString() ?? "";
      var culture = hit.GetProperty("culture").GetString() ?? "unknown";
      var path = hit.GetProperty("path").GetString() ?? "";
      var size = hit.GetProperty("size").GetInt32();

      return new Source(title, chunk, culture, path, size);
    })
    .ToList();

    // Console.WriteLine(JsonSerializer.Serialize(processedHits, new JsonSerializerOptions { WriteIndented = true }));

    return processedHits ?? new List<Source>();
  }
}
