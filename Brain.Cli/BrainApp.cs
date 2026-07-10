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
using Brain.Cli.Parsing;
using Brain.Cli.Searching;
using Brain.Cli.Storage;
using Brain.Cli.Syncing;
using DTC.Core.Markdown;
using System.Text.Json;

namespace Brain.Cli;

internal sealed class BrainApp
{
    private static readonly ConsoleMarkdown Markdown = new();

    private BrainStore m_store;
    private readonly Func<BrainStore, IBrainSynchroniser> m_syncFactory;

    public BrainApp()
        : this(null)
    {
    }

    public BrainApp(BrainStore store)
        : this(store, store => new GoogleDriveSync(store))
    {
    }

    internal BrainApp(BrainStore store, Func<BrainStore, IBrainSynchroniser> syncFactory)
    {
        m_store = store;
        m_syncFactory = syncFactory;
    }

    public int Run(string[] args)
    {
        try
        {
            var home = RemoveOption(args, "--home", out args);
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp();
                return 0;
            }

            var json = RemoveFlag(args, "--json", out args);
            var offline = RemoveFlag(args, "--offline", out args);
            if (args.Length == 0)
            {
                PrintHelp();
                return 0;
            }

            m_store ??= new BrainStore(BrainPaths.GetHome(home));

            var command = args[0].ToLowerInvariant();
            var synchroniser = m_syncFactory(m_store);
            var synchroniseAutomatically = !offline && command != "drive" && CanSynchroniseAutomatically(synchroniser);

            if (synchroniseAutomatically)
                TrySynchronise(synchroniser);

            var result = command switch
            {
                "add" => Add(args[1..], json),
                "recall" or "search" or "find" => Recall(args[1..], json),
                "recent" => Recent(args[1..], json),
                "people" => People(json),
                "path" => Path(json),
                "drive" => Drive(args[1..], json),
                _ => Add(args, json)
            };

            if (synchroniseAutomatically && IsCaptureCommand(command))
                TrySynchronise(synchroniser);

            return result;
        }
        catch (BrainUsageException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            PrintHelp();
            return 2;
        }
    }

    private int Add(string[] words, bool json)
    {
        var text = JoinWords(words);
        if (string.IsNullOrWhiteSpace(text))
            throw new BrainUsageException("Nothing to remember.");

        var people = m_store.LoadPeople();
        var analysis = BrainParser.Analyse(text, people);

        var entry = new BrainEntry(
            BrainIds.NewId(),
            DateTimeOffset.Now,
            text,
            analysis.Context,
            analysis.ContextReason,
            analysis.People.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            analysis.References.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            analysis.Urls.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            analysis.EmailAddresses.Order(StringComparer.OrdinalIgnoreCase).ToArray());

        m_store.Append(entry);

        if (json)
        {
            WriteJson(entry);
            return 0;
        }

        Markdown.Write("**Remembered.**");
        Console.WriteLine();

        if (entry.Context != null)
            Console.WriteLine($"Context: {entry.Context} ({entry.ContextReason})");

        if (entry.People.Count > 0)
            Console.WriteLine($"People: {string.Join(", ", entry.People)}");

        if (entry.References.Count > 0)
            Console.WriteLine($"References: {string.Join(", ", entry.References)}");

        if (entry.Urls.Count > 0)
            Console.WriteLine($"URLs: {string.Join(", ", entry.Urls)}");

        if (entry.EmailAddresses.Count > 0)
            Console.WriteLine($"Email addresses: {string.Join(", ", entry.EmailAddresses)}");

        return 0;
    }

    private int Recall(string[] words, bool json)
    {
        var query = JoinWords(words);
        if (string.IsNullOrWhiteSpace(query))
            throw new BrainUsageException("What should I recall?");

        var matches = BrainSearch.Search(m_store.LoadEntries(), query).Take(20).ToArray();

        if (json)
        {
            WriteJson(matches);
            return 0;
        }

        if (matches.Length == 0)
        {
            Console.WriteLine("Nothing found.");
            return 1;
        }

        Markdown.Write(matches.Length == 1 ? "**Found 1 result**" : $"**Found {matches.Length} results**");
        Console.WriteLine();
        Console.WriteLine();

        foreach (var match in matches)
        {
            PrintEntry(match.Entry, match.Score);
            Console.WriteLine();
        }

        return 0;
    }

    private int Recent(string[] args, bool json)
    {
        var count = 10;
        if (args.Length > 0 && !int.TryParse(args[0], out count))
            throw new BrainUsageException("Recent expects an optional number.");

        var entries = m_store.LoadEntries()
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Max(1, count))
            .ToArray();

        if (json)
        {
            WriteJson(entries);
            return 0;
        }

        if (entries.Length == 0)
        {
            Console.WriteLine("Your Brain is empty.");
            return 0;
        }

        foreach (var entry in entries)
        {
            PrintEntry(entry);
            Console.WriteLine();
        }

        return 0;
    }

    private int People(bool json)
    {
        var people = m_store.LoadPeople().Order(StringComparer.OrdinalIgnoreCase).ToArray();

        if (json)
        {
            WriteJson(people);
            return 0;
        }

        if (people.Length == 0)
        {
            Console.WriteLine("No people known yet. Use @Name in a note to teach Brain.");
            return 0;
        }

        foreach (var person in people)
            Console.WriteLine(person);

        return 0;
    }

    private int Path(bool json)
    {
        if (json)
            WriteJson(new { path = m_store.Home.FullName });
        else
            Console.WriteLine(m_store.Home.FullName);

        return 0;
    }

    private int Drive(string[] args, bool json)
    {
        if (args.Length == 0)
            throw new BrainUsageException("Drive expects connect, sync, status, or disconnect.");

        var drive = new GoogleDriveSync(m_store);

        return args[0].ToLowerInvariant() switch
        {
            "connect" => ConnectDrive(drive, args[1..], json),
            "sync" => SyncDrive(drive, json),
            "status" => DriveStatus(drive, json),
            "disconnect" => DisconnectDrive(drive, json),
            _ => throw new BrainUsageException("Drive expects connect, sync, status, or disconnect.")
        };
    }

    private static int ConnectDrive(GoogleDriveSync drive, string[] args, bool json)
    {
        if (args.Length != 1)
        {
            GoogleDriveSync.PrintConnectionInstructions();
            return 2;
        }

        drive.Connect(args[0]);
        if (json)
            WriteJson(new { connected = true });
        else
            Console.WriteLine("Google Drive connected.");

        return 0;
    }

    private static int SyncDrive(GoogleDriveSync drive, bool json)
    {
        var result = drive.Sync();
        if (json)
            WriteJson(result);
        else
            Console.WriteLine($"Google Drive synced. Uploaded {result.Uploaded}, downloaded {result.Downloaded}.");

        return 0;
    }

    private static int DriveStatus(GoogleDriveSync drive, bool json)
    {
        if (json)
            WriteJson(new { connected = drive.IsConnected });
        else
            Console.WriteLine(drive.IsConnected ? "Google Drive is connected." : "Google Drive is not connected.");

        return 0;
    }

    private static int DisconnectDrive(GoogleDriveSync drive, bool json)
    {
        drive.Disconnect();
        if (json)
            WriteJson(new { connected = false });
        else
            Console.WriteLine("Google Drive disconnected.");

        return 0;
    }

    private static bool IsCaptureCommand(string command)
    {
        return command is not "recall" and not "search" and not "find" and not "recent" and not "people" and not "path" and not "drive";
    }

    private static void TrySynchronise(IBrainSynchroniser synchroniser)
    {
        try
        {
            synchroniser.Sync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Google Drive sync pending: {ex.Message}");
        }
    }

    private static bool CanSynchroniseAutomatically(IBrainSynchroniser synchroniser)
    {
        try
        {
            return synchroniser.CanSynchroniseAutomatically;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Google Drive sync pending: {ex.Message}");
            return false;
        }
    }

    private static void PrintEntry(BrainEntry entry, int? score = null)
    {
        var context = entry.Context == null ? string.Empty : $" [{entry.Context}]";
        var scoreText = score == null ? string.Empty : $"  score {score}";

        Markdown.Write($"**{entry.CreatedAt:yyyy-MM-dd HH:mm}{context}{scoreText}**");
        Console.WriteLine();
        Console.WriteLine(entry.Text);

        var extras = new List<string>();
        if (entry.People.Count > 0)
            extras.Add($"people: {string.Join(", ", entry.People)}");

        if (entry.References.Count > 0)
            extras.Add($"refs: {string.Join(", ", entry.References)}");

        if (entry.Urls.Count > 0)
            extras.Add($"urls: {string.Join(", ", entry.Urls)}");

        if (entry.EmailAddresses.Count > 0)
            extras.Add($"emails: {string.Join(", ", entry.EmailAddresses)}");

        if (extras.Count > 0)
        {
            Markdown.Write($"_{string.Join(" | ", extras)}_");
            Console.WriteLine();
        }
    }

    private static void PrintHelp()
    {
        Markdown.Write("""
            # Brain

            A tiny cross-platform tool for remembering things.

            ## Usage

            | Command | Description |
            | --- | --- |
            | `brain <text>` | Remember a thought |
            | `brain add <text>` | Remember a thought |
            | `brain recall <query>` | Search remembered thoughts |
            | `brain recent [count]` | Show recent thoughts |
            | `brain people` | Show known people |
            | `brain path` | Show the storage path |
            | `brain drive connect <credentials.json>` | Connect a Google Drive account |
            | `brain drive sync` | Sync entries with Google Drive |
            | `brain drive status` | Show Google Drive connection status |
            | `brain drive disconnect` | Forget the Google Drive connection |

            `--home <path>` uses a specific storage directory.
            `--offline` skips automatic Google Drive sync.

            ## Conventions

            - `@Erica` tags Erica as a person and remembers the name.
            - `PLAT-123` tags a Jira-style reference and implies work context.
            - `https://example.com` tags a URL.
            - `erica@example.com` tags an email address.
            - `--json` emits machine-readable JSON.

            > PowerShell: quote text or queries containing @, for example `"@Erica"`.

            ## Examples

            ```text
            brain "@Erica says 16 bit support is not needed for PLAT-123"
            brain "My wife loves getting flowers"
            brain recall "16 bit"
            brain recall "@Erica"
            ```
            """);
    }

    private static string JoinWords(string[] words) => string.Join(' ', words).Trim();

    private static bool IsHelp(string arg) => arg is "-h" or "--help" or "help";

    private static bool RemoveFlag(string[] args, string flag, out string[] remaining)
    {
        var found = false;
        var kept = new List<string>();

        foreach (var arg in args)
        {
            if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                found = true;
            else
                kept.Add(arg);
        }

        remaining = kept.ToArray();
        return found;
    }

    private static string RemoveOption(string[] args, string option, out string[] remaining)
    {
        var value = default(string);
        var kept = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], option, StringComparison.OrdinalIgnoreCase))
            {
                kept.Add(args[index]);
                continue;
            }

            if (value != null || ++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
                throw new BrainUsageException($"{option} expects a directory path.");

            value = args[index];
        }

        remaining = kept.ToArray();
        return value;
    }

    private static void WriteJson<T>(T value)
    {
        Console.WriteLine(JsonSerializer.Serialize(value, BrainJson.Options));
    }
}
