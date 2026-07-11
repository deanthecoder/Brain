---
name: brain
description: Use the Brain command-line memory store when the user refers to "my brain" or asks Codex to remember, add, ask, check, search, recall, review, improve, tidy, or forget information in their brain. Trigger for phrases such as "ask my brain", "add to my brain", "remember in my brain", "check my brain", "what does my brain know", "review my brain", and "tidy my brain". Do not trigger merely because a software project or general discussion mentions brains.
---

# Brain

Use the installed `brain` CLI as the source of truth. Use `--json` for machine-readable results and preserve the user's original wording when remembering information.

## Check availability

Run `brain path --offline --json` before the first operation when availability is uncertain. If `brain` is unavailable, explain that the Brain CLI must be installed and stop. Do not search the filesystem for private Brain data or bypass the CLI.

Do not add `--offline` to normal operations unless the user requests it. Brain manages its own Google Drive synchronization.

## Remember

Treat phrases such as "add to my brain" and "remember in my brain" as explicit authorization to store the supplied text.

Run:

```text
brain add <verbatim text> --json
```

- Preserve the text after the trigger verbatim unless the user asks for editing.
- Do not add inferred facts or a generated summary as a separate memory.
- Report the returned entry ID concisely.
- If the text to store is ambiguous, ask before writing.

## Recall

Treat "ask my brain", "check my brain", and "what does my brain know" as authorization to query the store.

1. Interpret the question into a small set of useful lexical queries: named people, projects, references, distinctive phrases, and close synonyms.
2. Run `brain recall <query> --json` for each useful query. Avoid broad query explosions.
3. Merge results by `entry.id`, retaining the strongest match score.
4. Answer the user's question from the retrieved memories, distinguishing stored facts from reasonable inference.
5. Include compact supporting entry IDs and dates when they help the user verify or act on the answer.
6. If evidence is absent, weak, outdated, or contradictory, say so. Never fill gaps as though Brain contained the answer.

Use `brain recent <count> --json`, `brain people --json`, `brain tags --json`, or `brain todos --json` when the request specifically concerns recency, people, tags, or todos.

## Review

Treat "review my brain", "improve my brain", and "tidy my brain" as a read-only audit by default.

1. Run `brain recent 100000 --json` to retrieve active entries.
2. Look for exact or likely semantic duplicates, inconsistent person names, stale or unclear todos, contradictions, vague wording, and useful tag suggestions.
3. Present numbered suggestions with affected entry IDs and the proposed result.
4. Do not modify anything until the user explicitly approves numbered suggestions.

Brain recognizes hashtags in remembered text as tags and removes them from the clean display text while retaining the verbatim input. It does not yet expose untag or revise commands. Describe unsupported recommendations without claiming they were applied. For an approved consolidation or revision that can be represented safely with current commands:

1. Add the approved replacement text first with `brain add <text> --json`.
2. Verify that the new entry succeeded and retain its ID.
3. Forget the replaced entry IDs with `brain forget <id> --json`.
4. Report the new ID and forgotten IDs.

Never perform this replacement sequence without explicit approval.

## Forget

Require an exact entry ID or explicit confirmation of the identified entry before running:

```text
brain forget <id> --json
```

Do not choose among ambiguous matches. Report the forgotten ID.

## Privacy and failures

- Reveal retrieved memories only as needed to answer the user's request.
- Do not send Brain content to unrelated tools or services.
- Do not read or edit Brain's JSON files directly.
- On command failure, report the useful error and leave data unchanged.
- Do not retry writes or forget operations unless the command clearly did not complete.
