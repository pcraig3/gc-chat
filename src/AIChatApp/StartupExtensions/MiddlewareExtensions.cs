using System.Globalization;

namespace StartupExtensions;

public static class MiddlewareExtensions
{
  public static IApplicationBuilder UseAcceptLanguageRedirect(this IApplicationBuilder app)
  {
    return app.Use(async (context, next) =>
    {
      var path = context.Request.Path.Value;

      if (string.Equals(path, "/", StringComparison.OrdinalIgnoreCase))
      {
        var acceptLang = context.Request.Headers["Accept-Language"].ToString();

        if (!string.IsNullOrWhiteSpace(acceptLang) &&
                acceptLang.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
        {
          context.Response.Redirect("/fr", permanent: false);
          return;
        }

        context.Response.Redirect("/en", permanent: false);
        return;
      }

      await next();
    });
  }

  public static IApplicationBuilder UsePathBasedCulture(this IApplicationBuilder app)
  {
    return app.Use(async (context, next) =>
    {
      var path = context.Request.Path.Value;
      if (!string.IsNullOrEmpty(path))
      {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0)
        {
          var culture = segments[0].ToLowerInvariant();
          if (culture == "en" || culture == "fr")
          {
            var cultureInfo = new CultureInfo(culture);
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
          }
        }
      }

      await next();
    });
  }

  public static IApplicationBuilder UseCustom404(this IApplicationBuilder app)
  {
    return app.Use(async (context, next) =>
    {
      var originalPath = context.Request.Path.Value;

      await next();

      if (context.Response.StatusCode == 404 &&
              !context.Response.HasStarted &&
              (originalPath == null || !originalPath.EndsWith("/not-found")))
      {
        context.Response.Redirect("/not-found");
      }
    });
  }
}
