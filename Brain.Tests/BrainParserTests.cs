// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Brain.Cli.Parsing;

namespace Brain.Tests;

[TestFixture]
public sealed class BrainParserTests
{
    [Test]
    public void GivenTaggedWorkNoteCheckMetadataAndContextAreExtracted()
    {
        var analysis = BrainParser.Analyse(
            "@\"Ada Lovelace\" told @Erica to check PLAT-123 at https://example.com/plan). Email ADA@EXAMPLE.COM.",
            new HashSet<string>(["Erica"], StringComparer.OrdinalIgnoreCase));

        Assert.Multiple(() =>
        {
            Assert.That(analysis.People, Is.EquivalentTo(new[] { "Ada Lovelace", "Erica" }));
            Assert.That(analysis.ExplicitPeople, Is.EquivalentTo(new[] { "Ada Lovelace", "Erica" }));
            Assert.That(analysis.References, Is.EquivalentTo(new[] { "PLAT-123" }));
            Assert.That(analysis.Urls, Is.EquivalentTo(new[] { "https://example.com/plan" }));
            Assert.That(analysis.EmailAddresses, Is.EquivalentTo(new[] { "ada@example.com" }));
            Assert.That(analysis.Context, Is.EqualTo("work"));
            Assert.That(analysis.ContextReason, Is.EqualTo("jira-style reference"));
        });
    }

    [Test]
    public void GivenKnownPersonNameCheckOnlyWholeWordsAreRecognised()
    {
        var analysis = BrainParser.Analyse(
            "Alice called, but Alicia did not.",
            new HashSet<string>(["Alice"], StringComparer.OrdinalIgnoreCase));

        Assert.That(analysis.People, Is.EquivalentTo(new[] { "Alice" }));
        Assert.That(analysis.ExplicitPeople, Is.Empty);
    }

    [Test]
    public void GivenTodoTagCheckTodoIsRecognisedWithoutAddingAPerson()
    {
        var analysis = BrainParser.Analyse("@todo Buy flowers for Erica", new HashSet<string>());

        Assert.Multiple(() =>
        {
            Assert.That(analysis.IsTodo, Is.True);
            Assert.That(analysis.People, Is.Empty);
        });
    }
}
