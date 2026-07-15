// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Brain.Cli;
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

    [Test]
    public void GivenTaggedEntriesCheckTagCountsAreCollatedCaseInsensitively()
    {
        using var home = new TempDirectory();
        var store = new BrainStore(home);
        store.Append(Entry("one", ["admin", "todo"]));
        store.Append(Entry("two", ["Admin"]));

        var tags = store.LoadTagCounts();

        Assert.Multiple(() =>
        {
            Assert.That(tags["admin"], Is.EqualTo(2));
            Assert.That(tags["todo"], Is.EqualTo(1));
        });
    }

    [Test]
    public void GivenEntriesAndAttachmentCheckStatsDescribeActiveMemoryAndDiskUsage()
    {
        using var home = new TempDirectory();
        using var source = new TempFile(".txt");
        File.WriteAllText(source.FullName, "attachment content");
        var store = new BrainStore(home);
        var attachment = store.StoreAttachment(source);
        store.Append(Entry("active", ["todo", "work"]) with
        {
            IsTodo = true,
            People = ["Erica"],
            Attachments = [attachment]
        });
        store.Append(Entry("forgotten", ["old"]));
        store.Forget("forgotten");

        var stats = store.LoadStats();

        Assert.Multiple(() =>
        {
            Assert.That(stats.RememberedCount, Is.EqualTo(1));
            Assert.That(stats.TodoCount, Is.EqualTo(1));
            Assert.That(stats.PeopleCount, Is.EqualTo(1));
            Assert.That(stats.TagCount, Is.EqualTo(2));
            Assert.That(stats.Attachments.FileCount, Is.EqualTo(1));
            Assert.That(stats.Attachments.Bytes, Is.EqualTo(new FileInfo(source.FullName).Length));
            Assert.That(stats.Total.FileCount, Is.EqualTo(4));
            Assert.That(stats.Total.Bytes, Is.GreaterThan(stats.Attachments.Bytes));
        });
    }

    [Test]
    public void GivenEntriesCheckExportWritesReadableJsonInDateOrder()
    {
        using var home = new TempDirectory();
        var store = new BrainStore(home);
        store.Append(Entry("later", ["admin"]) with { CreatedAt = DateTimeOffset.Parse("2026-07-11T12:00:00Z") });
        store.Append(Entry("earlier", ["home"]) with { CreatedAt = DateTimeOffset.Parse("2026-07-11T10:00:00Z") });
        var destination = new FileInfo(Path.Combine(home.FullName, "backup", "brain.json"));

        var count = store.Export(destination);
        using var document = JsonDocument.Parse(File.ReadAllText(destination.FullName));
        var entries = document.RootElement.EnumerateArray().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(2));
            Assert.That(entries[0].GetProperty("id").GetString(), Is.EqualTo("earlier"));
            Assert.That(entries[1].GetProperty("id").GetString(), Is.EqualTo("later"));
            Assert.That(File.ReadAllText(destination.FullName), Does.Contain(Environment.NewLine + "  {"));
        });
    }

    [Test]
    public void GivenSameFileTwiceCheckAttachmentBlobIsDeduplicated()
    {
        using var home = new TempDirectory();
        using var source = new TempFile(".png");
        File.WriteAllText(source.FullName, "fake png data");
        var store = new BrainStore(home);

        var first = store.StoreAttachment(source);
        var second = store.StoreAttachment(source);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.ContentType, Is.EqualTo("image/png"));
            Assert.That(Directory.GetFiles(Path.Combine(home.FullName, "attachments")), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void GivenAttachedEntryCheckBlobIsIncludedInSyncFiles()
    {
        using var home = new TempDirectory();
        using var source = new TempFile(".txt");
        File.WriteAllText(source.FullName, "attachment content");
        var store = new BrainStore(home);
        var attachment = store.StoreAttachment(source);
        store.Append(Entry("attached", []) with { Attachments = [attachment] });

        Assert.That(store.GetSyncFiles().Select(x => x.Name), Is.EquivalentTo(new[]
        {
            "entry-attached.json",
            $"attachment-{attachment.Hash}.blob"
        }));
    }

    [Test]
    public void GivenAttachmentOrphanedForThirtyDaysCheckItIsPruned()
    {
        using var home = new TempDirectory();
        using var source = new TempFile(".txt");
        File.WriteAllText(source.FullName, "attachment content");
        var store = new BrainStore(home);
        var attachment = store.StoreAttachment(source);
        store.Append(Entry("attached", []) with { Attachments = [attachment] });
        store.Forget("attached");
        var pruneAt = store.LoadAttachmentStatuses(DateTimeOffset.UtcNow).Single().PruneAt.Value;

        var pruned = store.PruneAttachments(pruneAt.AddSeconds(1), false);

        Assert.Multiple(() =>
        {
            Assert.That(pruned.Select(x => x.Hash), Is.EqualTo(new[] { attachment.Hash }));
            Assert.That(store.GetAttachmentFile(attachment.Hash).Exists, Is.False);
        });
    }

    [Test]
    public void GivenSharedAttachmentCheckActiveReferencePreventsPruning()
    {
        using var home = new TempDirectory();
        using var source = new TempFile(".txt");
        File.WriteAllText(source.FullName, "shared attachment");
        var store = new BrainStore(home);
        var attachment = store.StoreAttachment(source);
        store.Append(Entry("forgotten", []) with { Attachments = [attachment] });
        store.Append(Entry("active", []) with { Attachments = [attachment] });
        store.Forget("forgotten");

        var pruned = store.PruneAttachments(DateTimeOffset.UtcNow.AddYears(1), false);

        Assert.Multiple(() =>
        {
            Assert.That(pruned, Is.Empty);
            Assert.That(store.GetAttachmentFile(attachment.Hash).Exists, Is.True);
        });
    }

    [Test]
    public void GivenAttachmentOverTenMegabytesCheckItIsRejected()
    {
        using var home = new TempDirectory();
        using var source = new TempFile(".bin");
        using (var stream = File.Create(source.FullName))
            stream.SetLength(10 * 1024 * 1024 + 1);

        var exception = Assert.Throws<BrainUsageException>(() => new BrainStore(home).StoreAttachment(source));

        Assert.That(exception.Message, Does.Contain("10 MB"));
    }

    [Test]
    public void GivenCorruptImportedAttachmentCheckIntegrityFailureIsReported()
    {
        using var home = new TempDirectory();
        var store = new BrainStore(home);
        using var stream = new MemoryStream("corrupt"u8.ToArray());

        var exception = Assert.Throws<InvalidDataException>(() => store.Import($"attachment-{new string('0', 64)}.blob", stream));

        Assert.That(exception.Message, Does.Contain("integrity check"));
    }

    private static BrainEntry Entry(string id, IReadOnlyList<string> tags)
    {
        return new BrainEntry(
            id,
            DateTimeOffset.UtcNow,
            "A remembered thought",
            null,
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Tags: tags);
    }
}
