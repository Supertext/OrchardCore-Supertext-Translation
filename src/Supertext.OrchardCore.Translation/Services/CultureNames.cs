using System.Globalization;

namespace Supertext.OrchardCore.Translation.Services;

public static class CultureNames
{
    /// <summary>"German (Switzerland)" for "de-CH"; the code itself if .NET doesn't know it.</summary>
    public static string Get(string culture)
    {
        try
        {
            var name = CultureInfo.GetCultureInfo(culture).DisplayName;
            return string.IsNullOrEmpty(name) ? culture : name;
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
    }
}
