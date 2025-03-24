namespace AIChatApp.Helpers;

public class ConfigHelper
{
  private readonly ILogger<ConfigHelper> _logger;

  public ConfigHelper(ILogger<ConfigHelper> logger)
  {
    _logger = logger;
  }

  /// <summary>
  /// Gets a double value from configuration. Returns null if not found/empty, or the default value if provided.
  /// </summary>
  /// <param name="config">The configuration instance</param>
  /// <param name="key">The configuration key</param>
  /// <param name="defaultValue">Optional default value to return if key not found</param>
  /// <returns>The parsed double value, default value, or null</returns>
  public double? GetDouble(IConfiguration config, string key, double? defaultValue = null)
  {
    var value = config[key];
    if (string.IsNullOrEmpty(value))
    {
      return defaultValue;
    }

    if (double.TryParse(value, out var result))
    {
      return result;
    }

    // Log warning about invalid value and return default
    _logger.LogWarning("Invalid double value '{Value}' for configuration key '{Key}'. Using default: {DefaultValue}", value, key, defaultValue);
    return defaultValue;
  }

  /// <summary>
  /// Gets an integer value from configuration. Returns null if not found/empty, or the default value if provided.
  /// </summary>
  /// <param name="config">The configuration instance</param>
  /// <param name="key">The configuration key</param>
  /// <param name="defaultValue">Optional default value to return if key not found</param>
  /// <returns>The parsed integer value, default value, or null</returns>
  public int? GetInt(IConfiguration config, string key, int? defaultValue = null)
  {
    var value = config[key];
    if (string.IsNullOrEmpty(value))
    {
      return defaultValue;
    }

    if (int.TryParse(value, out var result))
    {
      return result;
    }

    // Log warning about invalid value and return default
    _logger.LogWarning("Invalid integer value '{Value}' for configuration key '{Key}'. Using default: {DefaultValue}", value, key, defaultValue);
    return defaultValue;
  }

  /// <summary>
  /// Gets a string value from configuration. Returns null if not found/empty, or the default value if provided.
  /// </summary>
  /// <param name="config">The configuration instance</param>
  /// <param name="key">The configuration key</param>
  /// <param name="defaultValue">Optional default value to return if key not found</param>
  /// <returns>The string value, default value, or null</returns>
  public string? GetString(IConfiguration config, string key, string? defaultValue = null)
  {
    var value = config[key];
    return string.IsNullOrEmpty(value) ? defaultValue : value;
  }

  /// <summary>
  /// Gets a boolean value from configuration. Returns null if not found/empty, or the default value if provided.
  /// </summary>
  /// <param name="config">The configuration instance</param>
  /// <param name="key">The configuration key</param>
  /// <param name="defaultValue">Optional default value to return if key not found</param>
  /// <returns>The parsed boolean value, default value, or null</returns>
  public bool? GetBool(IConfiguration config, string key, bool? defaultValue = null)
  {
    var value = config[key];
    if (string.IsNullOrEmpty(value))
    {
      return defaultValue;
    }

    if (bool.TryParse(value, out var result))
    {
      return result;
    }

    // Log warning about invalid value and return default
    _logger.LogWarning("Invalid boolean value '{Value}' for configuration key '{Key}'. Using default: {DefaultValue}", value, key, defaultValue);
    return defaultValue;
  }
}
