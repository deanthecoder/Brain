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
    private const string LegacyEntriesFileName = "entries.jsonl";
    private const string LegacyPeopleFileName = "people.json";

    public BrainStore(DirectoryInfo home)
    {
        Home = home;
        Home.Create();
        EntriesDirectory.Create();
        MigrateLegacyStore();
    }

    public DirectoryInfo Home { get; }

    private DirectoryInfo EntriesDirectory => Home.GetDir(EntriesDirectoryName);

    private FileInfo LegacyEntriesFile => Home.GetFile(LegacyEntriesFileName);

    private FileInfo LegacyPeopleFile => Home.GetFile(LegacyPeopleFileName);

    public void Append(BrainEntry entry)
    {
        Save(entry);
    }

    public IReadOnlyList<BrainEntry> LoadEntries()
    {
        return GetEntryFiles().Select(ReadEntry).ToArray();
    }

    public HashSet<string> LoadPeople()
    {
        return LoadEntries()
            .SelectMany(x => x.People)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<FileInfo> GetEntryFiles()
    {
        return EntriesDirectory.EnumerateFiles("entry-*.json", SearchOption.TopDirectoryOnly);
    }

    public void Import(Stream stream)
    {
        var entry = JsonSerializer.Deserialize<BrainEntry>(stream, BrainJson.Options);
        if (entry == null)
            throw new InvalidDataException("The entry file did not contain a Brain entry.");

        Save(Normalise(entry));
    }

    private void Save(BrainEntry entry)
    {
        var destination = GetEntryFile(entry.Id);
        if (destination.Exists())
            return;

        var temporary = EntriesDirectory.GetFile($".{entry.Id}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporary.FullName, JsonSerializer.Serialize(entry, BrainJson.Options) + Environment.NewLine);

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

    private static BrainEntry ReadEntry(FileInfo file)
    {
        var entry = JsonSerializer.Deserialize<BrainEntry>(file.ReadAllText(), BrainJson.Options);
        if (entry == null)
            throw new InvalidDataException($"The entry file '{file.FullName}' did not contain a Brain entry.");

        return Normalise(entry);
    }

    private static BrainEntry Normalise(BrainEntry entry)
    {
        return entry with
        {
            People = entry.People ?? Array.Empty<string>(),
            References = entry.References ?? Array.Empty<string>(),
            Urls = entry.Urls ?? Array.Empty<string>(),
            EmailAddresses = entry.EmailAddresses ?? Array.Empty<string>()
        };
    }
}
