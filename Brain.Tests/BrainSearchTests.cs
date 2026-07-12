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
using Brain.Cli.Searching;

namespace Brain.Tests;

[TestFixture]
public sealed class BrainSearchTests
{
    [Test]
    public void GivenPersonFilterCheckOnlyMatchingPersonIsReturned()
    {
        var erica = Entry("erica", people: ["Erica"], references: ["PLAT-123"]);
        var ada = Entry("ada", people: ["Ada"], references: ["PLAT-456"]);

        var matches = BrainSearch.Search([erica, ada], "@Erica").ToArray();

        Assert.That(matches.Select(x => x.Entry.Id), Is.EqualTo(new[] { "erica" }));
        Assert.That(matches[0].Score, Is.EqualTo(100));
    }

    [Test]
    public void GivenFullTextAndTokenMatchesCheckFullTextMatchRanksFirst()
    {
        var phraseMatch = Entry("phrase", "16 bit support is no longer needed");
        var tokenMatch = Entry("token", "The processor has 16 registers");

        var matches = BrainSearch.Search([tokenMatch, phraseMatch], "16 bit").ToArray();

        Assert.That(matches.Select(x => x.Entry.Id), Is.EqualTo(new[] { "phrase", "token" }));
    }

    [Test]
    public void GivenHashtagQueryCheckOnlyMatchingTagIsReturned()
    {
        var admin = Entry("admin") with { Tags = ["admin"] };
        var home = Entry("home") with { Tags = ["home"] };

        var matches = BrainSearch.Search([admin, home], "#admin").ToArray();

        Assert.That(matches.Select(x => x.Entry.Id), Is.EqualTo(new[] { "admin" }));
        Assert.That(matches[0].Score, Is.EqualTo(100));
    }

    [Test]
    public void GivenBareDomainQueryCheckUrlsOnDomainAreReturned()
    {
        var github = Entry("github") with { Urls = ["github.com/deanthecoder"] };
        var example = Entry("example") with { Urls = ["example.com"] };

        var matches = BrainSearch.Search([github, example], "github.com").ToArray();

        Assert.That(matches.Select(x => x.Entry.Id), Is.EqualTo(new[] { "github" }));
        Assert.That(matches[0].Score, Is.EqualTo(100));
    }

    [Test]
    public void GivenExactEntryIdCheckOnlyThatEntryIsReturned()
    {
        var exact = Entry("abc123", "The matching entry");
        var textMatch = Entry("other", "This mentions abc123 in its text");

        var matches = BrainSearch.Search([textMatch, exact], "ABC123").ToArray();

        Assert.That(matches.Select(x => x.Entry.Id), Is.EqualTo(new[] { "abc123" }));
        Assert.That(matches[0].Score, Is.EqualTo(1000));
    }

    [Test]
    public void GivenPartialEntryIdCheckItIsNotTreatedAsAnIdMatch()
    {
        var entry = Entry("abc123", "A remembered thought");

        var matches = BrainSearch.Search([entry], "abc12").ToArray();

        Assert.That(matches, Is.Empty);
    }

    private static BrainEntry Entry(
        string id,
        string text = "A remembered thought",
        IReadOnlyList<string> people = null,
        IReadOnlyList<string> references = null)
    {
        return new BrainEntry(
            id,
            DateTimeOffset.UnixEpoch,
            text,
            null,
            null,
            people ?? Array.Empty<string>(),
            references ?? Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}
