# Pinned "customer name" search: every customer, not the one the view picked

## The bug

`vw_AppraisalList.CustomerName` is `OUTER APPLY (SELECT TOP 1 Name FROM request.RequestCustomers
WHERE RequestId = a.RequestId)` — **no ORDER BY**, so it surfaces one arbitrary customer per
request. The pinned search scope filtered on that column, so an appraisal whose match is the second
customer was invisible. The all-fields search never had the hole: it reads
`request.RequestCustomers` directly.

Dev data: 105,492 requests carry one customer, **25 carry two** — small, but the failure is total
for those, and silent.

## The fix

`AppraisalFilterBuilder`: the CustomerName predicate is now

```sql
RequestId IN (SELECT c.RequestId FROM request.RequestCustomers c
              WHERE c.Name LIKE '%' + @CustomerName + '%' ESCAPE '\')
```

- `RequestId IN (…)`, not `EXISTS`: the WHERE clause is shared between the view (`v`) and the base
  table (`t`), so there is no alias to qualify an outer column with — and `RequestCustomers` has its
  own `RequestId`, which an unqualified correlation would bind to, making the predicate always true.
- `RequiresView` is now **false** for this filter: `RequestId` is on `appraisal.Appraisals`, so the
  count runs off the base table.

Also in the same pass: a leading `*` typed into the three pinned scopes is stripped
(`StripWidenMarker`). The marker only means something to the all-fields search, which is
prefix-matched; the pinned scopes are already `LIKE '%x%'` and the value is escaped, so a `*` left
in place was searched for literally and found nothing. A term that is only markers filters nothing
rather than becoming `LIKE '%%'`.

## Measured — dev database, 105,475 appraisals

Count query, `SET STATISTICS IO/TIME`, second run of each:

| Term | | Matches | CPU | RequestCustomers scans | logical reads |
|---|---|---|---|---|---|
| `Jane` | old | **0** | 568 ms | 105,475 | 337,485 |
| `Jane` | new | **22** | 133 ms | 1 | 921 |
| `ศรีมงคล` | old | 1 | 529 ms | 105,475 | 337,488 |
| `ศรีมงคล` | new | 1 | 125 ms | 1 | 921 |
| `Doe` | old | 22 | 557 ms | 105,475 | 337,488 |
| `Doe` | new | 22 | 113 ms | 1 | 921 |

Faster as well as correct: the old shape made the view's OUTER APPLY run once per appraisal.
CPU is the number that matters here — elapsed is lower on the old shape only because it goes
parallel (`CPU 568 / elapsed 46`), which is throughput spent, not saved.

A third shape was measured and rejected: the derived-table-joined-in-front + `FORCE ORDER` pattern
the free-text search uses (171 ms CPU, 7,270 reads on Appraisals vs 6,472). That pattern exists
because a 17-arm UNION gets re-run per row as a sub-predicate; a single seekable subquery does not
have that problem, so it does not need the machinery.

## Tests

`AppraisalFilterBuilderTests`: CustomerName moved from `ViewOnlyFilters` to `BaseTableFilters`,
`Searching_customer_or_request_number_needs_the_view` split (RequestNumber still does),
`Customer_search_covers_every_customer_on_the_request` pins the table it reads, plus a theory for
the `*` stripping and one for a markers-only term. 77 pass.

## Making the mismatch legible: "+N" on the customer column

Searching a name the row does not display would read as a wrong result, so the row now says there
are more customers. `vw_AppraisalList` gained

```sql
(SELECT COUNT(*) FROM request.RequestCustomers rc WHERE rc.RequestId = a.RequestId) AS CustomerCount
```

and `AppraisalDto.CustomerCount` carries it. The list renders `+1` next to the name with a tooltip.

Cost is confined to the page: the paging statement selects only `Id`, so the optimizer prunes the
subquery entirely (85 ms CPU, unchanged); the enrichment statement computes it for 25 rows —
RequestCustomers scan count 50, 151 logical reads, **4 ms CPU**.

Still open, deliberately: the view's TOP 1 has no ORDER BY, so WHICH customer is displayed is
arbitrary and can change between runs. Fixing that means an ORDER BY in the view (a stable choice,
e.g. by created order) — worth doing, but it is a separate change with its own regression surface.

## Making the displayed customer deterministic — measured before and after

The view's customer pick was `SELECT TOP 1 Name … WHERE RequestId = a.RequestId` with **no
ORDER BY**, so which customer is displayed could change between executions — and the Customer column
is sortable, which made that a *sort key* that can shift under the user. The `+N` badge is what made
it visible: a row can read "John +1" while it matched on Jane. Now `ORDER BY Id` (bigint identity =
the customer entered first).

The risk was that an ordered `TOP 1` forces a sort per outer row. It does not:
`IX_RequestCustomer_RequestId` is non-unique, so its leaf carries the clustered key `Id` and the seek
already arrives in `Id` order.

Same four statements, second run of each, dev database (105,475 appraisals):

| Query | before | after |
|---|---|---|
| enrichment, `SELECT *` for 25 ids | 3 ms CPU · scan 50 · 151 reads | 3 ms CPU · scan 50 · 151 reads |
| id page, `ORDER BY CreatedAt DESC` | 67 ms CPU | 63 ms CPU |
| customer search count (`Jane`) | 139 ms CPU · 977 reads · 22 rows | 130 ms CPU · 977 reads · 22 rows |
| id page, `ORDER BY CustomerName` (worst case — the APPLY runs for every row) | 572 ms CPU · 337,488 reads | 595 ms CPU · 337,488 reads |

Identical logical reads everywhere; the CPU differences are run-to-run noise (±4%). No index was
needed.

Determinism confirmed separately: the 25 multi-customer rows were queried three times and the result
hashed — all three hashes identical.

Worth knowing, not fixed here: sorting the list by Customer costs ~600 ms CPU because the view's
APPLY has to run for all 105k rows to build the sort key. That is unchanged by this work and predates
it; it would want a computed/persisted column or an indexed view to fix properly.
