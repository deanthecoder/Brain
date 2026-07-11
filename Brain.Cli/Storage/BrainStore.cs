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
using System.Text.Json;

namespace Brain.Cli.Storage;

internal sealed class BrainStore
{
    private const string EntriesDirectoryName = "entries";
    private const string ForgottenDirectoryName = "forgotten";
    private const string LegacyEntriesFileName = "entries.jsonl";
    private const string LegacyPeopleFileName = "people.json";

    public BrainStore(DirectoryInfo home)
    {
        Home = home;
        Home.Create();
        EntriesDirectory.Create();
        ForgottenDirectory.Create();
        MigrateLegacyStore();
    }

    public DirectoryInfo Home { get; }

    private DirectoryInfo EntriesDirectory => Home.GetDir(EntriesDirectoryName);

    private DirectoryInfo ForgottenDirectory => Home.GetDir(ForgottenDirectoryName);

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

        return GetEntryFiles()
            .Select(ReadEntry)
            .Where(x => !forgottenIds.Contains(x.Id))
            .ToArray();
    }

    public HashSet<string> LoadPeople()
    {
        return LoadEntries()
            .SelectMany(x => x.People)
            .Where(x => !string.Equals(x, "todo", StringComparison.OrdinalIgnoreCase))
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
        return GetEntryFiles().Concat(GetForgottenFiles());
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

    private IEnumerable<FileInfo> GetForgottenFiles()
    {
        return ForgottenDirectory.EnumerateFiles("forgotten-*.json", SearchOption.TopDirectoryOnly);
    }

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
            OriginalText = entry.OriginalText ?? entry.Text
        };
    }
}
