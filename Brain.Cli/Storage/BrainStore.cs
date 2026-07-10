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
    private const string EntriesFileName = "entries.jsonl";
    private const string PeopleFileName = "people.json";

    public BrainStore(DirectoryInfo home)
    {
        Home = home;
        Home.Create();
    }

    public DirectoryInfo Home { get; }

    private FileInfo EntriesFile => Home.GetFile(EntriesFileName);

    private FileInfo PeopleFile => Home.GetFile(PeopleFileName);

    public void Append(BrainEntry entry)
    {
        var line = JsonSerializer.Serialize(entry, BrainJson.Options);
        File.AppendAllText(EntriesFile.FullName, line + Environment.NewLine);
    }

    public IReadOnlyList<BrainEntry> LoadEntries()
    {
        if (!EntriesFile.Exists())
            return Array.Empty<BrainEntry>();

        var entries = new List<BrainEntry>();

        foreach (var line in File.ReadLines(EntriesFile.FullName))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var entry = JsonSerializer.Deserialize<BrainEntry>(line, BrainJson.Options);
            if (entry != null)
            {
                entries.Add(entry with
                {
                    People = entry.People ?? Array.Empty<string>(),
                    References = entry.References ?? Array.Empty<string>(),
                    Urls = entry.Urls ?? Array.Empty<string>(),
                    EmailAddresses = entry.EmailAddresses ?? Array.Empty<string>()
                });
            }
        }

        return entries;
    }

    public HashSet<string> LoadPeople()
    {
        if (!PeopleFile.Exists())
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var people = JsonSerializer.Deserialize<string[]>(PeopleFile.ReadAllText(), BrainJson.Options)
            ?? Array.Empty<string>();

        return people.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public void SavePeople(IReadOnlySet<string> people)
    {
        var ordered = people.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        PeopleFile.WriteAllText(JsonSerializer.Serialize(ordered, BrainJson.Options) + Environment.NewLine);
    }
}
