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
using Brain.Cli.Storage;
using DTC.Core;

namespace Brain.Tests;

[TestFixture]
public sealed class BrainStoreTests
{
    [Test]
    public void GivenSavedEntryAndPeopleCheckTheyRoundTrip()
    {
        using var home = new TempDirectory();
        var store = new BrainStore(home);
        var entry = new BrainEntry(
            "note-1",
            DateTimeOffset.Parse("2026-07-10T10:00:00+01:00"),
            "@Erica mentioned PLAT-123",
            "work",
            "jira-style reference",
            ["Erica"],
            ["PLAT-123"],
            ["https://example.com"],
            ["erica@example.com"]);

        store.Append(entry);
        store.SavePeople(new HashSet<string>(["Erica", "Ada"], StringComparer.OrdinalIgnoreCase));
        var loadedEntry = store.LoadEntries().Single();

        Assert.Multiple(() =>
        {
            Assert.That(loadedEntry.Id, Is.EqualTo(entry.Id));
            Assert.That(loadedEntry.CreatedAt, Is.EqualTo(entry.CreatedAt));
            Assert.That(loadedEntry.Text, Is.EqualTo(entry.Text));
            Assert.That(loadedEntry.Context, Is.EqualTo(entry.Context));
            Assert.That(loadedEntry.ContextReason, Is.EqualTo(entry.ContextReason));
            Assert.That(loadedEntry.People, Is.EqualTo(entry.People));
            Assert.That(loadedEntry.References, Is.EqualTo(entry.References));
            Assert.That(loadedEntry.Urls, Is.EqualTo(entry.Urls));
            Assert.That(loadedEntry.EmailAddresses, Is.EqualTo(entry.EmailAddresses));
            Assert.That(store.LoadPeople(), Is.EquivalentTo(new[] { "Ada", "Erica" }));
        });
    }
}
