namespace Appraisal.Exceptions;

using Shared.Exceptions;

public class RequestAppraisalDetailNotFoundException(long id) : NotFoundException("RequestAppraisalDetail", id)
{
}