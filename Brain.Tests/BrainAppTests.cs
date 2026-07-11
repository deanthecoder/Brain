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
using Brain.Cli.Storage;
using Brain.Cli.Syncing;
using DTC.Core;

namespace Brain.Tests;

[TestFixture]
public sealed class BrainAppTests
{
    [Test]
    public void GivenHomeOptionCheckStorageIsCreatedThere()
    {
        using var home = new TempDirectory();

        var result = new BrainApp().Run(["--home", home.FullName, "add", "@Erica remembers PLAT-123"]);
        var store = new BrainStore(home);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Zero);
            Assert.That(store.LoadPeople(), Is.EquivalentTo(new[] { "Erica" }));
            Assert.That(store.LoadEntries().Single().References, Is.EqualTo(new[] { "PLAT-123" }));
        });
    }

    [Test]
    public void GivenSingleDashHomeSwitchCheckStorageIsCreatedThere()
    {
        using var home = new TempDirectory();

        var result = new BrainApp().Run(["-home", home.FullName, "add", "A remembered thought"]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Zero);
            Assert.That(new BrainStore(home).LoadEntries(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void GivenDueConnectedDriveCheckCapturePullsThenPushes()
    {
        using var home = new TempDirectory();
        var synchroniser = new TestSynchroniser();
        var app = new BrainApp(new BrainStore(home), _ => synchroniser);

        var result = app.Run(["A remembered thought"]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Zero);
            Assert.That(synchroniser.PullCount, Is.EqualTo(1));
            Assert.That(synchroniser.PushCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void GivenOfflineFlagCheckAutomaticSyncIsSkipped()
    {
        using var home = new TempDirectory();
        var synchroniser = new TestSynchroniser();
        var app = new BrainApp(new BrainStore(home), _ => synchroniser);

        var result = app.Run(["--offline", "A remembered thought"]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Zero);
            Assert.That(synchroniser.PullCount, Is.Zero);
            Assert.That(synchroniser.PushCount, Is.Zero);
        });
    }

    [Test]
    public void GivenOfflineHelpCheckNothingIsRemembered()
    {
        using var home = new TempDirectory();
        var store = new BrainStore(home);

        var result = new BrainApp(store).Run(["--offline", "--help"]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Zero);
            Assert.That(store.LoadEntries(), Is.Empty);
        });
    }

    [Test]
    public void GivenDriveConnectArgumentCheckUsageFails()
    {
        using var home = new TempDirectory();

        var result = new BrainApp(new BrainStore(home)).Run(["drive", "connect", "credentials.json"]);

        Assert.That(result, Is.EqualTo(2));
    }

    [Test]
    public void GivenRecentlyPulledConnectedDriveCheckRecallDoesNotPullAgain()
    {
        using var home = new TempDirectory();
        var synchroniser = new TestSynchroniser { IsPullDue = false };
        var app = new BrainApp(new BrainStore(home), _ => synchroniser);

        var result = app.Run(["recall", "anything"]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(1));
            Assert.That(synchroniser.PullCount, Is.Zero);
        });
    }

    [Test]
    public void GivenTodoThoughtCheckTodosCollatesIt()
    {
        using var home = new TempDirectory();
        var store = new BrainStore(home);
        var app = new BrainApp(store);

        var addResult = app.Run(["@todo", "Buy", "flowers"]);
        var todos = store.LoadEntries().Where(x => x.IsTodo).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(addResult, Is.Zero);
            Assert.That(todos, Has.Length.EqualTo(1));
            Assert.That(todos[0].People, Is.Empty);
        });
    }

    [Test]
    public void GivenHashtaggedThoughtCheckCleanAndOriginalTextAreStored()
    {
        using var home = new TempDirectory();
        var store = new BrainStore(home);

        var result = new BrainApp(store).Run(["Renew", "the", "domain", "#Admin", "#todo"]);
        var entry = store.LoadEntries().Single();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Zero);
            Assert.That(entry.Text, Is.EqualTo("Renew the domain"));
            Assert.That(entry.OriginalText, Is.EqualTo("Renew the domain #Admin #todo"));
            Assert.That(entry.Tags, Is.EqualTo(new[] { "admin", "todo" }));
            Assert.That(entry.IsTodo, Is.True);
        });
    }

    private sealed class TestSynchroniser : IBrainSynchroniser
    {
        public bool CanSynchroniseAutomatically => true;

        public bool IsPullDue { get; set; } = true;

        public int PullCount { get; private set; }

        public int PushCount { get; private set; }

        public DriveSyncResult Pull()
        {
            PullCount++;
            return new DriveSyncResult(0, 0);
        }

        public DriveSyncResult Push()
        {
            PushCount++;
            return new DriveSyncResult(0, 0);
        }

        public DriveSyncResult Sync()
        {
            Pull();
            Push();
            return new DriveSyncResult(0, 0);
        }
    }
}
