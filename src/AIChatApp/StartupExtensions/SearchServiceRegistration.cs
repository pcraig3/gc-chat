using AIChatApp.Helpers;

namespace StartupExtensions;

public static class SearchServiceRegistration
{
  public static async Task AddSearchServiceIfAvailableAsync(this WebApplicationBuilder builder, ILogger logger)
  {
    var config = builder.Configuration;

    if (
        string.IsNullOrWhiteSpace(config["search-endpoint"]) ||
        string.IsNullOrWhiteSpace(config["search-index-name"]) ||
        string.IsNullOrWhiteSpace(config["search-api-key"]))
    {
      logger.LogWarning("No SearchService: Missing config values");
      return;
    }

    // Create temporary services for testing connectivity using the actual SearchService
    var tempHttpClient = new HttpClient();
    var tempLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ConfigHelper>();
    var tempConfigHelper = new ConfigHelper(tempLogger);
    var tempSearchService = new SearchService(tempHttpClient, config, tempConfigHelper);

    if (await tempSearchService.ExistsAsync())
    {
      builder.Services.AddHttpClient<SearchService>();
      builder.Services.AddScoped<SearchService>();
      logger.LogInformation("SearchService registered: vector search enabled");
    }
    else
    {
      logger.LogWarning("No SearchService: Configuration invalid. Running in direct prompt mode.");
    }

    // Clean up temporary resources
    tempHttpClient.Dispose();
  }
}
