// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Brain.Cli.Models;
using System.Text.RegularExpressions;

namespace Brain.Cli.Parsing;

internal static partial class BrainParser
{
    public static BrainAnalysis Analyse(string text, IReadOnlySet<string> knownPeople)
    {
        var explicitPeople = FindExplicitPeople(text);
        var people = new HashSet<string>(explicitPeople, StringComparer.OrdinalIgnoreCase);

        foreach (var knownPerson in knownPeople)
        {
            if (!string.Equals(knownPerson, "todo", StringComparison.OrdinalIgnoreCase) && ContainsWord(text, knownPerson))
                people.Add(knownPerson);
        }

        var references = ReferenceRegex()
            .Matches(text)
            .Select(x => x.Value.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var urls = BrainUrl.FindAll(text)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var emailAddresses = EmailAddressRegex()
            .Matches(text)
            .Select(x => x.Value.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tags = HashtagRegex()
            .Matches(text)
            .Select(x => x.Groups["tag"].Value.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (urls.Count > 0)
            tags.Add("url");

        var cleanText = HashtagRegex().Replace(text, "\u001F");
        cleanText = WhitespaceBeforeTagMarkerRegex().Replace(cleanText, string.Empty);
        cleanText = WhitespaceRegex().Replace(cleanText, " ").Trim();

        var (context, reason) = DetermineContext(text, references);

        return new BrainAnalysis(people, explicitPeople, references, urls, emailAddresses, tags, cleanText, context, reason,
            TodoTagRegex().IsMatch(text) || tags.Contains("todo"));
    }

    private static HashSet<string> FindExplicitPeople(string text)
    {
        var people = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in PersonTagRegex().Matches(text))
        {
            var name = match.Groups["quoted"].Success
                ? match.Groups["quoted"].Value
                : match.Groups["name"].Value;

            name = name.Trim();

            if (name.Length > 0 && !string.Equals(name, "todo", StringComparison.OrdinalIgnoreCase))
                people.Add(name);
        }

        return people;
    }

    private static (string Context, string Reason) DetermineContext(string text, IReadOnlySet<string> references)
    {
        if (references.Count > 0)
            return ("work", "jira-style reference");

        if (PersonalPhraseRegex().IsMatch(text))
            return ("personal", "personal relationship phrase");

        return (null, null);
    }

    private static bool ContainsWord(string text, string word)
    {
        return Regex.IsMatch(
            text,
            $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(word)}(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    [GeneratedRegex("""(?<![\w.])@(?:"(?<quoted>[^"]+)"|(?<name>[A-Za-z][A-Za-z0-9_-]*))""", RegexOptions.CultureInvariant)]
    private static partial Regex PersonTagRegex();

    [GeneratedRegex(@"\b[A-Z][A-Z0-9]+-\d+\b", RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex("""(?<![\w@])[A-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Z0-9](?:[A-Z0-9-]*[A-Z0-9])?(?:\.[A-Z0-9](?:[A-Z0-9-]*[A-Z0-9])?)+(?![\w@])""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailAddressRegex();

    [GeneratedRegex(@"\b(my wife|my husband|my partner|my son|my daughter|mum|mom|dad)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PersonalPhraseRegex();

    [GeneratedRegex(@"(?<![\w.])@todo\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TodoTagRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}_/#])#(?<tag>[\p{L}\p{N}](?:[\p{L}\p{N}_-]*[\p{L}\p{N}])?)\b", RegexOptions.CultureInvariant)]
    private static partial Regex HashtagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("\\s*\\u001F", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceBeforeTagMarkerRegex();
}
