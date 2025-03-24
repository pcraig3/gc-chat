using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace AIChatApp.Components.Shared
{
  public class SharedCultureComponent : ComponentBase
  {
    [Parameter]
    public string? Culture { get; set; }

    [CascadingParameter(Name = "Culture")]
    protected string? CascadedCulture { get; set; }

    protected string CurrentCulture => Culture ?? CascadedCulture ?? "en";

    protected override void OnParametersSet()
    {
      var culture = CurrentCulture;

      var supportedCultures = new[] { "en", "fr" };
      if (supportedCultures.Contains(culture))
      {
        var cultureInfo = new CultureInfo(culture);
        CultureInfo.CurrentCulture = cultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo;
      }

      base.OnParametersSet();
    }
  }
}