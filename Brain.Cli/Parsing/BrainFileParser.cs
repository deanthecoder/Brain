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

internal static partial class BrainFileParser
{
    public static BrainFileAnalysis Analyse(string text)
    {
        var files = FileRegex().Matches(text)
            .Select(x => x.Groups["quoted"].Success ? x.Groups["quoted"].Value : x.Groups["path"].Value)
            .Select(x => new FileInfo(Path.GetFullPath(x)))
            .ToArray();
        var cleanText = WhitespaceRegex().Replace(FileRegex().Replace(text, " "), " ").Trim();

        return new BrainFileAnalysis(cleanText, files);
    }

    [GeneratedRegex("""(?<!\S)@file:(?:\"(?<quoted>[^\"]+)\"|(?<path>\S+))""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FileRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

internal sealed record BrainFileAnalysis(string Text, IReadOnlyList<FileInfo> Files);
