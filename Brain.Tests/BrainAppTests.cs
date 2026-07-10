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
}
