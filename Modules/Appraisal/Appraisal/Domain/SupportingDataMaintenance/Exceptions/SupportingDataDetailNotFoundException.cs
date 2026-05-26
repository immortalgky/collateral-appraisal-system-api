namespace Appraisal.Domain.SupportingDataMaintenance.Exceptions;

public class SupportingDataDetailNotFoundException(Guid id)
    : NotFoundException("SupportingDataDetail", id);