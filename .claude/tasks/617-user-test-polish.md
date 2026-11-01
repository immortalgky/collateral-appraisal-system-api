# #617 user-test polish (2026-11-01, uncommitted in both admin-email worktrees)

Changes made while the user tested the integration branch end to end. BE + FE, no migration.

## What changed
- Preview shows real recipients: `companyName`, `ccPeople`, role chips expanded to addresses when the people are known.
- To: `company <email>`. No company email → danger chip, "มอบหมายและส่ง" disabled until an extra To exists.
- Template: one `<p>`, `<br>` lines, blank line after เรียน and before จึงเรียน, no space before ":", "หมายเลขประเมิน".
- Remark keeps line breaks and indentation.
- Upload `documentType`/`documentCategory` ≤ 10 chars (`EMAIL_ATT` / `email_att`).
- Sent view: shell font, attachments open/download.
- Company/staff selected cards restyled (left accent, line 2 with "·", stars, email). Staff card shows the real workload (`activeTaskCount`) and the user record.
- Assignment type is a SegmentedChoice. Admin page cards have no header divider.
- Liftoff overlay on assign.
- Compose/resend/view-sent moved to a right drawer (SlideOverPanel, ~720px).
- To/Cc recipient search (`@` browse, search, loading/empty states).
- Company search matches email + contact person.
- TaskLayout lock banner i18n.

## Settled decisions (do not re-raise)
- Role chips are frozen into addresses at compose ("แตก chip เป็นรายคน", user 11-01). Roster drift between assign and send is accepted.
- × kept on both cards. "โทร." stays as stored. Stars stay on line 2 (moving them next to the name was tried and rejected).
- Search still matches the registration number, which is hidden on the row (accepted).
- `@` browse fetches all active companies (small list, no paging endpoint).
- Drawer footer is sticky inside the body, not SlideOverPanel `footer` (the form state lives inside).
- Altitude refactor of the role lookups returning name+email: declined.
- Workload SQL duplicated vs WorkloadBasedAssigneeSelector: declined for now.
- 2100-parameter limit on eligible-assignees IN lists: declined (small pools, existing pattern).
- `{{…}}` typed in the remark: declined edge case.
- Preview shows the real keyer / follow-up / company whenever the row knows them, whether the admin picked them or the system resolved them (user 11-01). Safe because the FE always saves the full-replace draft before the preview; that contract is documented in ComposeAsync and pinned by an FE test. Review findings about "stale row after route-back" are answered by this contract.
- GetNamesByEmailAsync compares on the trimmed column (correctness over seek). storedEmails duplication is kept. A handover with a junk-only company email fails as "no recipient" without naming the address (diagnostics only).
- Remark per-rune string allocation: declined nit.

## Open (asked, no answer yet)
- Hint text under the assignment-type toggle; mock for the lock banner.
- Matt review: key-in refuses every Pending row; stale Pending row on EXT company-no-match; resend from an empty Failed row.
- Filter expired temporary accounts out of the recipient search?
- PRD risk: open cases on workflow versions without the EXTO path complete silently when EXTO is chosen (seen on 69000203, v2).

- Phone in the by-username card: bank staff only, as the user asked for the staff card. Quoted display names with commas: declined. TAG characters dropped (anti-smuggling), even though emoji subdivision flags lose them.

## Review
- /code-review on the uncommitted diff, high:
  - FE: r1 16, r2 6, r3 1 (fixed, loop closed).
  - BE: r1 13, r2 8, r3 10, r4 7, r5 6, r6 0 medium+ (loop closed).
- Notable fixes:
  - one address rule for preview and send (`PlainAddresses`; bare or `Name <addr>` only, never rewritten);
  - a known role resolving to nobody keeps its token;
  - by-username is a slim contact DTO: bank-staff targets only, company callers 403, `!IsSystem`;
  - remark keeps line breaks and spacing and drops invisible characters (ZWJ/ZWNJ kept);
  - the administration hooks' query keys no longer collide with userManagement's `['users', id]`;
  - recipient search `pending` means the term changed;
  - EXTO liftoff names the keyer;
  - the resend header comes from the saved assignment.
- Low notes left: a keyer with no AspNetUsers row gives a null name, so a resend refuses `{{responsible}}`; no test for a padded username.
