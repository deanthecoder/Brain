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
using DTC.Core.Extensions;
using System.Security.Cryptography;
using System.Text.Json;

namespace Brain.Cli.Storage;

internal sealed class BrainStore
{
    private const string EntriesDirectoryName = "entries";
    private const string ForgottenDirectoryName = "forgotten";
    private const string AttachmentsDirectoryName = "attachments";
    private const long MaximumAttachmentSize = 10 * 1024 * 1024;
    private const string LegacyEntriesFileName = "entries.jsonl";
    private const string LegacyPeopleFileName = "people.json";

    public BrainStore(DirectoryInfo home)
    {
        Home = home;
        Home.Create();
        EntriesDirectory.Create();
        ForgottenDirectory.Create();
        AttachmentsDirectory.Create();
        MigrateLegacyStore();
    }

    public DirectoryInfo Home { get; }

    private DirectoryInfo EntriesDirectory => Home.GetDir(EntriesDirectoryName);

    private DirectoryInfo ForgottenDirectory => Home.GetDir(ForgottenDirectoryName);

    private DirectoryInfo AttachmentsDirectory => Home.GetDir(AttachmentsDirectoryName);

    private FileInfo LegacyEntriesFile => Home.GetFile(LegacyEntriesFileName);

    private FileInfo LegacyPeopleFile => Home.GetFile(LegacyPeopleFileName);

    public void Append(BrainEntry entry)
    {
        Save(entry);
    }

    public IReadOnlyList<BrainEntry> LoadEntries()
    {
        var forgottenIds = GetForgottenFiles()
            .Select(ReadForgottenEntry)
            .Select(x => x.Id)
            .ToHashSet(StringComparer.Ordinal);

        return LoadAllEntries()
            .Where(x => !forgottenIds.Contains(x.Id))
            .ToArray();
    }

    public BrainEntry FindEntry(string id)
    {
        return LoadEntries().FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));
    }

    public IReadOnlyList<BrainAttachmentStatus> LoadAttachmentStatuses(DateTimeOffset now)
    {
        var allEntries = LoadAllEntries();
        var forgotten = GetForgottenFiles()
            .Select(ReadForgottenEntry)
            .ToDictionary(x => x.Id, x => x.ForgottenAt, StringComparer.Ordinal);
        var activeIds = allEntries
            .Select(x => x.Id)
            .Where(x => !forgotten.ContainsKey(x))
            .ToHashSet(StringComparer.Ordinal);
        var attachments = allEntries
            .SelectMany(entry => entry.Attachments.Select(attachment => (Entry: entry, Attachment: attachment)))
            .GroupBy(x => x.Attachment.Hash, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
        var hashes = attachments.Keys
            .Concat(AttachmentsDirectory.EnumerateFiles("attachment-*.blob").Select(GetAttachmentHash))
            .Distinct(StringComparer.Ordinal);

        return hashes.Select(hash =>
        {
            attachments.TryGetValue(hash, out var references);
            references ??= Array.Empty<(BrainEntry Entry, BrainAttachment Attachment)>();
            var metadata = references.Select(x => x.Attachment).FirstOrDefault();
            var file = GetAttachmentFile(hash);
            var referenceCount = references.Count(x => activeIds.Contains(x.Entry.Id));
            var orphanedAt = referenceCount > 0
                ? (DateTimeOffset?)null
                : GetOrphanedAt(references.Select(x => x.Entry.Id), forgotten, file, now);

            return new BrainAttachmentStatus(
                hash,
                metadata?.FileName ?? file.Name,
                metadata?.ContentType ?? "application/octet-stream",
                file.Exists() ? file.Length : metadata?.Size ?? 0,
                referenceCount,
                orphanedAt,
                orphanedAt?.AddDays(30),
                file.Exists());
        }).OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public BrainEntry FindByOriginalText(string text)
    {
        return LoadEntries()
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefault(x => string.Equals(x.OriginalText, text, StringComparison.OrdinalIgnoreCase));
    }

    public BrainAttachment StoreAttachment(FileInfo source)
    {
        source.Refresh();
        if (!source.Exists)
            throw new BrainUsageException($"Attachment file not found: {source.FullName}");

        if (source.Length > MaximumAttachmentSize)
            throw new BrainUsageException($"Attachment exceeds the 10 MB limit: {source.FullName}");

        var bytes = File.ReadAllBytes(source.FullName);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var destination = GetAttachmentFile(hash);
        if (!destination.Exists())
            File.WriteAllBytes(destination.FullName, bytes);

        return new BrainAttachment(hash, source.Name, GetContentType(source.Extension), bytes.Length);
    }

    public HashSet<string> LoadPeople()
    {
        return LoadEntries()
            .SelectMany(x => x.People)
            .Where(x => !string.Equals(x, "file", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, int> LoadTagCounts()
    {
        return LoadEntries()
            .SelectMany(x => x.Tags)
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
    }

    public int Export(FileInfo destination)
    {
        var entries = LoadEntries()
            .OrderBy(x => x.CreatedAt)
            .ToArray();
        var options = new JsonSerializerOptions(BrainJson.Options) { WriteIndented = true };

        destination.Directory?.Create();
        File.WriteAllText(destination.FullName, JsonSerializer.Serialize(entries, options) + Environment.NewLine);
        return entries.Length;
    }

    public IEnumerable<FileInfo> GetEntryFiles()
    {
        return EntriesDirectory.EnumerateFiles("entry-*.json", SearchOption.TopDirectoryOnly);
    }

    public IEnumerable<FileInfo> GetSyncFiles()
    {
        return GetEntryFiles().Concat(GetForgottenFiles()).Concat(GetReferencedAttachmentFiles());
    }

    public IEnumerable<FileInfo> GetReferencedAttachmentFiles()
    {
        return LoadEntries()
            .SelectMany(x => x.Attachments)
            .Select(x => x.Hash)
            .Distinct(StringComparer.Ordinal)
            .Select(GetAttachmentFile)
            .Where(x => x.Exists());
    }

    public IReadOnlyList<BrainAttachmentStatus> PruneAttachments(DateTimeOffset now, bool dryRun)
    {
        var attachments = LoadAttachmentStatuses(now)
            .Where(x => x.ReferenceCount == 0 && x.PruneAt <= now)
            .ToArray();

        if (!dryRun)
        {
            foreach (var attachment in attachments)
            {
                var file = GetAttachmentFile(attachment.Hash);
                if (file.Exists())
                    file.Delete();
            }
        }

        return attachments;
    }

    public void Forget(string id)
    {
        SaveForgottenEntry(new BrainForgottenEntry(id, DateTimeOffset.UtcNow));
    }

    public void Import(string fileName, Stream stream)
    {
        if (fileName.StartsWith("entry-", StringComparison.Ordinal))
        {
            var entry = JsonSerializer.Deserialize<BrainEntry>(stream, BrainJson.Options);
            if (entry == null)
                throw new InvalidDataException("The entry file did not contain a Brain entry.");

            Save(Normalise(entry));
            return;
        }

        if (fileName.StartsWith("forgotten-", StringComparison.Ordinal))
        {
            var forgottenEntry = JsonSerializer.Deserialize<BrainForgottenEntry>(stream, BrainJson.Options);
            if (forgottenEntry == null)
                throw new InvalidDataException("The forgotten-entry file did not contain a Brain entry ID.");

            SaveForgottenEntry(forgottenEntry);
            return;
        }

        if (fileName.StartsWith("attachment-", StringComparison.Ordinal))
        {
            ImportAttachment(fileName, stream);
            return;
        }

        throw new InvalidDataException($"The sync file '{fileName}' is not recognised.");
    }

    private void Save(BrainEntry entry)
    {
        var destination = GetEntryFile(entry.Id);
        if (destination.Exists())
            return;

        SaveFile(destination, entry);
    }

    private void SaveForgottenEntry(BrainForgottenEntry entry)
    {
        var destination = ForgottenDirectory.GetFile($"forgotten-{entry.Id}.json");
        if (destination.Exists())
            return;

        SaveFile(destination, entry);
    }

    private void SaveFile(FileInfo destination, object value)
    {
        var temporary = destination.Directory.GetFile($".{destination.Name}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporary.FullName, JsonSerializer.Serialize(value, BrainJson.Options) + Environment.NewLine);

        try
        {
            File.Move(temporary.FullName, destination.FullName);
        }
        catch (IOException) when (destination.Exists())
        {
            // Another process wrote the same immutable entry first.
        }
        finally
        {
            if (temporary.Exists())
                temporary.Delete();
        }
    }

    private void MigrateLegacyStore()
    {
        if (!LegacyEntriesFile.Exists())
            return;

        foreach (var line in File.ReadLines(LegacyEntriesFile.FullName))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var entry = JsonSerializer.Deserialize<BrainEntry>(line, BrainJson.Options);
            if (entry == null)
                throw new InvalidDataException("The legacy entry store contains an invalid entry.");

            Save(Normalise(entry));
        }

        LegacyEntriesFile.Delete();
        if (LegacyPeopleFile.Exists())
            LegacyPeopleFile.Delete();
    }

    private FileInfo GetEntryFile(string id) => EntriesDirectory.GetFile($"entry-{id}.json");

    public FileInfo GetAttachmentFile(string hash) => AttachmentsDirectory.GetFile($"attachment-{hash}.blob");

    private IEnumerable<FileInfo> GetForgottenFiles()
    {
        return ForgottenDirectory.EnumerateFiles("forgotten-*.json", SearchOption.TopDirectoryOnly);
    }

    private IReadOnlyList<BrainEntry> LoadAllEntries() => GetEntryFiles().Select(ReadEntry).ToArray();

    private static BrainEntry ReadEntry(FileInfo file)
    {
        var entry = JsonSerializer.Deserialize<BrainEntry>(file.ReadAllText(), BrainJson.Options);
        if (entry == null)
            throw new InvalidDataException($"The entry file '{file.FullName}' did not contain a Brain entry.");

        return Normalise(entry);
    }

    private static BrainForgottenEntry ReadForgottenEntry(FileInfo file)
    {
        var entry = JsonSerializer.Deserialize<BrainForgottenEntry>(file.ReadAllText(), BrainJson.Options);
        if (entry == null)
            throw new InvalidDataException($"The forgotten-entry file '{file.FullName}' did not contain a Brain entry ID.");

        return entry;
    }

    private static BrainEntry Normalise(BrainEntry entry)
    {
        return entry with
        {
            People = entry.People ?? Array.Empty<string>(),
            References = entry.References ?? Array.Empty<string>(),
            Urls = entry.Urls ?? Array.Empty<string>(),
            EmailAddresses = entry.EmailAddresses ?? Array.Empty<string>(),
            Tags = entry.Tags ?? Array.Empty<string>(),
            OriginalText = entry.OriginalText ?? entry.Text,
            Attachments = entry.Attachments ?? Array.Empty<BrainAttachment>()
        };
    }

    private static DateTimeOffset GetOrphanedAt(
        IEnumerable<string> entryIds,
        IReadOnlyDictionary<string, DateTimeOffset> forgotten,
        FileInfo file,
        DateTimeOffset now)
    {
        var forgottenAt = entryIds
            .Where(forgotten.ContainsKey)
            .Select(x => forgotten[x])
            .DefaultIfEmpty()
            .Max();
        if (forgottenAt != default)
            return forgottenAt;

        return file.Exists() ? file.LastWriteTimeUtc : now;
    }

    private static string GetAttachmentHash(FileInfo file) => Path.GetFileNameWithoutExtension(file.Name)["attachment-".Length..];

    private void ImportAttachment(string fileName, Stream stream)
    {
        if (stream.CanSeek && stream.Length > MaximumAttachmentSize)
            throw new InvalidDataException($"Attachment '{fileName}' exceeds the 10 MB limit.");

        var hash = Path.GetFileNameWithoutExtension(fileName)["attachment-".Length..];
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(hash, actualHash, StringComparison.Ordinal))
            throw new InvalidDataException($"Attachment '{fileName}' failed its integrity check.");

        var destination = GetAttachmentFile(hash);
        if (!destination.Exists())
            File.WriteAllBytes(destination.FullName, bytes);
    }

    private static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".gif" => "image/gif",
            ".jpeg" or ".jpg" => "image/jpeg",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".svg" => "image/svg+xml",
            ".txt" => "text/plain",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
