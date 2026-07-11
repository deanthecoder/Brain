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

internal sealed record BrainEntry(
    string Id,
    DateTimeOffset CreatedAt,
    string Text,
    string Context,
    string ContextReason,
    IReadOnlyList<string> People,
    IReadOnlyList<string> References,
    IReadOnlyList<string> Urls,
    IReadOnlyList<string> EmailAddresses,
    bool IsTodo = false,
    IReadOnlyList<string> Tags = null,
    string OriginalText = null);
