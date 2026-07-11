// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Text.RegularExpressions;

namespace Brain.Cli.Parsing;

internal static partial class BrainUrl
{
    private static readonly char[] TrailingPunctuation = ['.', ',', ';', ':', '!', '?', ')', ']', '}'];
    private static readonly HashSet<string> FileExtensions = new([
        "cs", "csproj", "dll", "dmg", "exe", "json", "md", "sln", "slnx", "txt", "xml", "yaml", "yml"
    ], StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> FindAll(string text)
    {
        return UrlRegex()
            .Matches(text)
            .Where(x => IsPlausibleUrl(x.Value))
            .Select(x => Normalise(x.Value))
            .Where(x => x.Length > 0);
    }

    public static bool TryParse(string text, out string url)
    {
        var value = text.Trim();
        var match = UrlRegex().Match(value);
        if (!match.Success || match.Index != 0 || match.Length != value.Length || !IsPlausibleUrl(match.Value))
        {
            url = null;
            return false;
        }

        url = Normalise(match.Value);
        return url.Length > 0;
    }

    private static string Normalise(string value) => value.TrimEnd(TrailingPunctuation);

    private static bool IsPlausibleUrl(string value)
    {
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            return true;

        var host = value.Split('/', '?', '#')[0].Split(':')[0];
        var topLevelDomain = host[(host.LastIndexOf('.') + 1)..];
        return !FileExtensions.Contains(topLevelDomain) &&
               (topLevelDomain.All(char.IsLower) || topLevelDomain.All(char.IsUpper));
    }

    [GeneratedRegex("""(?<![\w@])(?:(?:(?:https?://)|(?:www\.))[^\s<>\"']+|(?:[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?\.)+[A-Z]{2,63}(?::\d+)?(?:[/?#][^\s<>\"']*)?)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();
}
