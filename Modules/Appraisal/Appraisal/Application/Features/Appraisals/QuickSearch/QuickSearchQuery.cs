using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.QuickSearch;

public record QuickSearchQuery(string Q, string Scope, int Limit) : IQuery<QuickSearchResult>;
