namespace Appraisal.Domain.SupportingDataMaintenance.Exceptions;

public class SupportingDataNotFoundException(Guid id)
    : NotFoundException("SupportingData", id);