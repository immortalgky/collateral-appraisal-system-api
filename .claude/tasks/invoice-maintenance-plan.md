# Invoice Maintenance Backend Plan

## Steps

- [x] Step 1: Domain entities (Invoice, InvoiceItem, IInvoiceRepository)
- [x] Step 2: EF Configurations (InvoiceConfiguration, InvoiceItemConfiguration, DbContext DbSets)
- [x] Step 3: Running number (enum + UoW)
- [x] Step 4: Repository implementation
- [x] Step 5: Register in AppraisalModule.cs
- [x] Step 6: SQL Views
- [x] Step 7: CQRS Queries (GetInvoiceList, GetInvoiceById, GetEligibleAssignments)
- [x] Step 8: CQRS Commands (CreateInvoice, UpdateInvoiceDraft, SubmitInvoice, ApproveInvoice)
- [x] Step 9: Carter Endpoints
- [x] Step 10: EF Migration + dotnet build

## Review

All 10 steps implemented. Build passed with 0 errors.
