namespace AIChatApp.Services
{
  public class UserService
  {
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
      _configuration = configuration;
      _httpContextAccessor = httpContextAccessor;
    }

    // Method to get the current User ID (Azure AD ID)
    public string? GetUserIdAsync()
    {
      if (IsLocalEnvironment() && !string.IsNullOrWhiteSpace(_configuration["mock-user-id"]))
      {
        return _configuration["mock-user-id"];
      }

      // \Check for Azure Easy Auth User ID (Primary ID Only)
      var request = _httpContextAccessor.HttpContext?.Request;
      var userId = request?.Headers["X-MS-CLIENT-PRINCIPAL-ID"].FirstOrDefault();

      return userId;
    }

    public bool IsUserAvailable()
    {
      var userId = GetUserIdAsync();
      return !string.IsNullOrWhiteSpace(userId);
    }

    // Method to determine if we are in a local development environment
    private bool IsLocalEnvironment()
    {
      var request = _httpContextAccessor?.HttpContext?.Request;
      var host = request?.Host.Host?.ToLower();

      // Local development if host is localhost or 127.0.0.1
      return host == "localhost" || host == "127.0.0.1";
    }
  }
}
