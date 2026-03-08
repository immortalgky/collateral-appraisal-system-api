using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetComparativeAnalysisTemplates;

public record GetComparativeAnalysisTemplatesQuery(bool ActiveOnly = false) : IQuery<GetComparativeAnalysisTemplatesResult>;
