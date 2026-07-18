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
using DTC.Core;

namespace Brain.Tests;

[TestFixture]
public sealed class BrainUpdateCheckerTests
{
    [Test]
    public void GivenNewerReleaseCheckUpdateIsReturned()
    {
        using var home = new TempDirectory();
        var checker = CreateChecker(home, new Version(0, 1), "v0.2");

        var update = checker.Check();

        Assert.Multiple(() =>
        {
            Assert.That(update, Is.Not.Null);
            Assert.That(update.CurrentVersion, Is.EqualTo("0.1"));
            Assert.That(update.LatestVersion, Is.EqualTo("0.2"));
            Assert.That(update.Url, Is.EqualTo("https://example.com/release"));
        });
    }

    [Test]
    public void GivenCurrentReleaseCheckNoUpdateIsReturned()
    {
        using var home = new TempDirectory();
        var checker = CreateChecker(home, new Version(0, 2), "v0.2");

        Assert.That(checker.Check(), Is.Null);
    }

    [Test]
    public void GivenFreshCacheCheckReleaseIsNotDownloadedAgain()
    {
        using var home = new TempDirectory();
        var downloadCount = 0;
        var now = new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero);
        var cacheFile = new FileInfo(Path.Combine(home.FullName, "update-check.json"));
        var checker = new BrainUpdateChecker(cacheFile, new Version(0, 1), () => now, () =>
        {
            downloadCount++;
            return ReleaseJson("v0.2");
        });

        checker.Check();
        checker.Check();

        Assert.That(downloadCount, Is.EqualTo(1));
    }

    [Test]
    public void GivenReleaseCheckFailureCheckNoUpdateIsReturned()
    {
        using var home = new TempDirectory();
        var checker = new BrainUpdateChecker(
            new FileInfo(Path.Combine(home.FullName, "update-check.json")),
            new Version(0, 1),
            () => DateTimeOffset.UtcNow,
            () => throw new HttpRequestException("Unavailable"));

        Assert.That(checker.Check(), Is.Null);
    }

    private static BrainUpdateChecker CreateChecker(DirectoryInfo home, Version currentVersion, string latestVersion) =>
        new(
            new FileInfo(Path.Combine(home.FullName, "update-check.json")),
            currentVersion,
            () => new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero),
            () => ReleaseJson(latestVersion));

    private static string ReleaseJson(string version) =>
        $$"""{"tag_name":"{{version}}","html_url":"https://example.com/release"}""";
}
