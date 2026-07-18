// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Brain.Cli.Storage;
using DTC.Core.Extensions;
using System.Reflection;
using System.Text.Json;

namespace Brain.Cli;

/// <summary>
/// Checks GitHub Releases for a newer Brain version.
/// </summary>
/// <remarks>
/// The result is cached so launch-time update checks remain fast and unobtrusive.
/// </remarks>
internal sealed class BrainUpdateChecker
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/deanthecoder/Brain/releases/latest";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    private readonly FileInfo m_cacheFile;
    private readonly Version m_currentVersion;
    private readonly Func<DateTimeOffset> m_now;
    private readonly Func<string> m_downloadLatestRelease;

    public BrainUpdateChecker()
        : this(
            BrainPaths.GetHome().GetFile("update-check.json"),
            Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0),
            () => DateTimeOffset.UtcNow,
            DownloadLatestRelease)
    {
    }

    internal BrainUpdateChecker(
        FileInfo cacheFile,
        Version currentVersion,
        Func<DateTimeOffset> now,
        Func<string> downloadLatestRelease)
    {
        m_cacheFile = cacheFile;
        m_currentVersion = currentVersion;
        m_now = now;
        m_downloadLatestRelease = downloadLatestRelease;
    }

    public static void NotifyIfAvailable(string[] args)
    {
        if (args.Any(x => string.Equals(x, "--offline", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(x, "-offline", StringComparison.OrdinalIgnoreCase)))
            return;

        var update = new BrainUpdateChecker().Check();
        if (update != null)
            Console.Error.WriteLine($"Brain {update.LatestVersion} is available (you have {update.CurrentVersion}). Download: {update.Url}");
    }

    public BrainUpdate Check()
    {
        try
        {
            var now = m_now();
            var cache = LoadCache();
            if (cache == null || now - cache.CheckedAtUtc >= CheckInterval)
                cache = Refresh(now);

            if (!TryParseVersion(cache.LatestVersion, out var latestVersion) || latestVersion <= m_currentVersion)
                return null;

            return new BrainUpdate(FormatVersion(m_currentVersion), FormatVersion(latestVersion), cache.Url);
        }
        catch
        {
            // Update checks must never prevent Brain from running.
            return null;
        }
    }

    private UpdateCache Refresh(DateTimeOffset now)
    {
        var cache = new UpdateCache(now, null, null);
        try
        {
            using var document = JsonDocument.Parse(m_downloadLatestRelease());
            var root = document.RootElement;
            cache = new UpdateCache(
                now,
                root.GetProperty("tag_name").GetString(),
                root.GetProperty("html_url").GetString());
        }
        catch
        {
            // Cache failed checks too, avoiding a launch delay on every command while offline.
        }

        SaveCache(cache);
        return cache;
    }

    private UpdateCache LoadCache()
    {
        try
        {
            m_cacheFile.Refresh();
            return m_cacheFile.Exists
                ? JsonSerializer.Deserialize<UpdateCache>(File.ReadAllText(m_cacheFile.FullName))
                : null;
        }
        catch
        {
            return null;
        }
    }

    private void SaveCache(UpdateCache cache)
    {
        m_cacheFile.Directory?.Create();
        File.WriteAllText(m_cacheFile.FullName, JsonSerializer.Serialize(cache) + Environment.NewLine);
    }

    private static string DownloadLatestRelease()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Brain-Update-Checker");
        return client.GetStringAsync(LatestReleaseUrl).GetAwaiter().GetResult();
    }

    private static bool TryParseVersion(string text, out Version version) =>
        Version.TryParse(text?.Trim().TrimStart('v', 'V'), out version);

    private static string FormatVersion(Version version)
    {
        if (version.Revision > 0)
            return version.ToString(4);
        if (version.Build > 0)
            return version.ToString(3);
        return version.ToString(2);
    }

    private sealed record UpdateCache(DateTimeOffset CheckedAtUtc, string LatestVersion, string Url);
}

internal sealed record BrainUpdate(string CurrentVersion, string LatestVersion, string Url);
