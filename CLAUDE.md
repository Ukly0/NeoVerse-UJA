# Working rules — NeoVerse-UJA

## Language

This is a repository touched by many people. Everything added or changed must be in English: code, identifiers, comments, commit messages, branch names, PR titles/descriptions, and any documentation. Do not introduce Spanish (or any other language) into the repo, even in throwaway comments or commit messages, regardless of what language the conversation with me happens in.

## Before committing or pushing

- If there's a substantial change (new feature, fix, refactor — anything beyond a trivial tweak), **ask before committing and before pushing**. Do not move on to another task until you have the go-ahead.
- Never `git push --force` to `main`/`master`/`uja` without explicit confirmation.
- Do not use `--no-verify` or skip hooks unless explicitly requested.

## Branches

Pattern: `<type>/<short-description>` in kebab-case (lowercase, hyphens, no spaces or accents). Never use `main`/`master`/`develop`/`uja` as a prefix.

- `feature/` (or `feat/`) — new functionality → `feature/google-login`
- `fix/` (or `bugfix/`) — bug fix → `fix/save-crash`
- `hotfix/` — urgent production fix
- `refactor/` — restructuring without changing behavior
- `docs/` — documentation
- `chore/` — maintenance (deps, config)
- `release/` — preparing a release → `release/1.2.0`

If there's an issue number, include it: `feature/42-google-login`. In teams, the username is sometimes prepended: `alberto/fix/save-crash`.

## Commits (Conventional Commits)

Format:

```
<type>(optional scope): <description in imperative mood>

[optional body]

[optional footer]
```

Standard types:

- `feat:` — new functionality
- `fix:` — bug fix
- `docs:` — documentation-only changes
- `style:` — formatting (whitespace, commas…), no logic change
- `refactor:` — code change that neither fixes a bug nor adds a feature
- `perf:` — performance improvement
- `test:` — adding or fixing tests
- `build:` — changes to build system or dependencies
- `ci:` — CI configuration changes (GitHub Actions, etc.)
- `chore:` — maintenance that doesn't touch src or tests
- `revert:` — reverts a previous commit

Examples:

```
feat(auth): add Google login
fix: correct hours calculation in the summary
docs: update installation instructions
```

Breaking changes: mark them with `!` or in the footer — `feat(api)!: change response format` / `BREAKING CHANGE: ...`. This maps directly to SemVer (`feat` → MINOR, `fix` → PATCH, breaking → MAJOR).

### The seven rules of a great commit message (Chris Beams)

1. Use the imperative mood: "add", not "added" or "adds". Trick: it should complete the sentence "If applied, this commit will ___".
2. Keep the subject line short (~50 characters) and without a trailing period.
3. Separate subject from body with a blank line.
4. Wrap the body at ~72 characters; explain what and why, not how.
5. One commit = one coherent logical change.
6. Don't mix unrelated types in a single commit (e.g. a `fix` and a `feat` together).
7. Review the diff before committing — the message must reflect exactly what changes.

## Working style with me

- Concise responses, no filler.
- Before any hard-to-reverse action (push, force-push, reset --hard, deleting branches) or anything affecting shared state, ask for explicit confirmation — even if approved once before, don't assume it carries over to next time.
- Don't create documentation (.md) or summaries that weren't requested.
- Don't add abstractions, refactors, or "improvements" beyond what was asked.
