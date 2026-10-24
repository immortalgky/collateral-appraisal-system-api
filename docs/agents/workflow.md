# Workflow: Matt Pocock skills in this repo

How we use `mattpocock-skills` alongside the existing flow (plan in `.claude/tasks/`, subagents implement in an
orca worktree, built-in `/code-review`). Setup files are `issue-tracker.md`, `triage-labels.md` and `domain.md`.

## Not every feature starts with a grill

Answer two questions before you start:

| Requirements clear? | Fits in one session? | Flow |
|---|---|---|
| yes | yes | **Existing flow.** Plan in `.claude/tasks/`, a subagent implements, then `/code-review`. No grill and no issue. |
| no | yes | `/grill-with-docs`, then implement in the same window. No issue. |
| either | no | **Full flow** (below). Creates GitHub issues. |
| foggy, too big to even scope | no | `/wayfinder` first, then join the full flow at `/to-spec`. |

GitHub issues come only from `/to-spec` and `/to-tickets`, which means only from the full flow.

## Full flow

```
1. /grill-with-docs   interview until every branch is decided; writes GLOSSARY.md / docs/adr/
2. /to-spec           conversation → spec issue on GitHub
3. /to-tickets        spec → vertical-slice ticket issues with blocked-by links, labelled ready-for-agent
   (keep 1–3 in ONE context window: no /clear, no /compact)
4. per ticket, frontier first (every blocker closed):
   orca worktree → /implement (session codes test-first with tdd, Matt's code-review) → /code-review loop → test-automator gates
5. before the PR: /mattpocock-skills:code-review main   (Standards + Spec vs the issue)
6. /retro             in the same session, before /clear
```

Adjustments for this repo:
- **`/implement` and `/implement-spec` are exempt from the team-lead rule.** `tdd` only reaches whoever runs the
  skill, and our specialist agents can't load skills. So the `/implement` session writes the code itself, and
  `/implement-spec` uses its own implementer subagents. When either skill calls `code-review`, that means
  **Matt's** `code-review`. The built-in `/code-review` loop still runs before the PR.
- **Two reviews, different jobs.** The built-in `/code-review` hunts bugs. Round 1 runs on the whole branch with
  `high --max-findings all`, later rounds review only the fix diff, and the loop stops when no new critical/high/medium
  appears. Never use `ultra`. Matt's `code-review` runs once before the PR, to check the work against the spec.
- For big work, the grill **replaces** CLAUDE.md rules 1–3 (plan → check-in), and the tickets replace the todo list.

## After the tickets exist

One ticket per round, each in a fresh session:

1. **Pick from the frontier.** Take an open `ready-for-agent` ticket whose blockers are all closed.
   `gh issue list --label ready-for-agent` lists them, or ask "next ticket for #<spec>".
2. **`/clear`.** The ticket is self-contained, so the previous ticket's context isn't needed.
3. **Create an orca worktree from fresh `origin/main`**, with a branch named after the ticket.
   If a blocker is still unmerged, wait for it to merge. Don't stack on its branch: merging a parent with
   `--delete-branch` closes the stacked PR.
4. **Implement.** Start Claude inside the worktree and run `/implement #<ticket>`. That session works test-first
   with `tdd`, runs Matt's `code-review` and commits on the branch.
5. **QA**, all through subagents and skills. Matt's `code-review` already ran inside `/implement`, so now the built-in
   `/code-review` loop runs, then `test-automator` runs the gates.
6. **The user tests, then the PR.** Put `Closes #<ticket>` in the body, so the merge closes the ticket and
   unblocks the next ones.

- Tickets with no edge between them, such as a BE slice and an FE slice, can run in parallel worktrees.
- Don't edit the spec issue by hand mid-build. A change of scope is a new ticket, or a comment on the spec.
- **Option: `/implement-spec #<spec>`** works the whole ticket graph for you.
  Neither skill creates the branch you work on, so create that first:
  1. Create an orca worktree, `feat/<spec#>-<slug>` from `origin/main`, and start a Claude session inside it.
  2. Run `/implement-spec #<spec>` and say "the integration branch is the current branch". Its step 7
     `code-review` is Matt's. Run the built-in `/code-review` loop on the branch afterwards.
  3. It runs implementer subagents in parallel on the frontier. Implementers are the skill's own subagents, not our
     specialist agents. Each gets its own **temporary** worktree, which the skill makes and deletes, and a merger
     subagent merges it into the integration branch.
  4. You test on the orca worktree, then mark the draft PR ready.

  It is not tested here yet, so watch the first run. It covers one repo, so a BE+FE feature needs one run per repo.
- **Option: `/implement #<ticket>`** for one ticket. It works on the **current branch**, so create the orca worktree
  first and start Claude inside it. The session itself does the TDD coding with `tdd`, then runs the skill's
  `code-review`, which is **Matt's** (Standards + Spec against the ticket), then commits. The built-in
  `/code-review` bug-hunt loop runs afterwards, before the PR.

## Other entry points

| Situation | Skill |
|---|---|
| Hard or intermittent bug | `/mattpocock-skills:diagnosing-bugs`. It needs a command that goes red on the bug before it will theorise. |
| Raw issue someone else opened | `/triage`. Never use it on tickets that `/to-tickets` made. |
| A question only a running thing can answer | `/handoff` → `/prototype` (mock in `docs/poc/`) → `/handoff` back |
| The answer sits with the bank or a BA | `/to-questionnaire` |
| An agent reply didn't land | `/wait-what` |
| Codebase upkeep | `/improve-codebase-architecture`. Each candidate becomes a new idea, which starts at step 1. |

## Notes

- `GLOSSARY.md` pays off most. Terms that keep causing mistakes here belong in it: `ValuationDate`,
  `BoundaryLength`, `AssignedTo` vs `CompletedBy`, and the two `CorrelationId`s. Decisions the team must
  share go in `docs/adr/`, not only in Claude's private memory.
- There is no `CODING_STANDARDS.md` yet, so Matt's Standards axis falls back to `CLAUDE.md` and the Fowler smells.
  `/retro` may propose creating the file.
- Keep steps 1–3 under about 150k tokens. If the window gets close, `/compact` at a step boundary.
- The FE repo has its own setup. Run `/setup-matt-pocock-skills` there before creating FE tickets.
- **Models.** Matt's skills pin no model, so subagents inherit the session's. First `/implement-spec` pilot: all
  opus. After that, tell it "implementer subagents use sonnet" and compare review findings, fix rounds and
  follow-up test fixes against the pilot. Reviews stay on opus.
