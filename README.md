# Brain

A tiny cross-platform tool for remembering things.

Brain is a small command-line memory stream. It is intentionally minimal: capture a thought quickly, recall it later, and keep the original text intact.

## Current shape

```bash
brain "@Erica tells me I don't have to worry about 16 bit support for PLAT-123"
brain "My wife loves getting flowers"

brain recall "16 bit"
brain recall "@Erica"
brain recent
```

Brain stores entries locally as JSON Lines. This first version avoids AI, sync, SQLite, accounts, and complex organisation. The point is to make the core habit feel good before adding more machinery.

## Build

Brain currently targets .NET 8, matching `DTC.Core`.

```bash
git submodule update --init --recursive
dotnet build Brain.sln
```

## Commands

```text
brain <text>                 Remember a thought
brain add <text>             Remember a thought
brain recall <query>         Search remembered thoughts
brain recent [count]         Show recent thoughts
brain people                 Show known people
brain path                   Show the storage path
```

Add `--json` to emit machine-readable output:

```bash
brain recall PLAT-123 --json
```

In PowerShell, quote text and queries containing `@`, for example `brain recall "@Erica"`.

## Tiny conventions

- `@Erica` marks Erica as a person and remembers that name for future entries.
- Later mentions of `Erica` are tagged automatically once Brain knows the person.
- `PLAT-123`-style references are captured as references and currently imply `work` context.
- Phrases like `my wife` imply `personal` context.
- HTTP(S) and `www.` URLs, plus email addresses, are captured as entry metadata.

These are deterministic hints, not AI guesses. Strong signals are recorded; ambiguous notes are left alone.

## Storage

By default Brain stores data in the DTC.Core per-application settings directory. Use `brain path` to display the exact location for the current platform and installation.

On Windows this is normally:

```text
%APPDATA%\brain
```

## Philosophy

Capturing a thought should be faster than deciding where to put it.
