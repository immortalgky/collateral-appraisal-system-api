# Track 1: Quotation-Driven Appraisal Assignment — Domain + Application Layer

## Todo

- [x] Read plan and all existing files
- [x] A. Domain: Update QuotationRequest.cs (new fields, methods, status vocabulary)
- [x] B. Domain: Update CompanyQuotation.cs (new fields, methods)
- [x] C. EF: Update QuotationConfiguration.cs (new columns, indexes, concurrency token)
- [x] D. App: QuotationAccessPolicy.cs
- [x] E. App: StartQuotationFromTask command/handler/endpoint
- [x] F. App: CloseQuotation command/handler/endpoint
- [x] G. App: ShortlistQuotation + UnshortlistQuotation commands/handlers/endpoints
- [x] H. App: SendShortlistToRm command/handler/endpoint
- [x] I. App: RecallShortlist command/handler/endpoint
- [x] J. App: PickTentativeWinner command/handler/endpoint
- [x] K. App: OpenNegotiation command/handler/endpoint
- [x] L. App: RespondNegotiation command/handler/endpoint
- [x] M. App: RejectTentativeWinner command/handler/endpoint
- [x] N. App: FinalizeQuotation command/handler/endpoint
- [x] O. App: CancelQuotation command/handler/endpoint
- [x] P. App: SubmitQuotation (PUT /quotations/{id}/companies/{companyId}/submit) command/handler/endpoint
- [x] Q. Update IQuotationRepository (add GetByIdWithNegotiationsAsync)
- [x] R. Update QuotationRepository (implement new method)
- [x] S. Build check — 0 errors

## Review

Track 1 complete. Full solution builds with 0 errors (28 projects, 433 pre-existing warnings).

### Files Created
- QuotationAccessPolicy.cs (Shared policy helper)
- StartQuotationFromTask/ (5 files)
- CloseQuotation/ (3 files)
- ShortlistQuotation/ (3 files)
- UnshortlistQuotation/ (3 files)
- SendShortlistToRm/ (3 files)
- RecallShortlist/ (3 files)
- PickTentativeWinner/ (4 files)
- OpenNegotiation/ (4 files)
- RespondNegotiation/ (4 files)
- RejectTentativeWinner/ (4 files)
- FinalizeQuotation/ (4 files)
- CancelQuotation/ (4 files)
- SubmitQuotation/ (4 files)

### Files Modified
- QuotationRequest.cs (new IBG fields, new methods, extended status vocabulary)
- CompanyQuotation.cs (IsShortlisted, NegotiationRounds, OriginalQuotedPrice, CurrentNegotiatedPrice, new methods)
- QuotationConfiguration.cs (new columns, indexes, concurrency token)
- IQuotationRepository.cs (GetByIdWithNegotiationsAsync added)
- QuotationRepository.cs (GetByIdWithNegotiationsAsync implemented)
