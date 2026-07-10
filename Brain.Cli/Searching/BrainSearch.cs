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

namespace Brain.Cli.Searching;

internal static class BrainSearch
{
    public static IEnumerable<BrainMatch> Search(IEnumerable<BrainEntry> entries, string query)
    {
        var personFilter = TryGetPersonFilter(query);
        var referenceFilter = TryGetReferenceFilter(query);
        var urlFilter = TryGetUrlFilter(query);
        var emailAddressFilter = TryGetEmailAddressFilter(query);
        var tokens = Tokenize(query).ToArray();

        return entries
            .Select(entry => new BrainMatch(entry, Score(entry, query, tokens, personFilter, referenceFilter, urlFilter, emailAddressFilter)))
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Entry.CreatedAt);
    }

    private static int Score(BrainEntry entry, string query, string[] tokens, string personFilter, string referenceFilter, string urlFilter, string emailAddressFilter)
    {
        if (personFilter != null)
            return entry.People.Contains(personFilter, StringComparer.OrdinalIgnoreCase) ? 100 : 0;

        if (referenceFilter != null)
            return entry.References.Contains(referenceFilter, StringComparer.OrdinalIgnoreCase) ? 100 : 0;

        if (urlFilter != null)
            return entry.Urls.Contains(urlFilter, StringComparer.OrdinalIgnoreCase) ? 100 : 0;

        if (emailAddressFilter != null)
            return entry.EmailAddresses.Contains(emailAddressFilter, StringComparer.OrdinalIgnoreCase) ? 100 : 0;

        var score = 0;

        if (entry.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 50;

        foreach (var person in entry.People)
        {
            if (person.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                query.Contains(person, StringComparison.OrdinalIgnoreCase))
                score += 40;
        }

        foreach (var reference in entry.References)
        {
            if (reference.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                query.Contains(reference, StringComparison.OrdinalIgnoreCase))
                score += 45;
        }

        foreach (var url in entry.Urls)
        {
            if (url.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                query.Contains(url, StringComparison.OrdinalIgnoreCase))
                score += 45;
        }

        foreach (var emailAddress in entry.EmailAddresses)
        {
            if (emailAddress.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                query.Contains(emailAddress, StringComparison.OrdinalIgnoreCase))
                score += 45;
        }

        foreach (var token in tokens)
        {
            if (entry.Text.Contains(token, StringComparison.OrdinalIgnoreCase))
                score += 10;

            if (entry.People.Any(x => string.Equals(x, token, StringComparison.OrdinalIgnoreCase)))
                score += 25;

            if (entry.References.Any(x => string.Equals(x, token, StringComparison.OrdinalIgnoreCase)))
                score += 30;

            if (entry.Urls.Any(x => x.Contains(token, StringComparison.OrdinalIgnoreCase)))
                score += 15;

            if (entry.EmailAddresses.Any(x => x.Contains(token, StringComparison.OrdinalIgnoreCase)))
                score += 15;
        }

        return score;
    }

    private static string TryGetPersonFilter(string query)
    {
        var match = Regex.Match(query.Trim(), """^@(?:"(?<quoted>[^"]+)"|(?<name>[A-Za-z][A-Za-z0-9_-]*))$""");
        if (!match.Success)
            return null;

        return match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["name"].Value;
    }

    private static string TryGetReferenceFilter(string query)
    {
        var match = Regex.Match(query.Trim(), @"^[A-Z][A-Z0-9]+-\d+$", RegexOptions.CultureInvariant);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    private static string TryGetUrlFilter(string query)
    {
        var match = Regex.Match(query.Trim(), """^(?:(?:https?://)|(?:www\.))[^\s<>\"']+$""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}') : null;
    }

    private static string TryGetEmailAddressFilter(string query)
    {
        var match = Regex.Match(query.Trim(), """^[A-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Z0-9](?:[A-Z0-9-]*[A-Z0-9])?(?:\.[A-Z0-9](?:[A-Z0-9-]*[A-Z0-9])?)+$""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Value.ToLowerInvariant() : null;
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        return Regex.Matches(text, @"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)
            .Select(x => x.Value)
            .Where(x => x.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
