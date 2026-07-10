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
brain drive connect <credentials.json>
                            Connect Google Drive
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

`brain drive connect` opens Google's Desktop OAuth consent flow and requests only access to Brain's private `appDataFolder`. The connection details and Google refresh token are stored in Brain's normal per-user settings file.

One Google setup is needed for each Google project, not for each Brain installation:

1. Open [Google Cloud credentials](https://console.cloud.google.com/apis/credentials), then create or select a project.
2. Enable the [Google Drive API](https://console.cloud.google.com/apis/library/drive.googleapis.com).
3. Create an OAuth client ID with application type **Desktop app**.
4. Download its JSON file and run `brain drive connect /path/to/client_secret_....json`.

Google opens a browser for the actual account sign-in after this command. The credentials JSON is read once; Brain stores the required local connection details and does not use environment variables.

Once connected, Brain pulls from Google Drive before reads when its last pull was at least one hour ago, and pushes after every capture. `brain drive sync` remains available for an explicit full sync, while `--offline` skips automatic sync for one command.

On Windows this is normally:

```text
%APPDATA%\brain
```

## Philosophy

Capturing a thought should be faster than deciding where to put it.
