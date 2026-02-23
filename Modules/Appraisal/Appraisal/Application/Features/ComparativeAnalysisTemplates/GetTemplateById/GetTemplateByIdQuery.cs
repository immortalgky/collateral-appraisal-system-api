using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetTemplateById;

public record GetTemplateByIdQuery(Guid TemplateId) : IQuery<GetTemplateByIdResult>;
