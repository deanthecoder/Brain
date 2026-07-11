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
using System.Text.Json;

namespace Brain.Tests;

[TestFixture]
public sealed class BrainStoreTests
{
    [Test]
    public void GivenSavedEntryCheckItRoundTripsAndPeopleAreDerived()
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
            ["erica@example.com"],
            Tags: ["work"],
            OriginalText: "@Erica mentioned PLAT-123 #work");

        store.Append(entry);
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
            Assert.That(loadedEntry.Tags, Is.EqualTo(entry.Tags));
            Assert.That(loadedEntry.OriginalText, Is.EqualTo(entry.OriginalText));
            Assert.That(store.LoadPeople(), Is.EquivalentTo(new[] { "Erica" }));
        });
    }

    [Test]
    public void GivenLegacyJsonLinesStoreCheckEntriesAreMigrated()
    {
        using var home = new TempDirectory();
        var entry = new BrainEntry(
            "legacy-1",
            DateTimeOffset.Parse("2026-07-10T10:00:00+01:00"),
            "@Erica mentioned PLAT-123",
            "work",
            "jira-style reference",
            ["Erica"],
            ["PLAT-123"],
            Array.Empty<string>(),
            Array.Empty<string>());
        var legacyEntriesPath = Path.Combine(home.FullName, "entries.jsonl");
        var legacyPeoplePath = Path.Combine(home.FullName, "people.json");

        File.WriteAllText(legacyEntriesPath, JsonSerializer.Serialize(entry, BrainJson.Options) + Environment.NewLine);
        File.WriteAllText(legacyPeoplePath, "[\"Erica\"]");

        var store = new BrainStore(home);

        Assert.Multiple(() =>
        {
            Assert.That(store.LoadEntries().Single().Id, Is.EqualTo("legacy-1"));
            Assert.That(store.LoadPeople(), Is.EquivalentTo(new[] { "Erica" }));
            Assert.That(File.Exists(legacyEntriesPath), Is.False);
            Assert.That(File.Exists(legacyPeoplePath), Is.False);
            Assert.That(File.Exists(Path.Combine(home.FullName, "entries", "entry-legacy-1.json")), Is.True);
        });
    }

    [Test]
    public void GivenForgottenEntryCheckItIsExcludedFromLoadedEntries()
    {
        using var home = new TempDirectory();
        var store = new BrainStore(home);
        store.Append(new BrainEntry(
            "note-1",
            DateTimeOffset.UtcNow,
            "A remembered thought",
            null,
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>()));

        store.Forget("note-1");

        Assert.Multiple(() =>
        {
            Assert.That(store.LoadEntries(), Is.Empty);
            Assert.That(store.GetSyncFiles().Select(x => x.Name), Is.EquivalentTo(new[] { "entry-note-1.json", "forgotten-note-1.json" }));
        });
    }
}
