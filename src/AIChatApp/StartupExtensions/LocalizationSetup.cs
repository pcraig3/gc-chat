using Microsoft.AspNetCore.Localization.Routing;

namespace StartupExtensions;

public static class LocalizationSetup
{
  public static void AddLocalizationSupport(this IServiceCollection services, params string[] supportedCultures)
  {
    if (supportedCultures == null || supportedCultures.Length == 0)
      throw new ArgumentException("At least one supported culture must be specified.");

    services.Configure<RequestLocalizationOptions>(options =>
    {
      options.SetDefaultCulture(supportedCultures[0])
                 .AddSupportedCultures(supportedCultures)
                 .AddSupportedUICultures(supportedCultures);

      options.RequestCultureProviders.Insert(0, new RouteDataRequestCultureProvider
      {
        RouteDataStringKey = "culture",
        UIRouteDataStringKey = "culture"
      });
    });
  }
}
