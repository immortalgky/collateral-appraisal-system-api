namespace Appraisal.Application.Features.Appraisals.DeleteProperty;

public record DeletePropertyCommand(Guid appraisalId, Guid propertyId) : ICommand<DeletePropertyResult>;