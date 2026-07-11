[![Twitter Follow](https://img.shields.io/twitter/follow/deanthecoder?style=social)](https://twitter.com/deanthecoder)

# Brain

Remember something now. Find it when you need it.

Brain is a small cross-platform command-line memory store for facts, decisions, people, links, and todos. Capturing a thought takes one command, the original wording stays intact, and optional Google Drive sync keeps the same memories available across your computers.

```bash
brain "@Erica says 16-bit support is not needed for PLAT-123"
brain "@todo Renew the domain next month"
brain "Renew the domain next month #todo #admin"

brain recall "16 bit"
brain recall "@Erica"
brain todos
```

## Why Brain?

- **Fast capture:** write a thought without choosing a document or organizing it first.
- **Simple recall:** search the original text, people, references, links, and todos.
- **Private storage:** memories live as readable JSON files on your computer.
- **Cross-machine sync:** connect Google Drive once on each computer; no API setup or credentials file is required.
- **Codex integration:** say "ask my brain" or "remember in my brain" and let Codex interpret the request.

## Install

Download the installer for your platform from [GitHub Releases](https://github.com/deanthecoder/Brain/releases):

- Windows: `Brain-<version>-win-x64.exe`
- Apple Silicon Mac: `Brain-<version>-osx-arm64.dmg`
- Intel Mac: `Brain-<version>-osx-x64.dmg`

Run the installer, then open a new terminal. The `brain` command will be available on your path.

## Remember and Recall

Remember a thought directly or with the explicit `add` command:

```bash
brain "My wife loves getting flowers"
brain add "@Bob recommended Google Drive for Brain"
```

Search and browse your memories:

```bash
brain recall "flowers"
brain recall "#dean-coding-style" --count 10
brain recent
brain recent 20
brain people
brain tags
brain todos
```

Recall returns every matching entry by default. Add `--count <number>` (or `-count <number>`) when you only want the highest-ranked matches. Output includes each entry's ID; use it to forget something:

```bash
brain forget 88e961720efc3bcf
```

Forgotten entries are synchronized as tombstones, so another computer will not restore them.

Export active memories to a readable JSON file for backup or use with another tool:

```bash
brain export brain-backup.json
```

## Sync with Google Drive

Connect each computer once:

```bash
brain drive connect
```

Brain opens Google's consent screen and requests access only to its private application-data folder. After connection, Brain pushes changes after capture and periodically pulls changes before reads.

Useful sync commands:

```bash
brain drive status
brain drive sync
brain drive disconnect
```

Use `--offline` when a command should not synchronize.

`brain drive status` reports the last successful pull and push, plus the most recent synchronization error when one is pending.

## Use Brain with Codex

The included Codex skill recognizes the phrase **"my brain"**. Once installed, try:

- "Ask my brain what we decided about iOS."
- "Check my brain for anything Bob said about installers."
- "Add to my brain: the next release should be version 0.2."
- "Remember in my brain that Erica owns PLAT-123."
- "Review my brain and suggest duplicates or unclear memories."

Ask Codex to install the public skill directly from this repository:

> Install the Brain skill from `https://github.com/deanthecoder/Brain/tree/main/skills/brain`.

Alternatively, clone the repository and copy `skills/brain` into `~/.codex/skills/brain`. Start a new Codex task after installation so the skill is discovered.

The skill requires the `brain` command to be installed on the same computer as Codex. It uses Brain's JSON output and never edits the storage files directly. Reviews check for duplicates, unclear memories, and inconsistent or missing tags; they remain read-only until you approve specific changes.

## Conventions

Brain derives useful metadata from clear signals while leaving ambiguous notes alone:

- Remembering the same text again, ignoring letter case, returns the existing entry instead of creating a duplicate.
- `@Erica` identifies a person. Later mentions of Erica are recognized automatically.
- `@todo` marks a todo.
- `#tag` categorizes a thought. Tags are stored separately and shown in bold in console results.
- `PLAT-123`-style values are recorded as references.
- Phrases such as `my wife` can imply personal context.
- URLs and email addresses are captured as metadata.

These hints are deterministic; Brain does not use AI to rewrite or reinterpret stored text.

## Command Reference

```text
brain <text>                 Remember a thought
brain add <text>             Remember a thought
brain recall <query> [--count <number>]
                             Search remembered thoughts
brain recent [count]         Show recent thoughts
brain people                 Show known people
brain tags                   Show known tags and entry counts
brain todos                  Show remembered todos
brain forget <id>            Forget an entry
brain export <file>          Export active entries as JSON
brain path                   Show the storage path
brain drive connect          Connect Google Drive
brain drive sync             Synchronize now
brain drive status           Show connection status
brain drive disconnect       Forget the Google connection
```

Global options accept one or two dashes:

- `--json` emits machine-readable JSON.
- `--offline` skips automatic synchronization.
- `--home <path>` uses a different storage directory.

In PowerShell, quote text and queries containing `@`, for example `brain recall "@Erica"`.

## Storage

Run `brain path` to display the storage location for the current installation. Each thought is an immutable JSON file, and known people are derived from those entries rather than maintained in a separate index.

Google Drive synchronization uses Brain's private `appDataFolder`. The refresh token is stored in Brain's per-user settings. Brain uses a bundled Desktop OAuth client with PKCE; users do not need environment variables or Google developer credentials.

## Build from Source

Brain targets .NET 8 and uses the `DTC.Core` and `DTC.Installer` submodules.

```bash
git clone --recurse-submodules https://github.com/deanthecoder/Brain.git
cd Brain
dotnet build Brain.sln
dotnet test Brain.sln
```

Build platform installers from the repository root with:

```bash
python Installer/pack.py
```

## Philosophy

Capturing a thought should be faster than deciding where to put it.

## License

MIT © Dean Edis. See `LICENSE` for details.
