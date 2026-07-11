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

Brain stores each entry as an immutable JSON file. It can optionally sync those files through Google Drive's private application-data folder. The point is to make the core habit feel good before adding more machinery.

## Build

Brain currently targets .NET 8, matching `DTC.Core`.

```bash
git submodule update --init --recursive
dotnet build Brain.sln
```

## Installers

Brain uses `DTC.Installer` to build self-contained Windows and macOS installers. The installers expose `brain` on the command path, so a new terminal can run it without locating the executable manually.

Build installers locally from the repository root:

```bash
python Installer/pack.py
```

Windows produces an Inno Setup installer. macOS produces Apple Silicon and Intel DMGs, each containing a native package installer. Release installers can also be built from the GitHub Actions workflow.

## Commands

```text
brain <text>                 Remember a thought
brain add <text>             Remember a thought
brain recall <query>         Search remembered thoughts
brain recent [count]         Show recent thoughts
brain people                 Show known people
brain todos                  Show remembered todos
brain forget <id>            Forget an entry
brain path                   Show the storage path
brain drive connect         Connect Google Drive
brain drive sync             Sync entries with Google Drive
brain drive status           Show Google Drive connection status
brain drive disconnect       Forget the Google Drive connection
```

Add `--json` or `-json` to emit machine-readable output:

```bash
brain recall PLAT-123 --json
```

Global switches accept either one or two dashes, for example `-home C:\BrainData` or `--offline`.

In PowerShell, quote text and queries containing `@`, for example `brain recall "@Erica"`.

## Tiny conventions

- `@Erica` marks Erica as a person and remembers that name for future entries.
- `@todo` marks a thought as a todo. Use `brain todos` to collate them.
- Later mentions of `Erica` are tagged automatically once Brain knows the person.
- `PLAT-123`-style references are captured as references and currently imply `work` context.
- Phrases like `my wife` imply `personal` context.
- HTTP(S) and `www.` URLs, plus email addresses, are captured as entry metadata.

Human-readable recall, recent, and todo output includes an entry ID. Use it with `brain forget <id>` to remove an entry; forgotten entries are synchronised as tombstones so they are not restored by another machine.

These are deterministic hints, not AI guesses. Strong signals are recorded; ambiguous notes are left alone.

## Storage

By default Brain stores data in the DTC.Core per-application settings directory. Use `brain path` to display the exact location for the current platform and installation.

Each remembered thought is stored as an immutable JSON file in `entries`. Known people are derived from those entries, rather than stored in a separate index.

## Google Drive

`brain drive connect` opens Google's OAuth consent flow and requests only access to Brain's private `appDataFolder`. The Google refresh token is stored in Brain's normal per-user settings file. Brain uses its bundled Desktop OAuth client with PKCE, so users do not need credentials files or environment variables.

Once connected, Brain pulls from Google Drive before reads when its last pull was at least one hour ago, and pushes after every capture. `brain drive sync` remains available for an explicit full sync, while `--offline` skips automatic sync for one command.

On Windows this is normally:

```text
%APPDATA%\brain
```

## Philosophy

Capturing a thought should be faster than deciding where to put it.
