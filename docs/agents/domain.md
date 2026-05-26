# Domain Docs

How the engineering skills should consume this repo's domain documentation.

## Layout

Use a single-context layout:

- `CONTEXT.md` at the repo root: canonical domain glossary, created lazily by `grill-with-docs` when terms are resolved.
- `docs/adr/`: architecture decision records, created lazily when a hard-to-reverse decision needs to be recorded.
- `方案讨论/针对2.0的ECS架构的迭代方案讨论/当前路线/`: existing supporting architecture and task context for EX-GAS 2.0.

If `CONTEXT.md` or `docs/adr/` does not exist yet, proceed silently. Do not create them just because they are missing; create them only when a domain term or architectural decision has actually been resolved.

## Usage rules

- Use `CONTEXT.md` vocabulary for domain concepts in issue titles, tests, refactor proposals, and diagnosis hypotheses.
- Keep `CONTEXT.md` as a glossary only. Do not put implementation plans, temporary notes, or task status there.
- Read relevant ADRs before proposing architecture changes. If a proposal contradicts an ADR, call out the conflict explicitly.
- Use the Chinese architecture notes under `方案讨论/` as supporting evidence, but prefer `CONTEXT.md` for canonical terminology once it exists.
