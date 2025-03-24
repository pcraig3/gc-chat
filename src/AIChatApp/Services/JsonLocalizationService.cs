using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;


public class JsonLocalizationService
{
  private readonly IWebHostEnvironment _env;
  private readonly ILogger<JsonLocalizationService> _logger;
  private readonly NavigationManager _navigationManager;

  public JsonLocalizationService(
      IWebHostEnvironment env,
      ILogger<JsonLocalizationService> logger,
      NavigationManager navigationManager)
  {
    _env = env;
    _logger = logger;
    _navigationManager = navigationManager;
  }

  private Dictionary<string, string> LoadResourcesForCulture(string culture)
  {
    var path = Path.Combine(_env.ContentRootPath, "Resources", $"{culture}.json");

    if (!File.Exists(path))
    {
      _logger.LogWarning($"Resource file not found: {path}, falling back to English");
      path = Path.Combine(_env.ContentRootPath, "Resources", "en.json");
    }

    try
    {
      if (File.Exists(path))
      {
        var json = File.ReadAllText(path);
        var resources = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ??
                     new Dictionary<string, string>();
        return resources;
      }
      else
      {
        _logger.LogError($"Neither requested nor fallback resource file exists");
        return new Dictionary<string, string>();
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, $"Error loading resources from {path}");
      return new Dictionary<string, string>();
    }
  }

  private string GetCultureFromUrl()
  {
    try
    {
      var currentUrl = _navigationManager.Uri;
      var uri = new Uri(currentUrl);
      var path = uri.AbsolutePath;

      if (!string.IsNullOrEmpty(path))
      {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length > 0)
        {
          var culture = segments[0].ToLowerInvariant();

          if (culture == "en" || culture == "fr")
          {
            return culture;
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error determining culture from URL");
    }
    return "en"; // Default culture
  }

  public string _(string key)
  {
    return _(key, GetCultureFromUrl());
  }

  public string _(string key, string? culture = null)
  {
    GetCultureFromUrl();
    var resources = LoadResourcesForCulture(culture ?? "en");

    if (resources.TryGetValue(key, out var value))
    {
      return value;
    }

    return key;
  }

  // Keep this for backward compatibility
  public string this[string key]
  {
    get
    {
      return _(key, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }
  }
}