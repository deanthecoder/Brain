// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Brain.Cli.Models;

internal sealed record BrainAnalysis(
    IReadOnlySet<string> People,
    IReadOnlySet<string> ExplicitPeople,
    IReadOnlySet<string> References,
    IReadOnlySet<string> Urls,
    IReadOnlySet<string> EmailAddresses,
    string Context,
    string ContextReason);
