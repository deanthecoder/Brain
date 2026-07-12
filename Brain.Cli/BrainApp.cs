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
    private bool m_hasChanges;

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
            var home = RemoveOption(args, out args, "--home", "-home");
            var json = RemoveFlag(args, out args, "--json", "-json");
            var offline = RemoveFlag(args, out args, "--offline", "-offline");
            if (args.Length == 0 || args.Any(IsHelp))
            {
                PrintHelp();
                return 0;
            }

            m_store ??= new BrainStore(BrainPaths.GetHome(home));
            m_hasChanges = false;

            var command = args[0].ToLowerInvariant();
            var synchroniser = m_syncFactory(m_store);
            var synchroniseAutomatically = !offline && command != "drive" && CanSynchroniseAutomatically(synchroniser);

            if (synchroniseAutomatically && synchroniser.IsPullDue)
                TryPull(synchroniser);

            var result = command switch
            {
                "add" => Add(args[1..], json),
                "recall" or "search" or "find" => Recall(args[1..], json),
                "recent" => Recent(args[1..], json),
                "people" => People(json),
                "tags" => Tags(json),
                "attachments" => Attachments(args[1..], json),
                "todo" or "todos" => Todos(json),
                "forget" => Forget(args[1..], json),
                "extract" => Extract(args[1..], json),
                "export" => Export(args[1..], json),
                "path" => Path(json),
                "drive" => Drive(args[1..], json),
                _ => Add(args, json)
            };

            if (synchroniseAutomatically && m_hasChanges)
                TryPush(synchroniser);

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

        var existingEntry = m_store.FindByOriginalText(text);
        if (existingEntry != null)
        {
            if (json)
                WriteJson(existingEntry);
            else
            {
                Markdown.Write($"**Already remembered.**  id {existingEntry.Id}");
                Console.WriteLine();
            }

            return 0;
        }

        var fileAnalysis = BrainFileParser.Analyse(text);
        if (string.IsNullOrWhiteSpace(fileAnalysis.Text))
            throw new BrainUsageException("An attachment needs some text to describe it.");

        var attachments = fileAnalysis.Files.Select(m_store.StoreAttachment).ToArray();
        var people = m_store.LoadPeople();
        var analysis = BrainParser.Analyse(fileAnalysis.Text, people);

        var entry = new BrainEntry(
            BrainIds.NewId(),
            DateTimeOffset.Now,
            analysis.Text,
            analysis.Context,
            analysis.ContextReason,
            analysis.People.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            analysis.References.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            analysis.Urls.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            analysis.EmailAddresses.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            analysis.IsTodo,
            analysis.Tags.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            text,
            attachments);

        m_store.Append(entry);
        m_hasChanges = true;

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

        if (entry.Tags.Count > 0)
        {
            Markdown.Write($"Tags: {FormatTags(entry.Tags)}");
            Console.WriteLine();
        }

        if (entry.IsTodo)
            Console.WriteLine("Todo: yes");

        foreach (var attachment in entry.Attachments)
            Console.WriteLine($"Attachment: {attachment.FileName} ({FormatSize(attachment.Size)})");

        return 0;
    }

    private int Recall(string[] words, bool json)
    {
        var countText = RemoveOption(words, out words, "--count", "-count");
        int? count = null;
        if (countText != null)
        {
            if (!int.TryParse(countText, out var parsedCount) || parsedCount < 1)
                throw new BrainUsageException("Recall count must be a positive number.");

            count = parsedCount;
        }

        var query = JoinWords(words);
        if (string.IsNullOrWhiteSpace(query))
            throw new BrainUsageException("What should I recall?");

        var search = BrainSearch.Search(m_store.LoadEntries(), query);
        var matches = (count == null ? search : search.Take(count.Value)).ToArray();

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

    private int Todos(bool json)
    {
        var entries = m_store.LoadEntries()
            .Where(x => x.IsTodo)
            .OrderByDescending(x => x.CreatedAt)
            .ToArray();

        if (json)
        {
            WriteJson(entries);
            return 0;
        }

        if (entries.Length == 0)
        {
            Console.WriteLine("No todos.");
            return 0;
        }

        foreach (var entry in entries)
        {
            PrintEntry(entry);
            Console.WriteLine();
        }

        return 0;
    }

    private int Tags(bool json)
    {
        var tags = m_store.LoadTagCounts()
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => new TagSummary(x.Key, x.Value))
            .ToArray();

        if (json)
        {
            WriteJson(tags);
            return 0;
        }

        if (tags.Length == 0)
        {
            Console.WriteLine("No tags known yet. Use #tag in a note to add one.");
            return 0;
        }

        foreach (var line in FormatTagTable(tags))
            Console.WriteLine(line);

        return 0;
    }

    private int Forget(string[] args, bool json)
    {
        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
            throw new BrainUsageException("Forget expects an entry ID.");

        var id = args[0].Trim();
        m_store.Forget(id);
        m_hasChanges = true;

        if (json)
            WriteJson(new { id, forgotten = true });
        else
            Console.WriteLine($"Forgotten {id}.");

        return 0;
    }

    private int Attachments(string[] args, bool json)
    {
        if (args.Length > 0)
            return PruneAttachments(args, json);

        var attachments = m_store.LoadAttachmentStatuses(DateTimeOffset.UtcNow);
        if (json)
        {
            WriteJson(attachments);
            return 0;
        }

        if (attachments.Count == 0)
        {
            Console.WriteLine("No attachments.");
            return 0;
        }

        foreach (var attachment in attachments)
        {
            var status = attachment.ReferenceCount > 0
                ? $"{attachment.ReferenceCount} {(attachment.ReferenceCount == 1 ? "reference" : "references")}"
                : $"orphaned; prunes {attachment.PruneAt:yyyy-MM-dd}";
            Console.WriteLine($"{attachment.FileName}  {FormatSize(attachment.Size)}  {status}  {attachment.Hash[..12]}");
        }

        return 0;
    }

    private int PruneAttachments(string[] args, bool json)
    {
        var dryRun = RemoveFlag(args, out args, "--dry-run", "-dry-run");
        if (args.Length != 1 || !string.Equals(args[0], "prune", StringComparison.OrdinalIgnoreCase))
            throw new BrainUsageException("Attachments expects prune with an optional --dry-run switch.");

        var drive = new GoogleDriveSync(m_store);
        var attachments = drive.PruneAttachments(dryRun);

        if (json)
            WriteJson(new { dryRun, count = attachments.Count, attachments });
        else if (attachments.Count == 0)
            Console.WriteLine("No attachments are ready to prune.");
        else
            Console.WriteLine(dryRun
                ? $"Would prune {attachments.Count} {(attachments.Count == 1 ? "attachment" : "attachments")}."
                : $"Pruned {attachments.Count} {(attachments.Count == 1 ? "attachment" : "attachments")}.");

        return 0;
    }

    private int Extract(string[] args, bool json)
    {
        var destinationText = RemoveOption(args, out args, "--to", "-to");
        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
            throw new BrainUsageException("Extract expects an entry ID and optional --to folder.");

        var entry = m_store.FindEntry(args[0].Trim());
        if (entry == null)
            throw new BrainUsageException($"Entry not found: {args[0].Trim()}");

        var destination = new DirectoryInfo(System.IO.Path.GetFullPath(destinationText ?? Directory.GetCurrentDirectory()));
        destination.Create();
        var files = entry.Attachments.Select(attachment => new
        {
            Attachment = attachment,
            Source = m_store.GetAttachmentFile(attachment.Hash),
            Destination = new FileInfo(System.IO.Path.Combine(destination.FullName, System.IO.Path.GetFileName(attachment.FileName)))
        }).ToArray();

        if (files.GroupBy(x => x.Destination.FullName, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new BrainUsageException("Two attachments have the same destination file name.");

        foreach (var file in files)
        {
            file.Source.Refresh();
            if (!file.Source.Exists)
                throw new BrainUsageException($"Attachment is not available locally: {file.Attachment.FileName}");
            if (file.Destination.Exists)
                throw new BrainUsageException($"Destination file already exists: {file.Destination.FullName}");
        }

        foreach (var file in files)
            file.Source.CopyTo(file.Destination.FullName);

        if (json)
            WriteJson(files.Select(x => new { path = x.Destination.FullName, attachment = x.Attachment }).ToArray());
        else if (files.Length == 0)
            Console.WriteLine("That entry has no attachments.");
        else
            foreach (var file in files)
                Console.WriteLine($"Extracted {file.Attachment.FileName} to {file.Destination.FullName}.");

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

    private int Export(string[] args, bool json)
    {
        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
            throw new BrainUsageException("Export expects a destination file.");

        var destination = new FileInfo(System.IO.Path.GetFullPath(args[0]));
        var count = m_store.Export(destination);

        if (json)
            WriteJson(new { path = destination.FullName, count });
        else
            Console.WriteLine($"Exported {count} {(count == 1 ? "entry" : "entries")} to {destination.FullName}.");

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
        if (args.Length != 0)
            throw new BrainUsageException("Drive connect does not expect additional arguments.");

        drive.Connect();
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
        var status = drive.Status;
        if (json)
            WriteJson(status);
        else
        {
            Console.WriteLine(status.Connected ? "Google Drive is connected." : "Google Drive is not connected.");
            Console.WriteLine($"Last pull: {FormatTimestamp(status.LastPulledAtUtc)}");
            Console.WriteLine($"Last push: {FormatTimestamp(status.LastPushedAtUtc)}");
            if (status.LastError != null)
                Console.WriteLine($"Last error: {status.LastErrorOperation} at {FormatTimestamp(status.LastErrorAtUtc)}: {status.LastError}");
        }

        return 0;
    }

    private static string FormatTimestamp(DateTime? timestamp) => timestamp?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "never";

    private static int DisconnectDrive(GoogleDriveSync drive, bool json)
    {
        drive.Disconnect();
        if (json)
            WriteJson(new { connected = false });
        else
            Console.WriteLine("Google Drive disconnected.");

        return 0;
    }

    private static void TryPull(IBrainSynchroniser synchroniser)
    {
        try
        {
            synchroniser.Pull();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Google Drive sync pending: {ex.Message}");
        }
    }

    private static void TryPush(IBrainSynchroniser synchroniser)
    {
        try
        {
            synchroniser.Push();
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

        Markdown.Write($"**{entry.CreatedAt:yyyy-MM-dd HH:mm}{context}{scoreText}  id {entry.Id}**");
        Console.WriteLine();
        Console.Write(entry.Text);
        if (entry.Tags.Count > 0)
        {
            Console.Write(' ');
            Markdown.Write(FormatTags(entry.Tags));
        }
        Console.WriteLine();

        var extras = new List<string>();
        if (entry.People.Count > 0)
            extras.Add($"people: {string.Join(", ", entry.People)}");

        if (entry.References.Count > 0)
            extras.Add($"refs: {string.Join(", ", entry.References)}");

        if (entry.Urls.Count > 0)
            extras.Add($"urls: {string.Join(", ", entry.Urls)}");

        if (entry.EmailAddresses.Count > 0)
            extras.Add($"emails: {string.Join(", ", entry.EmailAddresses)}");

        if (entry.Attachments.Count > 0)
            extras.Add($"attachments: {string.Join(", ", entry.Attachments.Select(x => $"{x.FileName} ({FormatSize(x.Size)})"))}");

        if (extras.Count > 0)
        {
            Markdown.Write($"_{string.Join(" | ", extras)}_");
            Console.WriteLine();
        }
    }

    private static string FormatTags(IEnumerable<string> tags) => string.Join(' ', tags.Select(x => $"**#{x}**"));

    private static string FormatSize(long size) => size < 1024 * 1024
        ? $"{Math.Max(1, size / 1024)} KB"
        : $"{size / (1024d * 1024d):0.#} MB";

    internal static IReadOnlyList<string> FormatTagTable(IReadOnlyList<TagSummary> tags, int maximumWidth = 80, int spacing = 5)
    {
        var values = tags.Select(x => $"{x.Tag} ({x.Count})").ToArray();

        for (var rowCount = 1; rowCount <= values.Length; rowCount++)
        {
            var columnCount = (values.Length + rowCount - 1) / rowCount;
            var columnWidths = Enumerable.Range(0, columnCount)
                .Select(column => values.Skip(column * rowCount).Take(rowCount).Max(x => x.Length))
                .ToArray();

            if (columnWidths.Sum() + spacing * (columnCount - 1) > maximumWidth)
                continue;

            return Enumerable.Range(0, rowCount)
                .Select(row => string.Concat(Enumerable.Range(0, columnCount).Select(column =>
                {
                    var index = column * rowCount + row;
                    if (index >= values.Length)
                        return string.Empty;

                    var isLastValueInRow = index + rowCount >= values.Length;
                    return isLastValueInRow ? values[index] : values[index].PadRight(columnWidths[column] + spacing);
                })).TrimEnd())
                .ToArray();
        }

        return values;
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
            | `brain recall <query> [--count <number>]` | Search remembered thoughts |
            | `brain recent [count]` | Show recent thoughts |
            | `brain people` | Show known people |
            | `brain tags` | Show known tags and entry counts |
            | `brain attachments` | Show stored attachments |
            | `brain attachments prune [--dry-run]` | Prune attachments orphaned for 30 days |
            | `brain todos` | Show remembered todos |
            | `brain forget <id>` | Forget an entry |
            | `brain extract <id> [--to <folder>]` | Extract an entry's attachments |
            | `brain export <file>` | Export active entries as JSON |
            | `brain path` | Show the storage path |
            | `brain drive connect` | Connect a Google Drive account |
            | `brain drive sync` | Sync entries with Google Drive |
            | `brain drive status` | Show Google Drive connection status |
            | `brain drive disconnect` | Forget the Google Drive connection |

            `--home <path>` or `-home <path>` uses a specific storage directory.
            `--json` or `-json` emits machine-readable JSON.
            `--offline` or `-offline` skips automatic Google Drive sync.
            Recall returns every match unless `--count <number>` or `-count <number>` is supplied.

            ## Conventions

            - `@Erica` tags Erica as a person and remembers the name.
            - `@todo` marks a thought as a todo.
            - `@file:<path>` copies and attaches a file; quote paths containing spaces.
            - `#tag` categorizes a thought; hashtags are removed from its displayed text.
            - URLs are automatically tagged `url`.
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

    internal sealed record TagSummary(string Tag, int Count);

    private static bool IsHelp(string arg) => arg is "-h" or "--help" or "help";

    private static bool RemoveFlag(string[] args, out string[] remaining, params string[] flags)
    {
        var found = false;
        var kept = new List<string>();

        foreach (var arg in args)
        {
            if (flags.Contains(arg, StringComparer.OrdinalIgnoreCase))
                found = true;
            else
                kept.Add(arg);
        }

        remaining = kept.ToArray();
        return found;
    }

    private static string RemoveOption(string[] args, out string[] remaining, params string[] options)
    {
        var value = default(string);
        var kept = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            if (!options.Contains(args[index], StringComparer.OrdinalIgnoreCase))
            {
                kept.Add(args[index]);
                continue;
            }

            if (value != null || ++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
                throw new BrainUsageException($"{options[0]} expects a value.");

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
