---
name: arcane-docs-style
description: "Use when writing or reviewing TSDoc/JSDoc comments, README files, or docs/<feature>.md pages in an Arcane TypeScript/JavaScript package (ngx libraries, schematics, blueprint.client, cosmowrench, or any other consumer) — covers example-first doc-block style, en-US spelling, README structure (install + snippet, use-when/avoid-when bullets vs. API tables), when to extract a feature section into its own docs file, and mermaid diagram conventions including the pastel color palette."
---

> **Source of truth: this repo (`sketch7/arcane.archives`).** Edit here, then run `npx skills update` in consuming repos. Never edit the installed copy under a consumer repo's `.agents/skills/<name>/` — it's a pulled artifact and gets silently overwritten on the next sync.

# Arcane Docs Style

## Principles

- **Short and direct.** One sentence per concept — no "This function...", no "Note that...".
- **Examples first.** Lead with a code block; explain only what the example can't show.
- **What and why, never how.** Document behavior and intent, not implementation internals.
- **One concern per doc block.** Split multiple scenarios into separate `@example` tags.
- **American English (en-US).** "serialized" not "serialised", "behavior" not "behaviour",
  "visualize" not "visualise".

## TSDoc / JSDoc

````ts
// ✅ Good — concise summary, example-first
/**
 * Selects a slice of state and schedules a re-render when it changes.
 *
 * @example
 * ```ts
 * readonly #count = useSelector(this, () => counterStore, (s) => s.count);
 * ```
 */
export function useSelector(...) {}

// ❌ Bad — verbose, explains internals
/**
 * This function creates a ReactiveController and registers it with the host.
 * It subscribes to the store returned by getStore on each render cycle and
 * calls host.requestUpdate() when the selected value changes...
 */
````

- Summary line: one sentence, trailing period optional.
- `@param` / `@returns`: one line each, skip when the example already makes it obvious.
- Prefer multiple `@example` blocks over one long one.

## Markdown (README / docs)

- Lead with install + a minimal working example — no prose before the first code block.
- Avoid API tables for general exports and hook lists; use a short description plus
  use-when/avoid-when bullets instead. **Exception:** use a table when it genuinely adds clarity —
  e.g. a config-options reference readers need to scan by name/type/default/description.
- Usage snippets are focused and simplified — show only the relevant call site, strip boilerplate.
- Skip filler headers like "Overview", "Introduction", "Background".

### README structure

- Keep the README scoped to the package entry point: install, core feature snippets, links to docs.
- Describe each feature with use-when/avoid-when bullets and a focused snippet — no prose
  paragraphs.
- When a feature outgrows a snippet, extract it to `docs/<feature>.md` and replace the section with
  a one-line link.
- Each `docs/<feature>.md` covers exactly one concern (e.g. `hooks.md`, `host-context.md`) — no
  mixed topics in one file.

### Diagrams

- Use mermaid for every diagram (flow, sequence, ER, class, state).
- For non-trivial diagrams, use the `mermaid-diagrams` skill rather than hand-rolling one.
- Pastel colors with strong contrast; always pair a fill with a stroke color darker than it. One
  concept per diagram.

| Role    | Fill      | Stroke    |
| ------- | --------- | --------- |
| Primary | `#BFDBFE` | `#1E40AF` |
| Success | `#BBF7D0` | `#166534` |
| Warning | `#FED7AA` | `#9A3412` |
| Danger  | `#FECACA` | `#991B1B` |
| Neutral | `#E5E7EB` | `#374151` |
| Accent  | `#DDD6FE` | `#5B21B6` |

## Common mistakes

- Opening a doc block with "This function/component..." instead of stating what it does.
- One giant `@example` covering three scenarios instead of three tagged `@example` blocks.
- Explaining _how_ a hook subscribes/re-renders instead of _what_ it returns and _when_ to use it.
- A table of every export instead of use-when/avoid-when bullets, "just because that's how API
  docs usually look" — reach for the table only when readers need to scan structured columns.
- British spelling (`serialised`, `colour`, `behaviour`) leaking in from habit or a pasted snippet.
- A feature section that's grown past a snippet into multiple paragraphs, still living inline in
  the README instead of being extracted to `docs/<feature>.md`.
