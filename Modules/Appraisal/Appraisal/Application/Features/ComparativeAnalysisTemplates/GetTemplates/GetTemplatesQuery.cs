using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetTemplates;

public record GetTemplatesQuery(bool ActiveOnly = false) : IQuery<GetTemplatesResult>;
