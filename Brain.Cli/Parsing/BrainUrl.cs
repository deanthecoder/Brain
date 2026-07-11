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

    public static IEnumerable<string> FindAll(string text)
    {
        return UrlRegex()
            .Matches(text)
            .Select(x => Normalise(x.Value))
            .Where(x => x.Length > 0);
    }

    public static bool TryParse(string text, out string url)
    {
        var value = text.Trim();
        var match = UrlRegex().Match(value);
        if (!match.Success || match.Index != 0 || match.Length != value.Length)
        {
            url = null;
            return false;
        }

        url = Normalise(match.Value);
        return url.Length > 0;
    }

    private static string Normalise(string value) => value.TrimEnd(TrailingPunctuation);

    [GeneratedRegex("""(?<![\w@])(?:(?:(?:https?://)|(?:www\.))[^\s<>\"']+|(?:[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?\.)+[A-Z]{2,63}(?::\d+)?(?:[/?#][^\s<>\"']*)?)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();
}
